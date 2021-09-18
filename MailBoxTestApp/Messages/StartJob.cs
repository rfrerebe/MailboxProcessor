using MailboxProcessor;

namespace MailBoxTestApp.Messages
{
    public sealed record StartJob : Message
    {
        public string WorkPath { get; init; }

        public IReplyChannel<StartJobReply> Channel { get; init; }

        public IAgent<Message> countAgent { get; init; }
    }
}
