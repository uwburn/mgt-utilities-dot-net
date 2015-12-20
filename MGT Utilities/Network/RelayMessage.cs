using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MGT.Utilities.Network
{
    [Serializable]
    public class RelayMessage
    {
        private int clientId;

        public int ClientId { get { return clientId; } set { clientId = value; } }
    }
}
