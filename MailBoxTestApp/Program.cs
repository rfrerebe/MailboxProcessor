using MailboxProcessor;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            // cts.CancelAfter(100);

            async Task RunJob(Agent<Message> agent1, Agent<Message> agent2, Agent<Message> agent3, Agent<Message> agent4)
            {
                const int numberOfLines = 25000;
                try
                {
                    // task #1 which uses 1 agent to post lines
                    Func<Task> taskPart1 = async ()=>
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
                            var lineNumber = await agent1.PostAndReply<int?>(channel => new AddLineAndReplyMessage(channel, line));

                            for (int j = 0; j < 5; ++j)
                            {
                                string line2 = $"Line2 {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                                // wait for reply
                                var reply = await agent2.PostAndReply<AddMultyLineMessageReply>(channel => new AddMultyLineMessage(channel, line: line2));

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
            };

            const int capacity = 100;

            using (var agent1 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox1.txt", token: cts.Token, capacity: capacity))
            using (var agent2 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox2.txt", token: cts.Token, capacity: capacity))
            using (var agent3 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox3.txt", token: cts.Token, capacity: capacity))
            using (var agent4 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox4.txt", token: cts.Token, capacity: capacity))
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                await RunJob(agent1,agent2, agent3, agent4);

                sw.Stop();

                Console.WriteLine($"Job took: {sw.ElapsedMilliseconds} milliseconds");

                sw.Restart();

                // allows to wait till all messages processed
                Task[] stopTasks = new Task[] { agent1.Stop(), agent2.Stop(), agent3.Stop(), agent4.Stop() };
                await Task.WhenAll(stopTasks);

                sw.Stop();

                Console.WriteLine($"Stopping Job took: {sw.ElapsedMilliseconds} milliseconds");
            }

            await Task.Yield();

            Console.WriteLine("Press Any Key to Exit ....");
            Console.ReadKey();
        }
    }
}
