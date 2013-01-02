namespace VSPC.Core.Messages.FSD
{
    public abstract class ATextMessage : AFSDMessage
    {
        public string Text { get; set; }

        public bool SenderIsServer { get { return !string.IsNullOrEmpty(Receiver) && Sender == "server"; } }

        public override string ToString()
        {
            return string.Format("TextMessage Sender: {0}, Receiver: {1}, Text: {2}", Sender, Receiver, Text);
        }
    }
}
