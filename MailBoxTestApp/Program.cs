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

            AgentOptions<Message> agentOptions = new AgentOptions<Message>() { CancellationToken = cts.Token, BoundedCapacity = 100 };

            using IAgent<Message> coordinatorAgent = AgentFactory.CreateCoordinatorAgent(agentOptions);
            using IAgent<Message> countAgent = AgentFactory.CreateCountAgent(@"c:\TEMP\count.txt", agentOptions);
            try
            {
                await Job.Run(coordinatorAgent, @"c:\TEMP\DIR1", "Job1", countAgent);
                await Job.Run(coordinatorAgent, @"c:\TEMP\DIR2", "Job2", countAgent);
            }
            finally
            {
                // waits to process all messages
                await coordinatorAgent.Stop();
                await countAgent.Stop();
            }

            await Task.Yield();

            Console.WriteLine("Press Any Key to Exit ....");
            Console.ReadKey();
        }
    }
}
