using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MGT.Utilities.Network
{
    [Serializable]
    struct TcpRelayGreeting
    {
        public int ClientId;
        public string Nickname;
    }
}
