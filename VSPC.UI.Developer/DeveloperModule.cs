using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core;

namespace VSPC.UI.Developer
{
    public class DeveloperModule : IVSPCModule, IVSPCLogConsumer
    {
        MessageBroker messageBroker;
        MainWindow window;

        public void OnModuleLoad(MessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
            window = new MainWindow(messageBroker);
            window.Show();
        }

        public void Log(string level, string message)
        {
            if (window != null)
                window.Log(level, message);
        }
    }
}
