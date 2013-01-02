using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core.Messages;

namespace VSPC.Core.MessageHandlers
{
    public abstract class AVSPCMessageHandler: IVSPCMessageHandler
    {
        protected MessageBroker broker;

        public virtual void Init(MessageBroker broker)
        {
            this.broker = broker;
        }

        public abstract void HandleMessage(AMessage message, VSPCContext context);
    }
}
