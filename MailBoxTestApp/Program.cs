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

            async Task RunAgent(Agent<Message> agent1, Agent<Message> agent2, Agent<Message> agent3, Agent<Message> agent4)
            {
                try
                {
                    for (int i = 0; i < 50000; ++i)
                    {
                        string line = string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25));
                        var lineNumber = await agent1.PostAndReply<int?>(channel => new AddLineAndReplyMessage(channel, line));
                   
                        for (int j = 0; j < 5; ++j)
                        {
                            string line2 = $"{lineNumber}) {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            await agent2.Post(new AddMultyLineMessage(line: line2, nextAgent: agent4));
                        }

                        for (int j = 0; j < 5; ++j)
                        {
                            string line3 = $"{lineNumber}) {string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 25))}";
                            await agent3.Post(new AddLineMessage(line3));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Agent Work is Cancelled");
                }
            };

            using (var agent1 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox1.txt", token: cts.Token, capacity: 100))
            using (var agent2 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox2.txt", token: cts.Token, capacity: 100))
            using (var agent3 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox3.txt", token: cts.Token, capacity: 100))
            using (var agent4 = AgentFactory.GetAgent(filePath: @"c:\TEMP\testMailbox4.txt", token: cts.Token, capacity: 100))
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

      
       
    }
}
