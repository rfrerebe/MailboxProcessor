using MailboxProcessor;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public class Agents<Message>
    {
        private ConcurrentDictionary<string, Lazy<TaskCompletionSource<Agent<Message>>>> _agents = new ConcurrentDictionary<string, Lazy<TaskCompletionSource<Agent<Message>>>>();
        private TaskCompletionSource<Agent<Message>> _GetAgent(string name)
        {
            return _agents.GetOrAdd(name, (_name) => new Lazy<TaskCompletionSource<Agent<Message>>>(() => new TaskCompletionSource<Agent<Message>>(TaskCreationOptions.RunContinuationsAsynchronously), true)).Value;
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

        public Task<Agent<Message>> GetAgent(string name)
        {
            if (_agents.TryGetValue(name, out var lazyVal))
            {
                return lazyVal.Value.Task;
            }

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            return Task.FromCanceled<Agent<Message>>(cts.Token);
        }

        public void Clear()
        {
            _agents.Keys.ToList().ForEach((name)=> this.RemoveAgent(name));
        }
    }

    public static class AgentFactory
    {
        private static Agents<Message> _agents = new Agents<Message>();

        public static Agent<Message> GetAgent(string filePath, CancellationToken? token = null, int? capacity = null)
        {
            string thisAgentName = Path.GetFileNameWithoutExtension(filePath);
            string[] allAgentNames = new string[] { "testMailbox1", "testMailbox2", "testMailbox3", "testMailbox4" };

            var agent = new Agent<Message>(async inbox =>
            {
                int n = 0;

                using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4 * 1024, false);
                using var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8, 4096, true);
                
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"Starting MailboxProcessor Thread: {threadId} File: {filePath}");

                while (inbox.IsRunning)
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
                                string line4 = $"{n}-{k}) Line4 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                                
                                var nextAgent = await _agents.GetAgent("testMailbox4");
                                await nextAgent?.Post(new AddLineMessage(line4));
                            }


                            int agentNum = n % 4;
                            string randomName = allAgentNames[agentNum];

                            // can not post himself in this thread (if the bounded queue - then we deadlock on waiting the post to complete)
                            if (randomName != thisAgentName)
                            {
                                string line5 = $"{n}-{agentNum}) Line5 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                                var randomAgent = await _agents.GetAgent(randomName);
                                await randomAgent?.Post(new AddLineMessage(line5));
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // NOOP
                    }
                }

                streamWriter.Flush();

                Console.WriteLine($"Exiting MailboxProcessor Thread: {threadId} File: {filePath}");
            }, token, capacity);

            string agentName = Path.GetFileNameWithoutExtension(filePath);
            
            agent.AgentStarting += (s, a) => { _agents.AddAgent(agentName, agent); };
            agent.Start();
            agent.AgentStopped += (s,a) => { _agents.RemoveAgent(agentName); };

            return agent;
        }
    }
}
