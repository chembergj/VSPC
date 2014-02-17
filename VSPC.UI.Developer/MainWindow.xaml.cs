using System;
using System.Windows;
using VSPC.Core;
using VSPC.Core.Messages.FSD;
using VSPC.Core.MessageHandlers;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;
using VSPC.Core.Messages;
using System.Threading;

namespace VSPC.UI.Developer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IVSPCMessageHandler
    {
        public bool ScrollToNewestLog { get; set; }
        TrafficBotState trafficBotState = new TrafficBotState();
        bool isSavingPosRepMessages = false;
        StreamWriter saveFile;

        public class LogMessage
        {
            public string Level { get; set; }
            public string Message { get; set; }
        }

        public class TrafficBotState
        {
            public List<TrafficPositionReportMessage> TrafficBotMessages { get; set; }
            public IEnumerator<TrafficPositionReportMessage> MessageEnumerator { get; set; }
            public Timer TimerReference;
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
            if (message is TrafficPositionReportMessage && isSavingPosRepMessages)
            {
                var tp = (TrafficPositionReportMessage)message;
                saveFile.WriteLine(string.Format("{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}",
                    tp.Latitude,
                    tp.Longitude,
                    tp.TrueAltitude,
                    tp.PressureAltitude,
                    tp.Groundspeed,
                    tp.Heading,
                    tp.BankAngle,
                    tp.Pitch));

            }
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

        private void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            StreamReader file = new System.IO.StreamReader(textBoxFilename.Text);
            trafficBotState.TrafficBotMessages = new List<TrafficPositionReportMessage>();
            string line;
            while((line = file.ReadLine()) != null)
            {
                var data = line.Split(':');
                trafficBotState.TrafficBotMessages.Add(new TrafficPositionReportMessage()
                {
                    Sender = "ROBOT1",
                    Latitude = double.Parse(data[0]),
                    Longitude = double.Parse(data[1]),
                    TrueAltitude = double.Parse(data[2]),
                    PressureAltitude = double.Parse(data[3]),
                    Groundspeed = double.Parse(data[4]),
                    Heading = double.Parse(data[5]),
                    BankAngle = double.Parse(data[6]),
                    Pitch = int.Parse(data[7])
                });
            }

            file.Close();

            buttonPlay.Content = "Playing...";
            trafficBotState.MessageEnumerator = trafficBotState.TrafficBotMessages.GetEnumerator();
            var timerDelegate = new System.Threading.TimerCallback(TimerTask);
            var timerItem = new System.Threading.Timer(timerDelegate, trafficBotState, 2000, 5000);
            trafficBotState.TimerReference = timerItem;
        }

        private void TimerTask(object StateObj)
        {
            var state = (TrafficBotState)StateObj;
            if (state.MessageEnumerator.MoveNext())
            {
                var msg = state.MessageEnumerator.Current;
                broker.Publish(msg);
            } 
            else
            {
                state.TimerReference.Dispose();
                DoInUIThread(() => buttonPlay.Content = "Play");
            }
        }

        private void buttonSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            Nullable<bool> result = openDialog.ShowDialog();
            if (result == true)
            {
                textBoxFilename.Text = openDialog.FileName;
            }
        }

        private void buttonRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!isSavingPosRepMessages)
            {
                saveFile = new System.IO.StreamWriter(textBoxFilename.Text);
                isSavingPosRepMessages = true;
                buttonRecord.Content = "Saving..";
            }
            else
            {
                isSavingPosRepMessages = false;
                buttonRecord.Content = "Record";
                saveFile.Close();
                saveFile = null;
            }
        }

    }
}
