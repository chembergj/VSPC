using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core.Messages;

namespace VSPC.Core.MessageHandlers
{
    public interface IVSPCMessageHandler
    {
        void HandleMessage(AMessage message, VSPCContext context);
    }
}
