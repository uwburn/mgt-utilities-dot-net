using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace MGT.Utilities.Network
{
    [Serializable]
    class TCPRelayClientInfo
    {
        public int ClientId;
        public string Nickname;
        public bool Connected;
    }
}
