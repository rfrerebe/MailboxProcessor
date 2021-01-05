using MailboxProcessor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public class AgentDictionary<Message>
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

        public async Task<Agent<Message>> GetAgent(string name)
        {
            if (_agents.TryGetValue(name, out var lazyVal))
            {
                return await lazyVal.Value.Task;
            }

            throw new Exception($"Agent {name} is not found in the dictionary");
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

        private static readonly string[] allAgentNames = new string[] { "testMailbox1", "testMailbox2", "testMailbox3", "testMailbox4" };
        */

        public static Agent<Message> GetAgent(string filePath, CancellationToken? token = null, int? capacity = null)
        {
            string thisAgentName = Path.GetFileNameWithoutExtension(filePath);

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
                            List<string> list = new List<string>();
                            for (int k = 0; k < 10; ++k)
                            {
                                string line4 = $"{n}-{k}) Line4 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                                list.Add(line4);
                            }
                            var chan = addMultyLineMessage.Channel;
                            chan.Reply(new AddMultyLineMessageReply(list.ToArray()));
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

            // agent.AgentStarting += (s, a) => { _agents.AddAgent(thisAgentName, agent); };
            agent.Start();
            // agent.AgentStopped += (s,a) => { _agents.RemoveAgent(thisAgentName); };

            return agent;
        }
    }
}
