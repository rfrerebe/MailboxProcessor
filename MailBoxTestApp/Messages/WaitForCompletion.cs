using MailboxProcessor;

namespace MailBoxTestApp.Messages
{
    /// <summary>
    /// the last message in the bactch
    /// when replied (used with the Ask method), it means all the previous posted messages are processed
    /// </summary>
    public sealed record WaitForCompletion : Message
    {
        public WaitForCompletion(IReplyChannel<string> channel)
        {
            this.Channel = channel;
        }

        public IReplyChannel<string> Channel { get; }
    }
}
