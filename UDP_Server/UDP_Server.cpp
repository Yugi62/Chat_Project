#include <iostream>
#include <algorithm>
#include <string>
#include <list>
#include <random>
#include <sstream>
#include <iomanip>

#include <process.h>
#include <winsock2.h>
#include <windows.h>

#include "Player.h"
using namespace std;

#define PINGTHREAD_WAIT_TIME 100
#define MAX_DISCONNECT_COUNT 5
#define BUF_SIZE 1024

typedef struct PACKET
{
	OVERLAPPED overlapped;
	SOCKADDR_IN clntAddr;
	int clntAddr_Size;
	WSABUF wsaBuf;
	char buf[BUF_SIZE];
	bool isReceving;
}*LP_PACKET;

enum PacketType
{
	ACCEPT = 1,
	PING = 2,
	MESSAGE = 3,
	GAME = 4
};

unsigned WINAPI IOCPThread(void* arg);
unsigned WINAPI PingThread(void* arg);

Player* CheckIPExist(SOCKADDR_IN addr);
void Accept(SOCKET sock, LP_PACKET packet, string packetBuffer);

string SetPacketType(PacketType _packetType);
void ErrorHandling(const char* message);

list<Player*> player_List;

int main(void)
{
	WSADATA wsaData;
	SOCKET servSock;
	SOCKADDR_IN servAddr;

	WSAEVENT readEvent;
	HANDLE iocp;
	LP_PACKET packet;
	DWORD flags = 0;

	//Version 2.2
	if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0)
		ErrorHandling("WSAStartup() error");

	//UDP 소켓 생성 (Overlapped IO + Non-blocking)
	servSock = WSASocket(PF_INET, SOCK_DGRAM, 0, NULL, 0, WSA_FLAG_OVERLAPPED);
	if (servSock == INVALID_SOCKET)
		ErrorHandling("socket() error");

	//소켓 바인드
	memset(&servAddr, 0, sizeof(servAddr));
	servAddr.sin_family = AF_INET;
	servAddr.sin_addr.s_addr = htonl(INADDR_ANY);
	servAddr.sin_port = htons(atoi("10200"));
	if (bind(servSock, (const SOCKADDR*)&servAddr, sizeof(servAddr)) == SOCKET_ERROR)
		ErrorHandling("bind() error");

	//수신 이벤트 초기화 (Non-signaled + Auto Reset)
	readEvent = CreateEvent(NULL, false, false, NULL);
	if (WSAEventSelect(servSock, readEvent, FD_READ) == SOCKET_ERROR)
		ErrorHandling("WSAEventSelect() error");

	//IOCP 초기화 (IO 완료 시 IOCPThread에서 처리)
	iocp = CreateIoCompletionPort(INVALID_HANDLE_VALUE, NULL, 0, 0);
	if (CreateIoCompletionPort((HANDLE)servSock, iocp, (ULONG_PTR)servSock, 0) == NULL)
		ErrorHandling("CreateIoCompletionPort() error");

	//IOCPThread 시작
	if (_beginthreadex(NULL, 0, IOCPThread, (void*)iocp, 0, NULL) == 0)
		ErrorHandling("Failed to begin IOCPThread");


	//PingThread 시작
	if (_beginthreadex(NULL, 0, PingThread, (void*)servSock, 0, NULL) == 0)
		ErrorHandling("Failed to begin PingThread");


	while (true)
	{
		//버퍼에 데이터가 들어올 때까지 블럭
		WaitForSingleObject(readEvent, INFINITE);

		//수신용 패킷 동적할당 (이후 IOCPThread에서 할당해제)
		packet = new PACKET;
		memset(&(packet->overlapped), 0, sizeof(packet->overlapped));
		packet->clntAddr_Size = sizeof(packet->clntAddr);
		packet->wsaBuf.buf = packet->buf;
		packet->wsaBuf.len = sizeof(packet->buf);
		packet->isReceving = true;

		WSARecvFrom(
			servSock,
			&(packet->wsaBuf),
			1,
			NULL,
			&flags,
			(SOCKADDR*)&(packet->clntAddr),
			&(packet->clntAddr_Size),
			&(packet->overlapped),
			NULL);
	}

	WSACloseEvent(readEvent);
	closesocket(servSock);
	WSACleanup();
	return 0;
}

unsigned WINAPI IOCPThread(void* arg)
{
	HANDLE iocp = (HANDLE)arg;
	SOCKET sock;
	LP_PACKET packet;
	DWORD bytesTransferred;

	while (true)
	{
		GetQueuedCompletionStatus(iocp, &bytesTransferred, (PULONG_PTR)&sock, (LPOVERLAPPED*)&packet, INFINITE);

		//수신받는 데이터에 한해서만 IOCP 적용
		if (!packet->isReceving)
			continue;

		//수신받은 패킷에서 쓰레기 값 처리
		string packetBuffer = packet->buf;
		packetBuffer = packetBuffer.substr(0, packetBuffer.find(-51));

		//패킷의 용도와 실제 데이터를 분리
		int packetType = strtol(packetBuffer.substr(0, 2).c_str(), NULL, 16);
		packetBuffer = packetBuffer.substr(2);

		/*
		수신받은 pacetType에 따라 작업을 분리

		ACCEPT  : player_List에 IP/Port 등록
		PING    : player_List에 존재하는 player에게 PING이 들어온 경우 초기화
		MESSAGE : 수신 받은 대상을 제외한 모든 클라이언트에게 뒤에 '\\' + playerName을 붙여 전송 (Ex. "03Hello\Player01")

		*/
		switch (packetType)
		{

		case PacketType::ACCEPT:
		{
			//리스트에 IP/Port가 존재하는지 확인
			bool playerExists = false;
			for (auto i = player_List.begin(); player_List.end() != i; i++)
			{
				if ((*i)->CheckAddr(packet->clntAddr))
					playerExists = true;
			}
			//IP/Port가 존재하는 경우 리스트에 등록
			if (!playerExists)
			{
				Player* newPlayer = new Player(packetBuffer, packet->clntAddr);
				player_List.push_back(newPlayer);
				cout << inet_ntoa(packet->clntAddr.sin_addr) << "/" << packet->clntAddr.sin_port << " has been accessed" << endl;

				PACKET packetForSending;

				char* begin = packetForSending.buf + 0;
				char* end = packetForSending.buf + sizeof(packetForSending.buf);
				fill(begin, end, 0);

				memset(&packetForSending.overlapped, 0, sizeof(packetForSending.overlapped));

				packetBuffer = SetPacketType(PacketType::ACCEPT) + packetBuffer;
				std::strcpy(packetForSending.buf, packetBuffer.c_str());

				packetForSending.wsaBuf.buf = packetForSending.buf;
				packetForSending.wsaBuf.len = sizeof(packetForSending.buf);
				packetForSending.isReceving = false;

				WSASendTo(
					sock,
					&packetForSending.wsaBuf,
					1,
					NULL,
					0,
					(const SOCKADDR*)&packet->clntAddr,
					sizeof(packet->clntAddr),
					&packetForSending.overlapped,
					NULL
				);

				newPlayer->SetIsAccepted(true);
			}
			break;
		}


		case PacketType::PING:
		{
			//player_List에 IP/Port가 존재하면 해당하는 player 반환
			Player* player = CheckIPExist(packet->clntAddr);

			//player가 존재하는 경우 수신받은 ping을 초기화
			if (player != NULL)
				player->SetPing(packetBuffer);
			break;
		}


		case PacketType::MESSAGE:
		{
			//player가 player_List에 존재하는 지 확인
			Player* currentPlayer = CheckIPExist(packet->clntAddr);
			if (currentPlayer != NULL)
			{
				//수신받은 데이터를 가공 후 수신자를 제외한 모든 클라이언트에게 전송
				PACKET packetForSending;

				char* begin = packetForSending.buf + 0;
				char* end = packetForSending.buf + sizeof(packetForSending.buf);
				fill(begin, end, 0);

				memset(&packetForSending.overlapped, 0, sizeof(packetForSending.overlapped));

				packetBuffer = packetBuffer + "\\" + currentPlayer->GetPlayerName();
				packetBuffer = SetPacketType(PacketType::MESSAGE) + packetBuffer;
				std::strcpy(packetForSending.buf, packetBuffer.c_str());

				packetForSending.wsaBuf.buf = packetForSending.buf;
				packetForSending.wsaBuf.len = sizeof(packetForSending.buf);
				packetForSending.isReceving = false;

				for (auto player : player_List)
				{
					if (player == currentPlayer)
						continue;

					SOCKADDR_IN playerAddr = player->GetAddr();

					WSASendTo(
						sock,
						&packetForSending.wsaBuf,
						1,
						NULL,
						0,
						(const SOCKADDR*)&playerAddr,
						sizeof(playerAddr),
						&packetForSending.overlapped,
						NULL
					);
				}
			}
			break;
		}
		}

		//처리가 완료된 패킷은 할당 해제
		delete packet;
	}

	return 0;
}


unsigned WINAPI PingThread(void* arg)
{
	/*
	PingThread : 서버 시작 직후 호출되며 다음과 같이 작동
	1. 리스트에 저장되어 있는 모든 IP에게 지속적으로 SYN을 보낸다 (SYN은 1000~9998 사이의 난수)
	2. PINGTHREAD_WAIT_TIME만큼 기다리며 그 사이의 클라이언트로부터 ACK를 받아 ACK == SYN +1인지 확인한다
	3. 연속적으로 ACK 값이 다른 경우 카운트를 1 올리며 카운트가 MAX_DISCONNECT_COUNT가 되었을 때 리스트에서 IP/Port를 제거한다 (연결해제)

	*/

	SOCKET sock = (SOCKET)arg;

	random_device rd;
	mt19937 gen(rd());
	uniform_int_distribution<> dist(1000, 9998);


	while (true)
	{
		string packetType = SetPacketType(PacketType::PING);

		int currentPing = dist(gen);
		string SYN = to_string(currentPing);

		PACKET packet;
		char* begin = packet.buf + 0;
		char* end = packet.buf + sizeof(packet.buf);
		fill(begin, end, 0);

		memset(&packet.overlapped, 0, sizeof(packet.overlapped));
		std::strcpy(packet.buf, (packetType + SYN).c_str());
		packet.wsaBuf.buf = packet.buf;
		packet.wsaBuf.len = sizeof(packet.buf);
		packet.isReceving = false;

		for (auto player : player_List)
		{
			if (!player->GetIsAccpeted())
				continue;

			SOCKADDR_IN playerAddr = player->GetAddr();

			WSASendTo(
				sock,
				&packet.wsaBuf,
				1,
				NULL,
				0,
				(const SOCKADDR*)&playerAddr,
				sizeof(playerAddr),
				&packet.overlapped,
				NULL
			);
		}

		Sleep(PINGTHREAD_WAIT_TIME);

		auto it = player_List.begin();
		while (it != player_List.end())
		{
			int playerPing = (*it)->GetPing();

			if (playerPing != currentPing + 1)
				(*it)->AddDisconnectCount();
			else if (playerPing == currentPing + 1)
				(*it)->ClearDisconnectCount();

			if ((*it)->GetDisConnectCount() >= MAX_DISCONNECT_COUNT)
			{
				auto _it = it;
				if ((++_it) == player_List.end())
				{
					cout << inet_ntoa((*it)->GetAddr().sin_addr) << "/" << (*it)->GetAddr().sin_port << " disconnected" << endl;
					player_List.erase(it);
					break;
				}
				else
				{
					cout << inet_ntoa((*it)->GetAddr().sin_addr) << "/" << (*it)->GetAddr().sin_port << " disconnected" << endl;
					player_List.erase(it);
					it = _it;
					continue;
				}
			}
			it++;
		}
	}

	return 0;
}

Player* CheckIPExist(SOCKADDR_IN addr)
{
	/*
	CheckIPExist : enum인 PacketType를 받아 16진수로 변환 후 십의 자리가 비었다면 '0'을 채운 후 string으로 반환한다

	*/
	for (auto i : player_List)
	{
		if (i->CheckAddr(addr))
			return i;
	}
	return NULL;
}

string SetPacketType(PacketType _packetType)
{
	/*
	SetPacketType : enum인 PacketType를 받아 16진수로 변환 후 십의 자리가 비었다면 '0'을 채운 후 string으로 반환한다

	*/

	ostringstream os;
	os << setfill('0') << setw(2) << hex << _packetType;
	return os.str();
}

void ErrorHandling(const char* message)
{
	/*
	ErrorHandling : 에러 발생 시 발생 시 에러 출력 후 프로그램 종료

	*/

	fputs(message, stderr);
	fputc('\n', stderr);
	exit(1);
}