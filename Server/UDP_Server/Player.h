#pragma once
#include <string>
#include <winsock2.h>
using namespace std;

class Player
{
private:
	string playerName;
	SOCKADDR_IN playerAddr;

	bool isAccepted = false;

	int ping = 0;
	int disconnectCount = 0;

public:
	Player(string _playerName, SOCKADDR_IN _playerAddr) : playerName(_playerName), playerAddr(_playerAddr)
	{

	}

	string GetPlayerName();

	SOCKADDR_IN GetAddr();
	bool CheckAddr(SOCKADDR_IN _playerAddr);

	void SetPing(int _ping);
	void SetPing(string _ping);
	int GetPing();

	void AddDisconnectCount();
	void ClearDisconnectCount();
	int GetDisConnectCount();

	bool GetIsAccpeted();
	void SetIsAccepted(bool _isAccepted);
};