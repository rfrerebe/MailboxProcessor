using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public class AgentDictionary<Message>
    {
        private readonly string[] _agentNames;
        private ConcurrentDictionary<string, Lazy<TaskCompletionSource<Agent<Message>>>> _agents = new ConcurrentDictionary<string, Lazy<TaskCompletionSource<Agent<Message>>>>();

        public string[] AgentNames => _agentNames;

        public AgentDictionary()
            : this(new string[] { "testMailbox1", "testMailbox2", "testMailbox3", "testMailbox4" })
        {

        }
        public AgentDictionary(string[] agentNames)
        {
            this._agentNames = agentNames;

            foreach(var agentName in _agentNames)
            {
               var lazyAgentVal = _agents.GetOrAdd(agentName, (_name) => new Lazy<TaskCompletionSource<Agent<Message>>>(() => new TaskCompletionSource<Agent<Message>>(TaskCreationOptions.RunContinuationsAsynchronously), true));
            }
        }

        private TaskCompletionSource<Agent<Message>> _GetAgent(string name)
        {
            if (_agents.TryGetValue(name, out var lazyVal))
            {
                return lazyVal.Value;
            }

            throw new Exception($"Agent {name} is not found in the dictionary");
        }

        public void AddAgent(string name, Agent<Message> agent)
        {
            var tcs = this._GetAgent(name);
            tcs.TrySetResult(agent);
        }
        
        public bool RemoveAgent(string name)
        {
            if (_agents.TryRemove(name, out var lazyVal))
            {
                Console.WriteLine($"Agent: {name} removed from dictionary");
                lazyVal.Value.TrySetCanceled();
                return true;
            }
            return false;
        }

        public async Task<Agent<Message>> GetAgent(string name)
        {
            return await this._GetAgent(name).Task;
        }

        public void Clear()
        {
            _agents.Keys.ToList().ForEach((name)=> this.RemoveAgent(name));
        }
    }

    public static class AgentFactory
    {
        /*
        private static AgentDictionary<Message> _agents = new AgentDictionary<Message>();
        */

        #region Doing Real Work here
        private static async Task RunJob(Agent<Message> agent1, Agent<Message> agent2, Agent<Message> agent3, Agent<Message> agent4)
        {
            const int numberOfLines = 25000;
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
                        var lineNumber = await agent1.PostAndReply<int?>(channel => new AddLineAndReply(channel, line));

                        for (int j = 0; j < 5; ++j)
                        {
                            string line2 = $"Line2 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            // wait for reply
                            var reply = await agent2.PostAndReply<AddMultyLineReply>(channel => new AddMultyLine(channel, line: line2));

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

        private static async Task ProcessFile(Func<bool> isRunning, Func<Task<Message>> receive, StreamWriter streamWriter)
        {
            int n = 0;

            while (isRunning())
            {
                var msg = await receive();

                if (msg is AddLineMessage addLineMessage)
                {
                    streamWriter.WriteLine($"{n+1}) {addLineMessage.Line}");
                    ++n;
                }
                else if (msg is Reset)
                {
                    n = 0;
                }
                else if (msg is AddLineAndReply addLineandReplyMessage)
                {
                    streamWriter.WriteLine($"{n+1}) {addLineandReplyMessage.Line}");
                    ++n;
                    var chan = addLineandReplyMessage.Channel;
                    chan.Reply(n);
                }
                else if (msg is AddMultyLine addMultyLineMessage)
                {
                    streamWriter.WriteLine($"{n+1}) {addMultyLineMessage.Line}");
                    ++n;
                    List<string> list = new List<string>();
                    for (int k = 0; k < 10; ++k)
                    {
                        string line4 = $"Line4 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                        list.Add(line4);
                    }
                    var chan = addMultyLineMessage.Channel;
                    chan.Reply(new AddMultyLineReply(list.ToArray()));
                }
            }
        }

        private static async Task CoordinateWork(Func<bool> isRunning, Func<Task<Message>> receive, CancellationToken token)
        {
            while (isRunning())
            {
                var msg = await receive();

                if (msg is StartJob startJob)
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    string workPath = startJob.WorkPath;
                    AgentOptions agentOptions = new AgentOptions() { CancellationToken = token, QueueCapacity = 100 };

                    using (var agent1 = AgentFactory.GetFileAgent(filePath: Path.Combine(workPath, "testMailbox1.txt"), agentOptions))
                    using (var agent2 = AgentFactory.GetFileAgent(filePath: Path.Combine(workPath, "testMailbox2.txt"), agentOptions))
                    using (var agent3 = AgentFactory.GetFileAgent(filePath: Path.Combine(workPath, "testMailbox3.txt"), agentOptions))
                    using (var agent4 = AgentFactory.GetFileAgent(filePath: Path.Combine(workPath, "testMailbox4.txt"), agentOptions))
                    {
                        await RunJob(agent1, agent2, agent3, agent4);

                        // allows to wait till all messages processed
                        Task[] stopTasks = new Task[] { agent1.Stop(), agent2.Stop(), agent3.Stop(), agent4.Stop() };
                        await Task.WhenAll(stopTasks);
                    }


                    sw.Stop();

                    var chan = startJob.Channel;
                    chan.Reply(new StartJobReply(sw.ElapsedMilliseconds));
                }
            }
        }
        #endregion

        public static Agent<Message> GetFileAgent(string filePath, AgentOptions agentOptions= null)
        {
            string thisAgentName = Path.GetFileNameWithoutExtension(filePath);

            var agent = new Agent<Message>(async inbox =>
            {
                using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4 * 1024, false);
                using var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8, 4096, true);
                
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"Starting MailboxProcessor Thread: {threadId} File: {filePath}");

                try
                {
                    await ProcessFile(()=> inbox.IsRunning, ()=> inbox.Receive(), streamWriter);
                }
                catch (OperationCanceledException)
                {
                    // NOOP
                }


                streamWriter.Flush();

                Console.WriteLine($"Exiting MailboxProcessor Thread: {threadId} File: {filePath}");
            }, agentOptions);

            // agent.AgentStarting += (s, a) => { _agents.AddAgent(thisAgentName, agent); };
            agent.Start();
            // agent.AgentStopped += (s,a) => { _agents.RemoveAgent(thisAgentName); };

            return agent;
        }

        public static Agent<Message> GetCoordinatorAgent(AgentOptions agentOptions = null)
        {
            var agent = new Agent<Message>(async inbox =>
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"Starting Coordinator Agent Thread: {threadId}");

                try
                {
                    await CoordinateWork(() => inbox.IsRunning, () => inbox.Receive(), inbox.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // NOOP
                }

                Console.WriteLine($"Exiting Coordinator Agent Thread: {threadId}");
            }, agentOptions);

            agent.Start();

            return agent;
        }
    }
}
