using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core;
using Metacraft.Vatsim.Network;

namespace VSPC.FSD
{
    public class FSDModule: IVSPCModule
    {
        readonly VSPCMessageHandler vspcMessageHandler = new VSPCMessageHandler();

        public void OnModuleLoad(MessageBroker broker)
        {
            vspcMessageHandler.Init(broker);
        }
    }
}
