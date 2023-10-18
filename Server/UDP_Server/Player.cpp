#include "Player.h"

string Player::GetPlayerName()
{
	return playerName;
}

SOCKADDR_IN Player::GetAddr()
{
	return playerAddr;
}

bool Player::CheckAddr(SOCKADDR_IN _playerAddr)
{
	return playerAddr.sin_addr.s_addr == _playerAddr.sin_addr.s_addr && playerAddr.sin_port == _playerAddr.sin_port;
}

void Player::SetPing(int _ping)
{
	ping = _ping;
}

void Player::SetPing(string _ping)
{
	ping = stoi(_ping);
}

int Player::GetPing()
{
	return ping;
}

void Player::AddDisconnectCount()
{
	disconnectCount++;
}

void Player::ClearDisconnectCount()
{
	disconnectCount = 0;
}

int Player::GetDisConnectCount()
{
	return disconnectCount;
}

bool Player::GetIsAccpeted()
{
	return isAccepted;
}

void Player::SetIsAccepted(bool _isAccepted)
{
	isAccepted = _isAccepted;
}