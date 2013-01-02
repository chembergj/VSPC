using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core;
using VSPC.UI.WPF.Main;

namespace VSPC.UI.WPF
{
    public class WPFModule: IVSPCModule
    {
        MessageBroker messageBroker;

        public void OnModuleLoad(MessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            new MainWindow(messageBroker).Show();
        }
    }
}
