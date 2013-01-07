using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSPC.Core.Messages.UI;
using VSPC.Core;
using VSPC.Core.Messages.FLSIM;
using VSPC.Core.MessageHandlers;
using VSPC.Core.Messages.FSD;
using System.Threading;
using System.Linq;

namespace VSPC.UI.WPF.Main
{
    

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IVSPCMessageHandler
    {
        MessageBroker broker;
        ConnectionState FSDState = ConnectionState.Offline;
        ConnectionState FlightsimState = ConnectionState.Offline;
		Queue<MessageBoxMessage> msgBoxMessages = new Queue<MessageBoxMessage>();
    	private bool msgBoxOpen = false;
        readonly List<CommChannelTab> commChannelTabs = new List<CommChannelTab>();
		TextMessageHandler textMessageHandler;
        double preCollapseWindowHeight;
        double preCollapseExpanderHeight;
        

        public List<CommChannelTab> CommChannelTabs { get { return commChannelTabs; } }

        public MainWindow(MessageBroker broker)
        {
            this.broker = broker;
            broker.Subscribe(this, typeof(FlightsimConnectedMessage));
            broker.Subscribe(this, typeof(FSDConnectedMessage));
            broker.Subscribe(this, typeof(CommErrorMessage));
            broker.Subscribe(this, typeof(SimCommErrorMessage));
            broker.Subscribe(this, typeof(FSDDisconnectedMessage));
            broker.Subscribe(this, typeof(FlightsimDisconnectedMessage));

        	textMessageHandler = new TextMessageHandler(this);
        	broker.Subscribe(textMessageHandler, typeof (TextMessageSend));
			broker.Subscribe(textMessageHandler, typeof (TextMessageReceive));
            InitializeComponent();
        }

		/// <summary>
		/// Avoid several messagebox'es at the same time, byt queueing the messages with this method
		/// If no others dialogs are being shown, the message is just shown right away
		/// Since this method must always we called from the UI thread, no lock'ing is needed
		/// </summary>
		/// <param name="message"></param>
		private void QueueMessageBoxMessage(MessageBoxMessage message)
		{
			msgBoxMessages.Enqueue(message);

			if (!msgBoxOpen)
			{
				while (msgBoxMessages.Count > 0)
				{
					msgBoxOpen = true;
					var msg = msgBoxMessages.Dequeue();
					MessageBox.Show(msg.Message, "VSPC", msg.Button, msg.Image);
				}
				msgBoxOpen = false;
			}
		}

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            if (FSDState == ConnectionState.Offline && FlightsimState == ConnectionState.Offline)
                DoLogon();
            else if (FSDState == ConnectionState.Online && FlightsimState == ConnectionState.Online)
                DoLogoff();
        }

      

        private void DoLogon()
        {
            if (string.IsNullOrEmpty(comboBoxCallsign.Text))
            {
                MessageBox.Show("Please enter or select a callsign.", "VSPC Login", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                FSDState = ConnectionState.LogonInProgress;
                FlightsimState = ConnectionState.LogonInProgress;
                comboBoxCallsign.IsEnabled = false;
                buttonConnect.Content = "CONNECTING";
                buttonConnect.Foreground = Brushes.Orange;
                var msg = new UserClickedLoginMessage() { Callsign = comboBoxCallsign.Text, Cid = Properties.Settings.Default.CID, Password = Properties.Settings.Default.Password, Realname = Properties.Settings.Default.Realname, Server = Properties.Settings.Default.Server };
                ThreadStart start = () => broker.Publish(msg);
                new Thread(start).Start();
            }
        }

        private void DoLogoff()
        {
            broker.Publish(new UserClickedLogoffMessage());
        }

        private void buttonOptions_Click(object sender, RoutedEventArgs e)
        {
            var optionsWindow = new OptionsWindow();
            optionsWindow.ShowDialog();
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            preCollapseExpanderHeight = expander.ActualHeight;
            SizeToContent = System.Windows.SizeToContent.Height;
        }

        private void expander_Expanded(object sender, RoutedEventArgs e)
        {
            SizeToContent = System.Windows.SizeToContent.Manual;
            

            if(preCollapseWindowHeight != 0)
                Height = preCollapseWindowHeight;
            expander.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
        }

        public void HandleMessage(Core.Messages.AMessage message, VSPCContext context)
        {
            if (message is FlightsimConnectedMessage)
            {
                FlightsimState = ConnectionState.Online;
                if (FSDState == ConnectionState.Online) SwitchToOnlineMode();
            }
            else if (message is FSDConnectedMessage)
            {
                FSDState = ConnectionState.Online;
                if (FlightsimState == ConnectionState.Online) SwitchToOnlineMode();
            }
            else if (message is FlightsimDisconnectedMessage)
            {
                FlightsimState = ConnectionState.Offline;
                if (FSDState == ConnectionState.Offline) SwitchToOfflineMode();
            }
            else if (message is FSDDisconnectedMessage)
            {
                FSDState = ConnectionState.Offline;
                if (FlightsimState == ConnectionState.Offline) SwitchToOfflineMode();
            }
            else if (message is CommErrorMessage)
                HandleCommErrorMessage((CommErrorMessage)message);
            else if (message is SimCommErrorMessage)
                HandleCommErrorMessage((SimCommErrorMessage)message);
            else if (message is TextMessageReceive)
                HandleTextMessageReceive((TextMessageReceive)message);
        }

        private void HandleTextMessageReceive(TextMessageReceive textMessageReceive)
        {
            
        }

        private void SwitchToOfflineMode()
        {
            DoInUIThread((() =>
            {
                buttonConnect.Content = "CONNECT";
                buttonConnect.Foreground = Brushes.Black;
                comboBoxCallsign.IsEnabled = true;
            }));
        }

        private void SwitchToOnlineMode()
        {
            DoInUIThread((() =>
            {
                buttonConnect.Content = "CONNECTED";
                buttonConnect.Foreground = Brushes.Green;
            }));
        }

        private void HandleCommErrorMessage(SimCommErrorMessage commErrorMessage)
        {
            if (FlightsimState == ConnectionState.LogonInProgress)
                HandleErrorMessage("Unable to connect to Flightsim");
            else if (FlightsimState == ConnectionState.Online)
                HandleErrorMessage("Flightsim connection lost");
        }

        void HandleCommErrorMessage(CommErrorMessage commErrorMessage)
        {
            if (FSDState == ConnectionState.LogonInProgress)
                HandleErrorMessage(commErrorMessage.ErrorMessage);
            if (FSDState == ConnectionState.Online)
                HandleErrorMessage("Connection lost");
        }

        private void HandleErrorMessage(string errorMessage)
        {
            Action handleError = () => {
                comboBoxCallsign.IsReadOnly = false;
                buttonConnect.Content = "CONNECT";
                buttonConnect.Foreground = Brushes.Black;
				QueueMessageBoxMessage(new MessageBoxMessage() { Button = MessageBoxButton.OK, Image = MessageBoxImage.Error, Message = errorMessage});
            };

            DoInUIThread(handleError);
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            commChannelTabs.Add(new CommChannelTab() { TabItem = tabItemAll, ListBox = listboxAll, ChannelName = "All", ChannelFreq = "*", IsPrivateChat = false });
            commChannelTabs.Add(new CommChannelTab() { TabItem = tabItemComm1, ListBox = listboxComm1, ChannelName = "COMM1", ChannelFreq = "@19800", IsPrivateChat = false });
            commChannelTabs.Add(new CommChannelTab() { TabItem = tabItemComm2, ListBox = listboxComm2, ChannelName = "COMM2", ChannelFreq = "@35270", IsPrivateChat = false });
            commChannelTabs.Add(new CommChannelTab() { TabItem = tabItemUnicom, ListBox = listboxUnicom, ChannelName = "Unicom", ChannelFreq = "@22800", IsPrivateChat = false });
            preCollapseWindowHeight = Height;
            preCollapseExpanderHeight = expander.ActualHeight;
        }

		internal CommChannelTab AddNewCommTab(string receiver)
		{
			var newTabItem = new TabItem() { Header = receiver, Content = new ListBox() };
			tabControl1.Items.Add(newTabItem);
			var newCommChannelTab = new CommChannelTab()
			                        	{
			                        		TabItem = newTabItem,
			                        		ListBox = (ListBox) newTabItem.Content,
			                        		ChannelName = "",
			                        		ChannelFreq = receiver,
                                            IsPrivateChat = true
			                        	};
			commChannelTabs.Add(newCommChannelTab);

			return newCommChannelTab;
		}

        private void textBox1_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var activeCommChannelTab = commChannelTabs.FirstOrDefault(t => t.TabItem == tabControl1.SelectedItem);
                if (activeCommChannelTab != null && !activeCommChannelTab.IsAllTab)
                {
                    var msg = new TextMessageSend() { Sender = comboBoxCallsign.Text, Receiver = activeCommChannelTab.ChannelFreq, Text = textBox1.Text };
                    broker.Publish(msg);
                    textBox1.Clear();
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            preCollapseWindowHeight = Height;
        }
	}

    enum ConnectionState
    {
        Offline,
        LogonInProgress,
        Online
    };

    public class MessageBoxMessage
    {
        public MessageBoxButton Button { get; set; }
        public MessageBoxImage Image { get; set; }
        public string Message { get; set; }
    }
}
