using MailboxProcessor;

namespace MailBoxTestApp.Messages
{
    public sealed record StartJob : Message
    {
        public StartJob(IReplyChannel<StartJobReply> channel, string workPath)
        {
            this.Channel = channel;
            this.WorkPath = workPath;
        }

        public string WorkPath { get; }

        public IReplyChannel<StartJobReply> Channel { get; }
    }
}
