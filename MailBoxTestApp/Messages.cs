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

    public class AddMultyLineMessage : Message
    {
        public AddMultyLineMessage(string line, Agent<Message> nextAgent)
        {
            this.Line = line;
            this.NextAgent = nextAgent;
        }

        public string Line { get; private set; }

        public Agent<Message> NextAgent { get; private set; }
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
