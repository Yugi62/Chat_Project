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
        //UDP ���� ���� (Non-Blocking)
        clntSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        clntSock.Blocking = false;

        isAccepting = false;
        isConnected = false;
        acceptWaitTimer = 0;
        disconnectTimer = 0;

        //���� �ʱ�ȭ
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
                        // Ping�� 1�� ���� �� string���� ��ȯ, �׸��� �տ� PacketType�� �߰��ϰ� �������� SendTo�� �Ѵ�
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
        ArrangePacket : byte�� ���۸� string���� ���ڵ� �� ���Ǵ� �κи� �߶� ��ȯ (PacketType �κе� ���� �ڸ���)

        */

        string arrangedBuffer = Encoding.UTF8.GetString(buffer);
        arrangedBuffer = arrangedBuffer.Substring(0, arrangedBuffer.IndexOf('\0'));
        arrangedBuffer = arrangedBuffer.Substring(2);
        return arrangedBuffer;
    }

    private PacketType CheckPacketType(byte[] buffer)
    {
        /*
        CheckPacketType : �����κ��� ���� ������ �� �� ����(��Ŷ�� �뵵)�� �޾� PacketType���� ��ȯ 

        */

        string bufferToString = Encoding.UTF8.GetString(buffer);
        bufferToString = bufferToString.Substring(0, 2);
        return (PacketType)System.Convert.ToInt32(bufferToString);
    }

    private string SetPacketType(string original, PacketType type)
    {
        /*
        SetPacketType : ��Ŷ �տ� � �뵵�� ���Ǿ����� 16������ �����ϴ� �۾��� �ǽ� (�뵵�� ������ PacketType ����)
         1. type�� 16���� ��ȯ �� ���ڿ��� typeString�� ����
         2. typeString + ���� ������ ������ ���ڿ� �籸�� �� ��ȯ (Ex. 011452 = ������ ���Ǵ� ���ڸ� ����)
        */

        string typeString = System.Convert.ToString((int)type, 16);
        typeString = typeString.PadLeft(2, '0');
        original = typeString + original;
        return original;
    }

    public void ConnectToServer()
    {
        /*
        ConnectToServer : ��ư UI�� ȣ��Ǹ� ������ ���� �۵��Ѵ�
         1. servEP�� inputfield�� ����� IP/Port�� �ʱ�ȭ
         2. �뵵(Accept) + name�� �������� ����
         3. �����κ��� ���ڸ� ���� ��� ���� �Ϸ� (isConnected = true)
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
                //�ӽ÷� isConnected ��Ű�� ���� (ReceiveFrom�� �������� �� ��ó ������ ���� ����)
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