using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VSPC.Core;
using VSPC.Core.Messages.FSD;
using VSPC.SimInterface;
using VSPC.Core.Messages;
using System.Threading;
using VSPC.Core.MessageHandlers;
using NLog;
using VSPC.FSD;
using VSPC.Core.Messages.UI;

namespace VSPC.TestUI
{
    public partial class Form1 : Form, IVSPCMessageHandler
    {
        MessageBroker broker = new MessageBroker();
        SimConnectInterface sci = new SimConnectInterface();
        VSPCMessageHandler vspcMessageHandler = new VSPCMessageHandler();
        FSDInterface fsd;
		private static Logger logger;


        public Form1()
        {
            InitializeComponent();
            
        }

        public void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            textBoxMessages.Text += e.ToString();
        }

        private void buttonSimLogin_Click(object sender, EventArgs e)
        {
            try
            {
                broker.Subscribe(this, typeof(PositionReportMessage));
                sci.Init(broker);
                broker.Publish(new LoggedInMessage());
            }
            catch (Exception ex)
            {
            }
        }

        private void buttonSimLogoff_Click(object sender, EventArgs e)
        {
            try
            {
                broker.Publish(new LoggedOffMessage());
            }
            catch (Exception ex)
            {
            }
        }

        protected override void DefWndProc(ref Message m)
        {
            if (!sci.GetWindowsMsg(m))
                base.DefWndProc(ref m);
        }

        public delegate void InvokeDelegate();

        public void HandleMessage(AMessage message, VSPCContext context)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new InvokeDelegate(() => HandleMessage(message, context)));
            }
            else
            {
                textBoxMessages.Text += message.ToString() + Environment.NewLine;
            }
        }

		private void Form1_Load(object sender, EventArgs e)
		{
			logger = LogManager.GetCurrentClassLogger();
		}

        private void buttonFSDLogin_Click(object sender, EventArgs e)
        {
            try
            {
                fsd = new FSDInterface(6809);
                var commHandler = new FSDCommHandlers(broker, fsd);
                vspcMessageHandler.Init(broker, commHandler);
                fsd.Init(commHandler);
                broker.Subscribe(this, typeof(ATextMessage));
                broker.Publish(new UserClickedLoginMessage() { Callsign = "OY-CHE", Cid = "222222", Password = "password", Realname = "VSPC.TestUI", Server = "localhost" });
            }
            catch (Exception ex)
            {
            }
        }

        private void buttonFSDLogoff_Click(object sender, EventArgs e)
        {
            try
            {
                broker.Publish(new UserClickedLogoffMessage());
            }
            catch (Exception ex)
            {
            }
        }
    }
}
