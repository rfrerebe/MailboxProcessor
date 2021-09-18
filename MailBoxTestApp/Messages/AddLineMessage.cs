namespace MailBoxTestApp.Messages
{
    public sealed record AddLineMessage : Message
    {
        public AddLineMessage(string line)
        {
            this.Line = line;
        }

        public string Line { get; }
    }
}
