using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    public class TextMessage: AMessage
    {
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return string.Format("TextMessage Sender: {0}, Receiver: {1}, Text: {2}", Sender, Receiver, Text);
        }
    }
}
