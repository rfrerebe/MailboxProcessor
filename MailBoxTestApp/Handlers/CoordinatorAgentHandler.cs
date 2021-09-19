using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp.Handlers
{
    internal class CoordinatorAgentHandler : IMessageHandler<Message>
    {
        private const int AGENTS_COUNT = 5;
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
                    AgentOptions<Message> agentOptions = new AgentOptions<Message>()
                    {
                        CancellationToken = token,
                        BoundedCapacity = 100,
                        ScanHandler = new MessageScanHandler(startJob.countAgent)
                    };

                    // AgentOptions<Message> agentOptions1 = new AgentOptions<Message>(agentOptions) { ScanHandler = null };

                    IAgent<Message>[] fileAgents = Enumerable.Repeat(0, AGENTS_COUNT).Select((x, index) =>
                    {
                        return AgentFactory.CreateFileAgent(
                            filePath: Path.Combine(workPath, $"testMailbox{index+1}.txt"),
                            agentOptions: agentOptions);
                    }).ToArray();

                    // disposing is not  really needed because we wait for completion with 'Stop', but it does not hurt
                    using (var agentsDisposable = new CompositeDisposable(fileAgents))
                    {
                        await StartJob(fileAgents);

                        // allows to wait till all messages processed
                        Task[] stopTasks = fileAgents.Select(agent => agent.Stop()).ToArray();
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

        private async Task StartJob(IEnumerable<IAgent<Message>> agents)
        {
            const int numberOfLines = 100000;
            try
            {
                IAgent<Message>[] _agents = agents.ToArray();
                int agentsCount = _agents.Length;


                Func <Task> taskBody = async () =>
                {
                    for (int i = 0; i < numberOfLines; ++i)
                    {
                        string line = $"Line{i % agentsCount} {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";

                        await _agents[i % agentsCount].Post(new AddLineMessage(line));
                    }
                };

                // several tasks are run in parrallel
                await Task.WhenAll(_agents.Select(x => Task.Run(taskBody)).ToList());

                // the last message with Ask patern to wait until all messages are processed
                // (not really needed because 'Stop' waits fot completion anyway, but we can get the results of processing here)
                Task<string>[] resultTasks = _agents.Select(agent => agent.Ask<string>(reply => new WaitForCompletion(reply))).ToArray();
                string[] results = await Task.WhenAll(resultTasks);
                Array.ForEach(results,(result) => Console.WriteLine(result));
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Agent Work is Cancelled");
            }
        }
    }
}
