# 개요
Chat_Project는 C++로 만든 UDP 서버와 유니티 클라이언트로 이루어진 채팅 프로그램입니다 <br/>

# 로직
UDP로 채팅 프로그램을 만들기 위해서 다음과 같은 단계를 거쳤습니다<br/>
1. 수신이 될 때마다 IP/Port를 확인 후 List에 없는 경우 push_back (연결)
2. 모든 클라이언트에게 Ping을 보내 연속적으로 ACK가 없는 경우 List에서 erase (연결종료)

# 시연
![ServerDemonstration](https://github.com/Yugi62/Chat_Project/assets/30305369/ee5ad4ce-ecfd-4390-b15b-916dae34cd87)
