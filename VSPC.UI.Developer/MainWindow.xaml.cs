using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VSPC.Core;
using VSPC.Core.Messages.FSD;
using VSPC.Core.MessageHandlers;
using VSPC.Core.Messages;
using System.Collections.ObjectModel;

namespace VSPC.UI.Developer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IVSPCMessageHandler
    {
        public bool ScrollToNewestLog { get; set; }

        public class LogMessage
        {
            public string Level { get; set; }
            public string Message { get; set; }
        }

        MessageBroker broker;
        public ObservableCollection<AFSDMessage> FSDMessages { get; set; }
        public ObservableCollection<LogMessage> Logs { get; set; }

        public MainWindow(MessageBroker broker)
        {
            this.broker = broker;
            broker.SubscribeSubclasses(this, typeof(AFSDMessage));
            FSDMessages = new ObservableCollection<AFSDMessage>();
            Logs = new ObservableCollection<LogMessage>();
            InitializeComponent();
        }

        public void HandleMessage(Core.Messages.AMessage message, VSPCContext context)
        {
            DoInUIThread(() => FSDMessages.Add((AFSDMessage)message));
        }

        public void DoInUIThread(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.BeginInvoke(action);
            }
        }

        public void Log(string level, string message)
        {
            var log = new LogMessage() { Level = level, Message = message}; 
            DoInUIThread(() => { Logs.Add(log); if(ScrollToNewestLog) listviewLogs.ScrollIntoView(log); });
        }
    }
}
