using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public static class AgentFactory
    {
        #region Doing Real Work here
        private static async Task RunJob(IAgentWriter<Message> agent1, IAgentWriter<Message> agent2, IAgentWriter<Message> agent3, IAgentWriter<Message> agent4)
        {
            const int numberOfLines = 1000;
            try
            {
                // task #1 which uses 1 agent to post lines
                Func<Task> taskPart1 = async () =>
                {
                    for (int i = 0; i < numberOfLines; ++i)
                    {
                        for (int j = 0; j < 5; ++j)
                        {
                            string line3 = $"Line3 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            await agent3.Post(new AddLineMessage(line3));
                        }
                    }
                };

                // task #2 which uses several agents to post lines and get replies
                Func<Task> taskPart2 = async () =>
                {
                    for (int i = 0; i < numberOfLines; ++i)
                    {
                        string line = $"Line1 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                        var lineNumber = await agent1.Ask<int?>(channel => new AddLineAndReply(channel, line));

                        for (int j = 0; j < 5; ++j)
                        {
                            string line2 = $"Line2 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            // wait for reply
                            var reply = await agent2.Ask<AddMultyLineReply>(channel => new AddMultyLine(channel, line: line2));

                            // send all lines in reply to agent4
                            foreach (var line4 in reply.Lines)
                            {
                                await agent4.Post(new AddLineMessage(line4));
                            }
                        }
                    }
                };

                // several tasks are run in parrallel
                await Task.WhenAll(Task.Run(taskPart1), Task.Run(taskPart1), Task.Run(taskPart1), Task.Run(taskPart2));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Agent Work is Cancelled");
            }
        }

        private class CoordinatorAgentHandler : IMessageHandler<Message>
        {
            public async Task Handle(Message msg, CancellationToken token)
            {
                if (msg is StartJob startJob)
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    try
                    {
                        string workPath = startJob.WorkPath;
                        AgentOptions<Message> agentOptions = new AgentOptions<Message>() { CancellationToken = token, BoundedCapacity = 100 };

                        using (var agent1 = CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox1.txt"), agentOptions))
                        using (var agent2 = CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox2.txt"), agentOptions))
                        using (var agent3 = CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox3.txt"), agentOptions))
                        using (var agent4 = CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox4.txt"), agentOptions))
                        {
                            await RunJob(agent1, agent2, agent3, agent4);

                            // allows to wait till all messages processed
                            Task[] stopTasks = new Task[] { agent1.Stop(), agent2.Stop(), agent3.Stop(), agent4.Stop() };
                            await Task.WhenAll(stopTasks);
                        }

                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        throw;
                    }

                    sw.Stop();

                    var chan = startJob.Channel;
                    chan.ReplyResult(new StartJobReply(sw.ElapsedMilliseconds));
                }
            }
        }

        private class FileAgentHandler : IMessageHandler<Message>, IDisposable
        {
            private readonly FileStream fileStream;
            private readonly StreamWriter streamWriter;
            private readonly string workDir;
            private int n;

            public FileAgentHandler(string filePath)
            {
                this.workDir = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(workDir))
                {
                    Directory.CreateDirectory(workDir);
                }

                const int BUFFER_SIZE = 32 * 1024;

                this.fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, BUFFER_SIZE, false);
                this.streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8, BUFFER_SIZE, true);
                this.n = 0;
            }

            public async Task Handle(Message msg, CancellationToken token)
            {
                if (msg is AddLineMessage addLineMessage)
                {
                    streamWriter.WriteLine($"{n + 1}) {addLineMessage.Line}");
                    ++n;
                }
                else if (msg is Reset)
                {
                    n = 0;
                }
                else if (msg is AddLineAndReply addLineandReplyMessage)
                {
                    streamWriter.WriteLine($"{n + 1}) {addLineandReplyMessage.Line}");
                    ++n;
                    var chan = addLineandReplyMessage.Channel;
                    chan.ReplyResult(n);
                }
                else if (msg is AddMultyLine addMultyLineMessage)
                {
                    var chan = addMultyLineMessage.Channel;
                    try
                    {
                        streamWriter.WriteLine($"{n + 1}) {addMultyLineMessage.Line}");
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
                }
            }

            public void Dispose()
            {
                this.streamWriter.Dispose();
                this.fileStream.Dispose();
            }
        }

        #endregion

        /// <summary>
        /// Scan (inspect) message before its processing
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static Task<ScanResults> ScanMessage(Message msg)
        {
            if (msg is AddLineMessage addLineMessage)
            {
               // Console.WriteLine($"Scanned {addLineMessage.Line.Substring(0, 35)}");
                return Task.FromResult(ScanResults.None);
            }
            else if (msg is AddLineAndReply addLineandReplyMessage)
            {
                // var chan = addLineandReplyMessage.Channel;
                // chan.ReplyResult(0);
                // return Task.FromResult(ScanResults.Handled);
                return Task.FromResult(ScanResults.None);
            }
            else if (msg is AddMultyLine addMultyLineMessage)
            {
                // var chan = addMultyLineMessage.Channel;
                // chan.ReplyResult(new AddMultyLineReply(new string[0]));
                // return Task.FromResult(ScanResults.Handled);
                return Task.FromResult(ScanResults.None);
            }

            // if ScanResults.None then message is not handled and will be processed as usual
            return Task.FromResult(ScanResults.None);
        }

        public static IAgentWriter<Message> CreateFileAgent(string filePath, AgentOptions<Message> agentOptions= null)
        {
            agentOptions = agentOptions ?? AgentOptions<Message>.Default;
            agentOptions.scanFunction = ScanMessage;

            FileAgentHandler fileAgentHandler = new FileAgentHandler(filePath);

            var agent = new Agent<Message>(fileAgentHandler, agentOptions);

            agent.AgentStarting += (s, a) => {
                 int threadId = Thread.CurrentThread.ManagedThreadId;
                 Console.WriteLine($"Starting MailboxProcessor Thread: {threadId} File: {filePath}");
            };

            agent.AgentStopped += (s, a) => {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"Stopping MailboxProcessor Thread: {threadId} File: {filePath}");
            };

            agent.Start();

            return agent;
        }

        public static IAgentWriter<Message> CreateCoordinatorAgent(AgentOptions<Message> agentOptions = null)
        {
            var handler = new CoordinatorAgentHandler();
            var agent = new Agent<Message>(handler, agentOptions);

            agent.AgentStarting += (s, a) => {
                Console.WriteLine($"Starting Coordinator");
            };

            agent.AgentStopped += (s, a) => {
                Console.WriteLine($"Stopping Coordinator");
            };

            agent.Start();

            return agent;
        }
    }
}
