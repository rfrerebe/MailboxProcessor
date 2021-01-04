using MailboxProcessor;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public static class AgentFactory
    {
        public static Agent<Message> GetAgent(string filePath, CancellationToken? token = null, int? capacity = null)
        {
            var agent = new Agent<Message>(async inbox =>
            {
                int n = 0;

                using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4 * 1024, false);
                using var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8, 4096, true);
                
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"Starting MailboxProcessor Thread: {threadId} File: {filePath}");

                while (!inbox.CancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var msg = await inbox.Receive();

                        if (msg is AddLineMessage addLineMessage)
                        {
                            streamWriter.WriteLine(addLineMessage.Line);
                            ++n;
                        }
                        else if (msg is Reset)
                        {
                            n = 0;
                        }
                        else if (msg is AddLineAndReplyMessage addLineandReplyMessage)
                        {
                            streamWriter.WriteLine(addLineandReplyMessage.Line);
                            ++n;
                            var chan = addLineandReplyMessage.Channel;
                            chan.Reply(n);
                        }
                        else if (msg is AddMultyLineMessage addMultyLineMessage)
                        {
                            streamWriter.WriteLine(addMultyLineMessage.Line);
                            ++n;
                            for (int k = 0; k < 5; ++k)
                            {
                                string line4 = $"{n}-{k}) {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                                await addMultyLineMessage.NextAgent.Post(new AddLineMessage(line4));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        inbox.ReportError(ex);
                    }
                }

                streamWriter.Flush();

                Console.WriteLine($"Exiting MailboxProcessor Thread: {threadId} File: {filePath}");
            }, token, capacity);

            agent.Start();

            return agent;
        }
    }
}
