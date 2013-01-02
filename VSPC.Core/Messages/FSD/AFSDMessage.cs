using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages.FSD
{
    /// <summary>
    /// Broker messages that origins from an FSD request or reply
    /// </summary>
    public abstract class AFSDMessage: AMessage
    {
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public virtual string Content { get { return ToString(); } }
    }
}
