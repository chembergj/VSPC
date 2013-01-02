using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core;
using VSPC.Core.MessageHandlers;
using VSPC.Core.Messages;
using VSPC.Core.Messages.FSD;

namespace VSPC.UI.WPF.Main
{
    public class TextMessageHandler : IVSPCMessageHandler
    {
        private MainWindow mainWindow;

        public TextMessageHandler(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }

        public void HandleMessage(AMessage message, VSPCContext context)
        {
            var textMessage = (ATextMessage)message;
            bool messageShown = false;

            mainWindow.DoInUIThread(() =>
                {
                    foreach (var ccTab in mainWindow.CommChannelTabs.Where(t => !t.IsAllTab))
                    {
                        if (ccTab.MessageBelongsToMe(textMessage, context.Callsign))
                        {
                            messageShown |= (!ccTab.IsAllTab || textMessage.SenderIsServer); // Only mark message as shown if the message is shown in a tab other than "All" with server messages as exception
                            ccTab.ShowMessage(textMessage);
                        }
                    }
                    if (!messageShown && NewTabShouldBeOpened(textMessage, context))
                    {
                        CommChannelTab newCommTab = mainWindow.AddNewCommTab(textMessage.Sender);
                        newCommTab.ShowMessage(textMessage);
                        messageShown = true;
                    }

                    var allTab = mainWindow.CommChannelTabs.Single(t => t.IsAllTab);
                    if (messageShown || textMessage.SenderIsServer)
                        allTab.ShowMessage(textMessage);
                });
        }

        private bool NewTabShouldBeOpened(ATextMessage textMessage, VSPCContext context)
        {
            return !textMessage.SenderIsServer && textMessage.Receiver == context.Callsign;
        }
    }
}


