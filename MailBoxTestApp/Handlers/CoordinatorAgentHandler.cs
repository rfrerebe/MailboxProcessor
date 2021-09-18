using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp.Handlers
{
    internal class CoordinatorAgentHandler : IMessageHandler<Message>
    {
        void IMessageHandler<Message>.OnStart()
        {

        }

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

                    using (var agent1 = AgentFactory.CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox1.txt"), agentOptions))
                    using (var agent2 = AgentFactory.CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox2.txt"), agentOptions))
                    using (var agent3 = AgentFactory.CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox3.txt"), agentOptions))
                    using (var agent4 = AgentFactory.CreateFileAgent(filePath: Path.Combine(workPath, "testMailbox4.txt"), agentOptions))
                    {
                        await StartJob(agent1, agent2, agent3, agent4);

                        // allows to wait till all messages processed
                        Task[] stopTasks = new Task[] { agent1.Stop(), agent2.Stop(), agent3.Stop(), agent4.Stop() };
                        await Task.WhenAll(stopTasks);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }

                sw.Stop();

                var chan = startJob.Channel;
                chan.ReplyResult(new StartJobReply(sw.ElapsedMilliseconds));
            }
        }

        void IMessageHandler<Message>.OnEnd()
        {

        }

        private async Task StartJob(IAgent<Message> agent1, IAgent<Message> agent2, IAgent<Message> agent3, IAgent<Message> agent4)
        {
            const int numberOfLines = 25000;
            try
            {
                // task #1 which uses 1 agent to post lines
                Func<object, Task> taskBody = async (state) =>
                {
                    IAgent<Message> agent = (IAgent<Message>)state;
                    for (int i = 0; i < numberOfLines; ++i)
                    {
                        for (int j = 0; j < 4; ++j)
                        {
                            string line = $"Line{j} {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            await agent.Post(new AddLineMessage(line));
                        }
                    }
                    
                    // the last message with Ask patern to wait until all messages are processed
                    string result = await agent.Ask<string>(reply => new WaitForCompletion(reply));
                    Console.WriteLine(result);
                };

                // several tasks are run in parrallel
                await Task.WhenAll(
                    Task.Factory.StartNew(taskBody, agent1).Unwrap(),
                    Task.Factory.StartNew(taskBody, agent2).Unwrap(),
                    Task.Factory.StartNew(taskBody, agent3).Unwrap(),
                    Task.Factory.StartNew(taskBody, agent4).Unwrap());
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Agent Work is Cancelled");
            }
        }
    }
}
