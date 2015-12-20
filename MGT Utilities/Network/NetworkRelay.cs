using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using MGT.Utilities.EventHandlers;
using Gramma.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace MGT.Utilities.Network
{
    public abstract class NetworkRelay<T> where T : RelayMessage, new()
    {
        protected IFormatter formatter = new FastBinaryFormatter();

        protected bool running = false;
        public bool Running { get { return running; } }

        public event GenericEventHandler<int> PortChanged;

        protected int port = 10308;
        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                if (running)
                    throw new Exception("Cannot set port when network provider is running");

                int bck = port;

                port = value;
                if (bck != value)
                    if (PortChanged != null)
                        PortChanged(this, value);
            }
        }

        public event GenericEventHandler<int> TimeoutChanged;

        protected int timeout = 20000;
        public int Timeout
        {
            get
            {
                return timeout;
            }
            set
            {
                if (running)
                    throw new Exception("Cannot set timeout when network provider is running");

                int bck = timeout;

                timeout = value;
                if (bck != value)
                    if (TimeoutChanged != null)
                        TimeoutChanged(this, value);
            }
        }

        public event GenericEventHandler<string> VersionChanged;

        protected string version = "0.0.1";
        public string Version
        {
            get
            {
                return version;
            }
            set
            {
                if (running)
                    throw new Exception("Cannot set versione when network provider is running");

                string bck = version;

                version = value;
                if (bck != value)
                    if (VersionChanged != null)
                        VersionChanged(this, value);
            }
        }

        public event GenericEventHandler<string> NicknameChanged;

        protected string nickname = "Nickname";
        public string Nickname
        {
            get
            {
                return nickname;
            }
            set
            {
                if (running)
                    throw new Exception("Cannot set nickname when network provider is running");

                string bck = nickname;

                nickname = value;
                if (bck != value)
                    if (NicknameChanged != null)
                        NicknameChanged(this, value);
            }
        }

        protected int selfId;

        public event GenericEventHandler<int, string> OnClientConnected;
        public event GenericEventHandler<int> OnclientDisconnected;
        public event GenericEventHandler<T> OnClientMessageReceived;
        public event GenericEventHandler<string, bool> OnProviderDisconnected;

        protected virtual void ClientConnected(int clientId, string nickname)
        {
            if (!running)
                return;

            GenericEventHandler<int, string> handler = OnClientConnected;
            if (handler != null)
            {
                handler(this, clientId, nickname);
            }
        }

        protected virtual void ClientDisconnected(int clientId)
        {
            if (!running)
                return;

            GenericEventHandler<int> handler = OnclientDisconnected;
            if (handler != null)
            {
                handler(this, clientId);
            }
        }

        protected virtual void ClientMessageReceived(T clientMessage)
        {
            if (!running)
                return;

            GenericEventHandler<T> handler = OnClientMessageReceived;
            if (handler != null)
            {
                handler(this, clientMessage);
            }
        }

        protected virtual void ProviderDisconnected(string cause, bool error)
        {
            GenericEventHandler<string, bool> handler = OnProviderDisconnected;
            if (handler != null)
            {
                handler(this, cause, error);
            }
        }

        public abstract bool Start();

        public abstract void Stop();

        public abstract void Send(T heartRateMessage);

        public void ResetSubscriptions()
        {
            PortChanged = null;
            TimeoutChanged = null;
            VersionChanged = null;
            NicknameChanged = null;
            OnClientConnected = null;
            OnclientDisconnected = null;
            OnClientMessageReceived = null;
            OnProviderDisconnected = null;
        }

        public virtual void Init()
        {
            if (PortChanged != null)
                PortChanged(this, port);
            if (TimeoutChanged != null)
                TimeoutChanged(this, timeout);
            if (VersionChanged != null)
                VersionChanged(this, version);
            if (NicknameChanged != null)
                NicknameChanged(this, nickname);
        }
    }
}
