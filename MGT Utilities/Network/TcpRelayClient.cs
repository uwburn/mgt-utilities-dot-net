using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net;
using System.IO;

namespace MGT.Utilities.Network
{
    public class TcpRelayClient<T> : NetworkRelay<T> where T : RelayMessage, new()
    {
        string serverAddress;
        TcpClient tcpClient;
        Thread readMessagesThread;

        bool sendMessages = false;

        public TcpRelayClient() { }

        public TcpRelayClient(string serverAddress)
        {
            ServerAddress = serverAddress;
        }

        public TcpRelayClient(string serverAddress, int port, int timeout, string version, string nickname)
        {
            ServerAddress = serverAddress;
            Port = port;
            Timeout = timeout;
            Version = version;
            Nickname = nickname;
        }

        public delegate void ServerAddressChangedEventHandler(object sender, string serverAddress);
        public event ServerAddressChangedEventHandler ServerAddressChanged;

        public string ServerAddress
        {
            get
            {
                return serverAddress;
            }
            set
            {
                if (running)
                    throw new Exception("Cannot set server address when client is running");

                string bck = serverAddress;

                serverAddress = value;
                if (bck != value)
                    if (ServerAddressChanged != null)
                        ServerAddressChanged(this, value);
            }
        }

        public override bool Start()
        {
            if (running)
                return true;

            running = true;

            if (!SetupClient())
                return false;

            if (!AwaitWelcome())
                return false;

            if (!SendGreeting())
                return false;

            if (!GetConnectedClientsInfo())
                return false;

            sendMessages = true;

            readMessagesThread = new Thread(() => ReadMessagesThread(tcpClient));
            readMessagesThread.Name = "TCP Relay Client Message Reader";
            readMessagesThread.Start();

            return true;
        }

        private bool SetupClient()
        {
            try
            {
                tcpClient = new TcpClient(serverAddress, port);
            }
            catch
            {
                running = false;
                ProviderDisconnected("Server did not respond", true);
                return false;
            }
            tcpClient.ReceiveTimeout = timeout;
            tcpClient.SendTimeout = timeout;

            return true;
        }

        private bool AwaitWelcome()
        {
            NetworkStream networkStream = tcpClient.GetStream();
            try
            {
                object message = formatter.Deserialize(networkStream);
                TcpRelayWelcome welcome = (TcpRelayWelcome)message;
                if (welcome.ServerFull)
                {
                    AbortConnection("Server is full");
                    return false;
                }

                if (welcome.Version != this.version)
                {
                    AbortConnection("Version mismatch");
                    return false;
                }

                selfId = welcome.ClientId;
            }
            catch
            {
                AbortConnection("Corrupeted message");
                return false;
            }

            return true;
        }

        private bool SendGreeting()
        {
            NetworkStream networkStream = tcpClient.GetStream();

            try
            {
                TcpRelayGreeting greeting = new TcpRelayGreeting { ClientId = selfId, Nickname = nickname };
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Sending greeting");
#endif
                lock(networkStream)
                    formatter.Serialize(networkStream, greeting);
            }
            catch
            {
                AbortConnection("Corrupeted message");
                return false;
            }

            return true;
        }

        private bool GetConnectedClientsInfo()
        {
            NetworkStream networkStream = tcpClient.GetStream();

            TCPRelayClientInfo[] csClientsInfo;
            try
            {
                object message = formatter.Deserialize(networkStream);
                csClientsInfo = (TCPRelayClientInfo[])message;
            }
            catch
            {
                AbortConnection("Corrupeted message");
                return false;
            }
            for (int i = 0; i < csClientsInfo.Length; i++)
            {
                TCPRelayClientInfo csClientInfo = csClientsInfo[i];
                ClientConnected(csClientInfo.ClientId, csClientInfo.Nickname);
            }

            return true;
        }

        private void AbortConnection(string cause)
        {
            running = false;
            try
            {
                tcpClient.GetStream().Close();
            }
            catch { }
            try
            {
                tcpClient.Close();
            }
            catch { }
            ProviderDisconnected(cause, true);
        }

        public override void Stop()
        {
            if (!running)
                return;

            running = false;

            try
            {
                tcpClient.GetStream().Close();
            }
            catch { }
            try
            {
                tcpClient.Close();
            }
            catch { }

            //readMessagesThread.Abort();

            ProviderDisconnected("Client stopped", false);
        }

        private void ReadMessagesThread(TcpClient tcpClient)
        {
            NetworkStream stream = tcpClient.GetStream();
            while (running)
            {
                try
                {
                    object message = formatter.Deserialize(stream);
                    if (message is T)
                        ClientMessageReceived((T)message);
                    else if (message is TCPRelayClientInfo)
                        ProcessClientInformation((TCPRelayClientInfo)message);
                    else if (message is KeepAliveMessage)
                        ReplyKeepAlive((KeepAliveMessage)message);
                }
                catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                    if (running)
                    {
                        ProviderDisconnected("Unable to read NetworkStream", true);
                        running = false;
                    }
                    break;
                }
            }
            stream.Close();
            tcpClient.Close();
        }

        private void ProcessClientInformation(TCPRelayClientInfo message)
        {
            if (message.Connected)
                ClientConnected(message.ClientId, message.Nickname);
            else
                ClientDisconnected(message.ClientId);
        }

        private void ReplyKeepAlive(KeepAliveMessage keepAliveMessage)
        {
            NetworkStream networkStream = tcpClient.GetStream();
#if DEBUG
            System.Diagnostics.Debug.WriteLine("Replying keep alive");
#endif
            lock(networkStream)
                formatter.Serialize(networkStream, keepAliveMessage);
        }

        public override void Send(T heartRateMessage)
        {
            if (!running)
                return;

            if (!sendMessages)
                return;

            heartRateMessage.ClientId = selfId;

            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Sending message");
#endif
                lock (networkStream)
                    formatter.Serialize(networkStream, heartRateMessage);
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                AbortConnection("Unable to write NetworkStream");
            }
        }

        public override void Init()
        {
            base.Init();
            ServerAddressChanged(this, serverAddress);
        }
    }
}
