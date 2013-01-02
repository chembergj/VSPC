using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages.UI
{
    public class UserClickedLoginMessage: AMessage
    {
        public string Callsign { get; set; }
        public string Cid { get; set; }
        public string Password { get; set; }
        public string Realname { get; set; }
        public string Server { get; set; }
    }
}
