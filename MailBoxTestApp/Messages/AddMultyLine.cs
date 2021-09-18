using MailboxProcessor;

namespace MailBoxTestApp.Messages
{
    public sealed record AddMultyLine : Message
    {
        public AddMultyLine(IReplyChannel<AddMultyLineReply> channel, string line)
        {
            this.Channel = channel;
            this.Line = line;
        }

        public string Line { get;  }

        public IReplyChannel<AddMultyLineReply> Channel { get; }
    }
}
