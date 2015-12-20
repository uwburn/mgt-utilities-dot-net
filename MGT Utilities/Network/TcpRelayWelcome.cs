using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MGT.Utilities.Network
{
    [Serializable]
    struct TcpRelayWelcome
    {
        public int ClientId;
        public string Version;
        public bool ServerFull;
    }
}
