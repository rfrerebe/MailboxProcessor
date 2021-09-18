using MailboxProcessor;

namespace MailBoxTestApp.Messages
{
    public sealed record AddLineAndReply : Message
    {
        public AddLineAndReply(IReplyChannel<int?> channel, string line)
        {
            this.Channel = channel;
            this.Line = line;
        }

        public IReplyChannel<int?> Channel { get; }

        public string Line { get; }
    }
}
