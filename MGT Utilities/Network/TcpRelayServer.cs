using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net;
using System.IO;

namespace MGT.Utilities.Network
{
    public class TcpRelayServer<T> : NetworkRelay<T> where T : RelayMessage, new()
    {
        private class ClientInfo
        {
            public bool Self;
            public int Index;
            public TcpClient TcpClient;
            public int ClientId;
            public string Nickname;
            public DateTime ConnectionTime;
            public DateTime LastReceiveTime;
            public DateTime LastSendTime;
            public T LastMessage;
        }

        Thread listenerThread;
        List<Thread> clientThreads = new List<Thread>();
        System.Timers.Timer keepAliveTimer;
        Random random = new Random();
        TcpListener tcpListener;
        int connectedClients = 1;
        int maxConnections = 4;
        ClientInfo[] clientsInfo = new ClientInfo[4];

        public TcpRelayServer() { }

        public TcpRelayServer(int maxConnections)
        {
            MaxConnections = maxConnections;
        }

        public TcpRelayServer(int maxConnections, int port, int timeout, string version, string nickname)
        {
            MaxConnections = maxConnections;
            Port = port;
            Timeout = timeout;
            Version = version;
            Nickname = nickname;
        }

        public delegate void MaxConnectionsChangedEventHandler(object sender, int maxConnections);
        public event MaxConnectionsChangedEventHandler MaxConnectionsChanged;

        public int MaxConnections
        {
            get
            {
                return maxConnections;
            }
            set
            {
                if (running)
                    throw new Exception("Cannot set max connections when server is running");

                int bck = maxConnections;

                maxConnections = value;
                if (bck != value)
                    if (MaxConnectionsChanged != null)
                        MaxConnectionsChanged(this, value);
                clientsInfo = new ClientInfo[maxConnections];
            }
        }

        public override bool Start()
        {
            if (running)
                return true;

            running = true;

            SetUpSelfInformation();

            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
            }
            catch
            {
                running = false;
                ProviderDisconnected("Unable to start server", true);
                return false;
            }

            listenerThread = new Thread(() => ListenerThread());
            listenerThread.Name = "TCP Relay Server Listener";
            listenerThread.Start();

            keepAliveTimer = new System.Timers.Timer();
            keepAliveTimer.Interval = 1000;
            keepAliveTimer.Elapsed += keepAliveTimer_Elapsed;
            keepAliveTimer.Start();

            return true;
        }

        private void SetUpSelfInformation()
        {
            DateTime now = DateTime.Now;

            selfId = GetNewClientId();

            T message = new T();
            message.ClientId = selfId;

            ClientInfo clientInfo = new ClientInfo();
            clientInfo.Self = true;
            clientInfo.Index = 0;
            clientInfo.TcpClient = null;
            clientInfo.ClientId = selfId;
            clientInfo.Nickname = nickname;
            clientInfo.ConnectionTime = now;
            clientInfo.LastMessage = message;

            clientsInfo[0] = clientInfo;
        }

        public override void Stop()
        {
            if (!running)
                return;

            running = false;

            tcpListener.Stop();

            for (int i = 0; i < clientsInfo.Length; i++)
            {
                ClientInfo ssInfo = clientsInfo[i];

                if (ssInfo == null)
                    continue;

                if (ssInfo.Self)
                    continue;

                try
                {
                    ssInfo.TcpClient.GetStream().Close();
                    ssInfo.TcpClient.Close();
                }
                catch { }
            }

            keepAliveTimer.Stop();
            keepAliveTimer.Dispose();

            ProviderDisconnected("Server stopped", false);
        }

        private int GetNewClientId()
        {
            int clientId;

            bool ok = true;
            do
            {
                clientId = random.Next();

                for (int i = 0; i < clientsInfo.Length; i++)
                {
                    ClientInfo info = clientsInfo[i];
                    if (info == null)
                        continue;

                    if (info.ClientId == clientId)
                    {
                        ok = false;
                        break;
                    }
                }
            } while (!ok);

            return clientId;
        }

        private int GetEmptySlotIndex()
        {
            for (int i = 0; i < clientsInfo.Length; i++)
            {
                if (clientsInfo[i] == null)
                    return i;
            }

            return -1;
        }

        private ClientInfo GetClientInformation(int clientId)
        {
            ClientInfo clientInformation = null;
            for (int i = 0; i < clientsInfo.Length; i++)
            {
                if (clientsInfo[i] == null)
                    continue;

                if (clientsInfo[i].ClientId == clientId)
                    clientInformation = clientsInfo[i];
            }

            return clientInformation;
        }

        private void ListenerThread()
        {
            while (running)
            {
                try
                {
                    DateTime now = DateTime.Now;

                    TcpClient tcpClient = tcpListener.AcceptTcpClient();
                    tcpClient.ReceiveTimeout = timeout;
                    tcpClient.SendTimeout = timeout;

                    if (connectedClients < maxConnections)
                    {
                        int clientId = GetNewClientId();

                        if (!SendWelcome(tcpClient, clientId))
                            continue;

                        TcpRelayGreeting greeting;
                        if (!AwaitGreeting(tcpClient, out greeting))
                            continue;

                        if (!SendConnectedClientsInfo(tcpClient, clientId))
                            continue;

                        NotifyClientConnected(clientId, greeting.Nickname);

                        ClientInfo ssInfo = RegisterClientInfo(tcpClient, clientId, greeting.Nickname);

                        ClientConnected(clientId, greeting.Nickname);

                        Thread thread = new Thread(() => ReadMessagesThread(ssInfo));
                        thread.Name = "TCP Relay Server Message Reader " + ssInfo.ClientId;
                        clientThreads.Add(thread);
                        thread.Start();
                    }
                    else
                    {
                        SendServerFull(tcpClient);
                    }
                }
                catch { }
            }
        }

        private bool SendWelcome(TcpClient tcpClient, int clientId)
        {
            NetworkStream networkStream = tcpClient.GetStream();

            TcpRelayWelcome welcome = new TcpRelayWelcome();
            welcome.ClientId = clientId;
            welcome.Version = version;
            welcome.ServerFull = false;

            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Sending welcome");
#endif
                lock(networkStream)
                    formatter.Serialize(networkStream, welcome);
            }
            catch
            {
                networkStream.Close();
                tcpClient.Close();
                return false;
            }

            return true;
        }

        private bool AwaitGreeting(TcpClient tcpClient, out TcpRelayGreeting greeting)
        {
            NetworkStream networkStream = tcpClient.GetStream();

            try
            {
                object message = formatter.Deserialize(networkStream);
                greeting = (TcpRelayGreeting)message;
            }
            catch
            {
                greeting = new TcpRelayGreeting { ClientId = 0, Nickname = null };
                networkStream.Close();
                tcpClient.Close();
                return false;
            }

            return true;
        }

        private bool SendConnectedClientsInfo(TcpClient tcpClient, int clientId)
        {
            NetworkStream networkStream = tcpClient.GetStream();

            List<TCPRelayClientInfo> list = new List<TCPRelayClientInfo>();
            for (int i = 0; i < clientsInfo.Length; i++)
            {
                ClientInfo ssInfo = clientsInfo[i];
                if (ssInfo == null)
                    continue;

                if (ssInfo.ClientId == clientId)
                    continue;

                TCPRelayClientInfo csInfo = new TCPRelayClientInfo();
                csInfo.ClientId = ssInfo.ClientId;
                csInfo.Connected = true;
                csInfo.Nickname = ssInfo.Nickname;

                list.Add(csInfo);
            }

            TCPRelayClientInfo[] csInfos = list.ToArray();

            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Sending clients informations");
#endif
                lock (networkStream)
                    formatter.Serialize(networkStream, csInfos);
            }
            catch
            {
                networkStream.Close();
                tcpClient.Close();
                return false;
            }

            return true;
        }

        private void SendServerFull(TcpClient tcpClient)
        {
            NetworkStream networkStream = tcpClient.GetStream();

            TcpRelayWelcome welcome = new TcpRelayWelcome();
            welcome.ClientId = 0;
            welcome.ServerFull = true;
            try
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("Sending server full");
#endif
                lock(networkStream)
                    formatter.Serialize(networkStream, welcome);
            }
            finally
            {
                networkStream.Close();
                tcpClient.Close();
            }
        }

        private void NotifyClientConnected(int clientId, string nickname)
        {
            TCPRelayClientInfo csInfo = new TCPRelayClientInfo();
            csInfo.ClientId = clientId;
            csInfo.Connected = true;
            csInfo.Nickname = nickname;
            for (int i = 0; i < clientsInfo.Length; i++)
            {
                ClientInfo ssInfo = clientsInfo[i];
                if (ssInfo == null)
                    continue;

                if (ssInfo.Self)
                    continue;

                if (ssInfo.ClientId == clientId)
                    continue;

                NetworkStream networkStream = ssInfo.TcpClient.GetStream();

                try
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Sending client information");
#endif
                    lock (networkStream)
                        formatter.Serialize(networkStream, csInfo);
                    ssInfo.LastSendTime = DateTime.Now;
                }
                catch { }
            }
        }

        private ClientInfo RegisterClientInfo(TcpClient tcpClient, int clientId, string nickname)
        {
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.Self = false;
            clientInfo.Index = GetEmptySlotIndex();
            clientInfo.TcpClient = tcpClient;
            clientInfo.ClientId = clientId;
            clientInfo.Nickname = nickname;
            clientInfo.ConnectionTime = DateTime.Now;
            clientInfo.LastReceiveTime = DateTime.Now;
            clientInfo.LastSendTime = DateTime.Now;
            clientInfo.LastMessage = new T();
            clientInfo.LastMessage.ClientId = clientInfo.ClientId;

            clientsInfo[clientInfo.Index] = clientInfo;

            connectedClients++;

            return clientInfo;
        }

        private void NotifyClientDisconnected(ClientInfo clientInfo)
        {
            TCPRelayClientInfo csInfo = new TCPRelayClientInfo();
            csInfo.ClientId = clientInfo.ClientId;
            csInfo.Connected = false;

            clientsInfo[clientInfo.Index] = null;
            connectedClients--;

            if (running)
            {
                for (int i = 0; i < clientsInfo.Length; i++)
                {
                    ClientInfo ssInfo = clientsInfo[i];

                    if (ssInfo == null)
                        continue;

                    if (ssInfo.Self)
                        continue;

                    NetworkStream networkStream = ssInfo.TcpClient.GetStream();
                    try
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine("Sending disconnect notification");
#endif
                        lock (networkStream)
                            formatter.Serialize(networkStream, csInfo);
                        ssInfo.LastSendTime = DateTime.Now;
                    }
                    catch { }
                }
            }

            ClientDisconnected(clientInfo.ClientId);
        }

        private void ReadMessagesThread(ClientInfo clientInformation)
        {
            TcpClient tcpClient = clientInformation.TcpClient;
            NetworkStream stream = tcpClient.GetStream();
            while (running)
            {
                try
                {
                    object message = formatter.Deserialize(stream);
                    if (message is T)
                    {
                        clientInformation.LastMessage = (T)message;
                        clientInformation.LastReceiveTime = DateTime.Now;
                        ClientMessageReceived((T)message);
                        RelayMessage((T)message, clientInformation);
                    }
                    else if (message is KeepAliveMessage)
                    {
                        clientInformation.LastReceiveTime = DateTime.Now;
                    }
                }
                catch
                {
                    NotifyClientDisconnected(clientInformation);
                    break;
                }
            }
            stream.Close();
            tcpClient.Close();
        }

        private void RelayMessage(T message, ClientInfo clientInformation)
        {
            for (int i = 0; i < clientsInfo.Length; i++)
            {
                ClientInfo info = clientsInfo[i];
                if (info == null)
                    continue;

                if (info.Self)
                    continue;

                if (info.ClientId == clientInformation.ClientId)
                    continue;

                try
                {
                    NetworkStream networkStream = info.TcpClient.GetStream();
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Sending message relay");
#endif
                    lock(networkStream)
                        formatter.Serialize(networkStream, message);
                    info.LastSendTime = DateTime.Now;
                }
                catch { }
            }
        }

        void keepAliveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SendKeepAlives();
        }

        private void SendKeepAlives()
        {
            for (int i = 0; i < clientsInfo.Length; i++)
            {
                DateTime now = DateTime.Now;

                ClientInfo info = clientsInfo[i];
                if (info == null)
                    continue;

                if (info.Self)
                    continue;

                bool receive = true;
                bool send = true;

                if ((now - info.LastReceiveTime).TotalMilliseconds >= timeout / 4)
                    receive = false;

                if ((now - info.LastSendTime).TotalMilliseconds >= timeout / 4)
                    send = false;

                if (receive && send)
                    continue;

                KeepAliveMessage keepAlive = new KeepAliveMessage();
                keepAlive.ClientId = info.ClientId;

                NetworkStream networkStream = info.TcpClient.GetStream();
                try
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Sending keep alive");
#endif
                    lock(networkStream)
                        formatter.Serialize(networkStream, keepAlive);
                    info.LastSendTime = DateTime.Now;
                }
                catch { }
            }
        }

        public override void Send(T message)
        {
            if (!running)
                throw new Exception("Cannot send messages when server is not running");

            message.ClientId = selfId;

            for (int i = 0; i < clientsInfo.Length; i++)
            {
                ClientInfo info = clientsInfo[i];
                if (info == null)
                    continue;

                if (info.Self)
                    continue;

                NetworkStream networkStream = info.TcpClient.GetStream();
                try
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Sending message");
#endif
                    lock(networkStream)
                        formatter.Serialize(networkStream, message);
                    info.LastSendTime = DateTime.Now;
                }
                catch { }
            }
        }

        public override void Init()
        {
            base.Init();
            if (MaxConnectionsChanged != null)
                MaxConnectionsChanged(this, maxConnections);
        }
    }
}
