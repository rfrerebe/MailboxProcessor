using MailboxProcessor;

namespace MailBoxTestApp.Messages
{
    public class Message
    {
    }

    public class Reset : Message
    {
    }
    public class StartJob : Message
    {
        public StartJob(IReplyChannel<StartJobReply> channel, string workPath)
        {
            this.Channel = channel;
            this.WorkPath = workPath;
        }

        public string WorkPath { get; private set; }

        public IReplyChannel<StartJobReply> Channel { get; private set; }
    }

    public class StartJobReply
    {
        public StartJobReply(long jobTimeMilliseconds)
        {
            this.JobTimeMilliseconds = jobTimeMilliseconds;
        }

        public long JobTimeMilliseconds { get; private set; }
    }

    public class AddLineMessage : Message
    {
        public AddLineMessage(string line)
        {
            this.Line = line;
        }

        public string Line { get; private set; }
    }

    public class AddMultyLineReply
    {
        public AddMultyLineReply(string[] lines)
        {
            this.Lines = lines ?? new string[0];
        }

        public string[] Lines { get; private set; }
    }

    public class AddMultyLine : Message
    {
        public AddMultyLine(IReplyChannel<AddMultyLineReply> channel, string line)
        {
            this.Channel = channel;
            this.Line = line;
        }

        public string Line { get; private set; }

        public IReplyChannel<AddMultyLineReply> Channel { get; private set; }
    }

    public class AddLineAndReply : Message
    {
        public AddLineAndReply(IReplyChannel<int?> channel, string line)
        {
            this.Channel = channel;
            this.Line = line;
        }

        public IReplyChannel<int?> Channel { get; private set; }

        public string Line { get; private set; }
    }
}
