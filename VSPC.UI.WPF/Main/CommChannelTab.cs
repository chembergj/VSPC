using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Threading;

namespace VSPC.UI.WPF.Main
{
    public class CommChannelTab
    {
        string channelFreq;

        public TabItem TabItem { get; set; }
		public ListBox ListBox { get; set; }
        public string ChannelName { get; set; }
        public bool IsPrivateChat { get; set; }

        public string DisplayChannelFreq
        {
            get { return channelFreq.StartsWith("@") ? string.Format("1{0}.{1}", channelFreq.Substring(1, 2), channelFreq.Substring(3, 2)) : channelFreq; }
        }

        public string ChannelFreq 
        {
            get { return channelFreq; }
            set
            {
                channelFreq = value;
                TabItem.Header = string.IsNullOrEmpty(ChannelName) ? DisplayChannelFreq.Replace("_", "__") : string.Format("{0}: {1}", ChannelName, DisplayChannelFreq);
            }
        }

        public bool IsAllTab 
        {
            get { return ChannelFreq == "*"; }
        }

		internal bool MessageBelongsToMe(Core.Messages.FSD.ATextMessage textMessage, string pilotCallsign)
		{
            return IsAllTab 
                || (!IsPrivateChat && textMessage.Receiver == ChannelFreq)                              // Not a private chat, freq. matches
                || IsPrivateChat &&                                                                     // Is a private chat, and...
                    ((textMessage.Sender != pilotCallsign &&  textMessage.Sender == ChannelFreq)        // either the sender is NOT me and the sender matches the freq
                    || (textMessage.Sender == pilotCallsign &&  textMessage.Receiver == ChannelFreq))   // or the sender IS me, and the receiver matches the freq
                ;
		}

		internal void ShowMessage(Core.Messages.FSD.ATextMessage textMessage)
		{
			Action action = () => ListBox.Items.Add(new ListBoxItem() { Content = string.Format("{0}> {1}", textMessage.Sender, textMessage.Text) });
			if (ListBox.Dispatcher.CheckAccess())
			{
				action();
			}
			else
			{
				ListBox.Dispatcher.BeginInvoke(action);
			}
		}
	}
}
