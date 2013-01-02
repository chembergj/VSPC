using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    public class ChatMessage: AVSPCMessage
    {
        public string Text { get; set; }
    }
}
