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

            AgentOptions agentOptions = new AgentOptions() { CancellationToken= cts.Token, BoundedCapacity= 10 };

            using (var agent = AgentFactory.CreateCoordinatorAgent(agentOptions))
            {
                // ************** Start a first Job here ************************
                string workPath = @"c:\TEMP\DIR1";
                var jobReply = await agent.Ask<StartJobReply>(channel => new StartJob(channel, workPath));

                Console.WriteLine($"Job1 took: {jobReply.JobTimeMilliseconds} milliseconds");

                // ************** Start a second Job here ************************
                workPath = @"c:\TEMP\DIR2";
                jobReply = await agent.Ask<StartJobReply>(channel => new StartJob(channel, workPath));

                Console.WriteLine($"Job2 took: {jobReply.JobTimeMilliseconds} milliseconds");

                await agent.Stop();
            }

            await Task.Yield();

            Console.WriteLine("Press Any Key to Exit ....");
            Console.ReadKey();
        }
    }
}
