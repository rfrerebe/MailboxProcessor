using MailboxProcessor;

namespace MailBoxTestApp
{
    public class Message
    {
    }

    public class Reset : Message
    {
    }

    public class AddLineMessage : Message
    {
        public AddLineMessage(string line)
        {
            this.Line = line;
        }

        public string Line { get; private set; }
    }

    public class AddMultyLineMessageReply
    {
        public AddMultyLineMessageReply(string[] lines)
        {
            this.Lines = lines ?? new string[0];
        }

        public string[] Lines { get; private set; }
    }

    public class AddMultyLineMessage : Message
    {
        public AddMultyLineMessage(IReplyChannel<AddMultyLineMessageReply> channel, string line)
        {
            this.Channel = channel;
            this.Line = line;
        }

        public string Line { get; private set; }

        public IReplyChannel<AddMultyLineMessageReply> Channel { get; private set; }
    }

    public class AddLineAndReplyMessage : Message
    {
        public AddLineAndReplyMessage(IReplyChannel<int?> channel, string line)
        {
            this.Channel = channel;
            this.Line = line;
        }

        public IReplyChannel<int?> Channel { get; private set; }

        public string Line { get; private set; }
    }
}
