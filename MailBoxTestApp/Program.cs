using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
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

            AgentOptions<Message> agentOptions = new AgentOptions<Message>() { CancellationToken = cts.Token, BoundedCapacity = 50 };

            var agent = AgentFactory.CreateCoordinatorAgent(agentOptions);
            try
            {

                await Job.Run(agent, @"c:\TEMP\DIR1", "Job1");
                await Job.Run(agent, @"c:\TEMP\DIR2", "Job2");

            }
            finally
            {
                await agent.Stop();
            }

            await Task.Yield();

            Console.WriteLine("Press Any Key to Exit ....");
            Console.ReadKey();
        }
    }
}
