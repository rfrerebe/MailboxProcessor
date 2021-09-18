namespace MailBoxTestApp.Messages
{
    public sealed record AddMultyLineReply
    {
        public AddMultyLineReply(string[] lines)
        {
            this.Lines = lines ?? new string[0];
        }

        public string[] Lines { get; }
    }
}
