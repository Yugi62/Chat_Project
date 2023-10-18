using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.NetworkInformation;

public class ClientSystem : MonoBehaviour
{
    enum PacketType
    {
        ACCEPT = 1,
        PING = 2,
        MESSAGE = 3,
        GAME = 4
    }

    private Socket clntSock;
    private EndPoint servEP;
    private string playerName;

    private bool isAccepting;
    private bool isConnected;

    private float acceptWaitTimer;
    private float disconnectTimer;

    private byte[] buffer;
    private const int max_BufferSize = 1024;

    [SerializeField] private TMP_InputField name_InputField;
    [SerializeField] private TMP_InputField ip_InputField;
    [SerializeField] private TMP_InputField port_InputField;

    [SerializeField] private GameObject secondUI;
    [SerializeField] private GameObject thirdUI;
    [SerializeField] private TMP_Text chatLog;

    [SerializeField] private float acceptWaitTime;
    [SerializeField] private float disconnectTime;

    private void Awake()
    {
        //UDP 소켓 생성 (Non-Blocking)
        clntSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        clntSock.Blocking = false;

        isAccepting = false;
        isConnected = false;
        acceptWaitTimer = 0;
        disconnectTimer = 0;

        //버퍼 초기화
        buffer = new byte[max_BufferSize];
    }
    private void Update()
    {
        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

        if (isConnected)
        {
            try
            {
                clntSock.ReceiveFrom(buffer, SocketFlags.None, ref endPoint);
                disconnectTimer = 0.0f;

                string arrangedBuffer = ArrangeBuffer(buffer);

                switch(CheckPacketType(buffer))
                {
                    case PacketType.ACCEPT:
                        break;

                    case PacketType.PING:
                        // Ping에 1을 더한 후 string으로 변환, 그리고 앞에 PacketType을 추가하고 서버에게 SendTo를 한다
                        int ping = int.Parse(arrangedBuffer);
                        ping++;
                        arrangedBuffer = ping.ToString();
                        arrangedBuffer = SetPacketType(arrangedBuffer, PacketType.PING);
                        byte[] dataToBytes = Encoding.UTF8.GetBytes(arrangedBuffer);
                        clntSock.SendTo(dataToBytes, servEP);
                        break;

                    case PacketType.MESSAGE:
                        string name = arrangedBuffer.Substring(arrangedBuffer.IndexOf('\\') + 1);
                        arrangedBuffer = arrangedBuffer.Substring(0, arrangedBuffer.IndexOf('\\'));
                        chatLog.text += name + " : " + arrangedBuffer + '\n';
                        break;

                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                disconnectTimer += Time.deltaTime;
            }

            if (disconnectTimer > disconnectTime)
            {
                Debug.Log("Disconnected");
                isConnected = false;
            }
        }

        else if (!isConnected)
        {
            if(isAccepting)
            {
                try
                {
                    clntSock.ReceiveFrom(buffer, SocketFlags.None, ref endPoint);
                    string arrangedBuffer = ArrangeBuffer(buffer);

                    if (CheckPacketType(buffer) == PacketType.ACCEPT)
                    {
                        if (arrangedBuffer == playerName)
                        {
                            isConnected = true;
                            isAccepting = false;

                            secondUI.SetActive(false);
                            thirdUI.SetActive(true);

                            Debug.Log("Sucessfully connected to Server");
                        }
                    }
                }
                catch (Exception e)
                {
                    acceptWaitTimer += Time.deltaTime;
                }

                if(acceptWaitTimer > acceptWaitTime)
                {
                    Debug.Log("Failed to connected to Server");
                    isAccepting = false;
                }
            }
        }
    }

    private string ArrangeBuffer(byte[] buffer)
    {
        /*
        ArrangePacket : byte인 버퍼를 string으로 인코딩 후 사용되는 부분만 잘라서 반환 (PacketType 부분도 같이 자른다)

        */

        string arrangedBuffer = Encoding.UTF8.GetString(buffer);
        arrangedBuffer = arrangedBuffer.Substring(0, arrangedBuffer.IndexOf('\0'));
        arrangedBuffer = arrangedBuffer.Substring(2);
        return arrangedBuffer;
    }

    private PacketType CheckPacketType(byte[] buffer)
    {
        /*
        CheckPacketType : 서버로부터 받은 버퍼의 앞 두 글자(패킷의 용도)를 받아 PacketType으로 반환 

        */

        string bufferToString = Encoding.UTF8.GetString(buffer);
        bufferToString = bufferToString.Substring(0, 2);
        return (PacketType)System.Convert.ToInt32(bufferToString);
    }

    private string SetPacketType(string original, PacketType type)
    {
        /*
        SetPacketType : 패킷 앞에 어떤 용도로 사용되었는지 16진수로 명시하는 작업을 실시 (용도의 종류는 PacketType 참조)
         1. type를 16진수 변환 후 문자열로 typeString에 저장
         2. typeString + 원본 데이터 순서로 문자열 재구축 후 반환 (Ex. 011452 = 핑으로 사용되는 네자리 난수)
        */

        string typeString = System.Convert.ToString((int)type, 16);
        typeString = typeString.PadLeft(2, '0');
        original = typeString + original;
        return original;
    }

    public void ConnectToServer()
    {
        /*
        ConnectToServer : 버튼 UI로 호출되며 다음과 같이 작동한다
         1. servEP에 inputfield에 저장된 IP/Port를 초기화
         2. 용도(Accept) + name을 서버에게 전송
         3. 서버로부터 에코를 받은 경우 연결 완료 (isConnected = true)
        */

        if (!isConnected)
        {
            if (ip_InputField.text != null && port_InputField.text != null)
            {
                //1.
                servEP = new IPEndPoint(IPAddress.Parse(ip_InputField.text), int.Parse(port_InputField.text));

                //2.
                playerName = name_InputField.text;

                string name = name_InputField.text;
                name = SetPacketType(name, PacketType.ACCEPT);
                byte[] nameToBytes = Encoding.UTF8.GetBytes(name);
                clntSock.SendTo(nameToBytes, servEP);

                //3.
                //임시로 isConnected 시키는 문장 (ReceiveFrom이 싪패했을 때 대처 사항이 아직 없음)
                isAccepting = true;
            }  
        }
    }

    public void SendData(string data)
    {
        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

        data = SetPacketType(data, PacketType.MESSAGE);
        byte[] dataToBytes = Encoding.UTF8.GetBytes(data);
        clntSock.SendTo(dataToBytes, servEP);
    }
}
