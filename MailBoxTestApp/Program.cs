using MailboxProcessor;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            // cts.CancelAfter(100);

            async Task RunAgent(Agent<Message> agent1, Agent<Message> agent2, Agent<Message> agent3, Agent<Message> agent4)
            {
                try
                {
                    for (int i = 0; i < 50000; ++i)
                    {
                        string line = string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25));
                        var result = await agent1.PostAndReply<int?>(channel => new AddLineAndReplyMessage(channel, line));

                        for (int j = 0; j < 10; ++j)
                        {
                            string line2 = $"{result}) {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            await agent2.Post(new AddLineMessage(line2));
                        }

                        for (int j = 0; j < 5; ++j)
                        {
                            string line3 = $"{result}) {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            var result3 = await agent3.PostAndReply<int?>(channel => new AddLineAndReplyMessage(channel, line3));

                            for (int k = 0; k < 5; ++k)
                            {
                                string line4 = $"{result}-{result3}) {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                                await agent4.Post(new AddLineMessage(line4));
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Agent Work is Cancelled");
                }
            };

            using (var agent1 = GetAgent(filePath: @"c:\TEMP\testMailbox1.txt", token: cts.Token, capacity: 100))
            using (var agent2 = GetAgent(filePath: @"c:\TEMP\testMailbox2.txt", token: cts.Token, capacity: 100))
            using (var agent3 = GetAgent(filePath: @"c:\TEMP\testMailbox3.txt", token: cts.Token, capacity: 100))
            using (var agent4 = GetAgent(filePath: @"c:\TEMP\testMailbox4.txt", token: cts.Token, capacity: 100))
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                await RunAgent(agent1,agent2, agent3, agent4);

                sw.Stop();

                Console.WriteLine($"Job took: {sw.ElapsedMilliseconds} milliseconds");
            }

            await Task.Yield();

            Console.WriteLine("Press Any Key to Exit ....");
            Console.ReadKey();
        }

        private class Message
        {
        }

        private class Reset : Message
        {
        }

        private class AddLineMessage : Message
        {
            public AddLineMessage(string line)
            {
                this.Line = line;
            }

            public string Line { get; private set; }
        }

        private class AddLineAndReplyMessage : Message
        {
            public AddLineAndReplyMessage(IReplyChannel<int?> channel, string line)
            {
                this.Channel = channel;
                this.Line = line;
            }

            public IReplyChannel<int?> Channel { get; private set; }

            public string Line { get; private set; }
        }

        private  static Agent<Message> GetAgent(string filePath, CancellationToken? token = null, int? capacity = null)
        {
            var agent = new Agent<Message>(async inbox =>
            {
                int n = 0;

                using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4 * 1024, false);
                using var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8, 4096, true);

                Console.WriteLine("Starting MailboxProcessor");

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
                    }
                    catch(Exception ex)
                    {
                        inbox.ReportError(ex);
                    }
                }

                streamWriter.Flush();

                Console.WriteLine("Exiting MailboxProcessor");
            }, token, capacity);

            agent.Start();

            return agent;
        }
    }
}
