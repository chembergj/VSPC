using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core.Messages.FSD;

namespace VSPC.Core
{
    public class VSPCContext
    {
        public string Callsign { get; set; }
        public string CID { get; set; }
        public string Realname { get; set; }
        public bool FSDIsConnected { get; set; }
        public bool FlightsimIsConnected { get; set; }
    }
}
