using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core;
using System.Windows;
using System.Windows.Interop;

namespace VSPC.SimInterface
{
    public class SimInterfaceModule: IVSPCModule
    {
        SimConnectInterface sci = new SimConnectInterface();

        public void OnModuleLoad(MessageBroker broker)
        {
            sci.Init(broker);
        }
    }
}
