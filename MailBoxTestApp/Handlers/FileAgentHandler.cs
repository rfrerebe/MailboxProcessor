using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp.Handlers
{
    internal class FileAgentHandler : IMessageHandler<Message>
    {
        private readonly string filePath;
        private readonly string workDir;

        private FileStream fileStream;
        private StreamWriter streamWriter;
        private int n;

        public FileAgentHandler(string filePath)
        {
            this.filePath = filePath;
            this.workDir = Path.GetDirectoryName(filePath);
        }

        void IMessageHandler<Message>.OnStart()
        {
            if (!Directory.Exists(workDir))
            {
                Directory.CreateDirectory(workDir);
            }

            const int BUFFER_SIZE = 32 * 1024;

            this.fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, BUFFER_SIZE, false);
            this.streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8, BUFFER_SIZE, true);
            this.n = 0;
        }

        public Task Handle(Message msg, CancellationToken token)
        {
            return this.OnMessage((dynamic)msg, token);
        }

        void IMessageHandler<Message>.OnEnd()
        {
            this.streamWriter.Dispose();
            this.fileStream.Dispose();
        }

        protected virtual Task OnMessage(Reset msg, CancellationToken token)
        {
            n = 0;
            return Task.CompletedTask;
        }

        protected virtual Task OnMessage(AddLineMessage msg, CancellationToken token)
        {
            streamWriter.WriteLine($"{n + 1}) {msg.Line}");
            ++n;
            return Task.CompletedTask;
        }

        protected virtual Task OnMessage(AddLineAndReply msg, CancellationToken token)
        {
            streamWriter.WriteLine($"{n + 1}) {msg.Line}");
            ++n;
            var chan = msg.Channel;
            chan.ReplyResult(n);
            return Task.CompletedTask;
        }

        protected virtual Task OnMessage(AddMultyLine msg, CancellationToken token)
        {
            var chan = msg.Channel;
            try
            {
                streamWriter.WriteLine($"{n + 1}) {msg.Line}");
                ++n;
                List<string> list = new List<string>();
                for (int k = 0; k < 10; ++k)
                {
                    string line4 = $"Line4 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                    list.Add(line4);
                }
                chan.ReplyResult(new AddMultyLineReply(list.ToArray()));
            }
            catch (Exception ex)
            {
                chan.ReplyError(ex);
            }
            return Task.CompletedTask;
        }
    }
}
