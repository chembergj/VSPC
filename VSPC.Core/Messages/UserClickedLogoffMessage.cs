using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    public class UserClickedLogoffMessage: AVSPCMessage
    {
        public string Callsign { get; set; }
    }
}
