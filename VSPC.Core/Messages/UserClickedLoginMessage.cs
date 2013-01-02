using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    public class UserClickedLoginMessage: AVSPCMessage
    {
        public string Callsign { get; set; }
        public string Cid { get; set; }
        public string Password { get; set; }
        public string Realname { get; set; }
    }
}
