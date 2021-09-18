namespace MailBoxTestApp.Messages
{
    public sealed record StartJobReply
    {
        public StartJobReply(long jobTimeMilliseconds)
        {
            this.JobTimeMilliseconds = jobTimeMilliseconds;
        }

        public long JobTimeMilliseconds { get; }
    }
}
