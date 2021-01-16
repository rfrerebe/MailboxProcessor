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

            /*
            using var scheduler = new Threading.Schedulers.WorkStealingTaskScheduler();

            var t1 = Task.Factory.StartNew(async () => {
                Thread.Sleep(100);
                Console.WriteLine($"Thread1: {Thread.CurrentThread.ManagedThreadId} IsPool: {Thread.CurrentThread.IsThreadPoolThread}");
                await Task.Delay(100).ConfigureLongRunning();
                Console.WriteLine($"Thread2: {Thread.CurrentThread.ManagedThreadId} IsPool: {Thread.CurrentThread.IsThreadPoolThread}");
                await Task.Delay(100);
                Console.WriteLine($"Thread3: {Thread.CurrentThread.ManagedThreadId} IsPool: {Thread.CurrentThread.IsThreadPoolThread}");
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

            await t1;

            Console.WriteLine($"BEFORE DELAY1: {TaskScheduler.Current.GetType().Name}");

            await Task.Delay(1000).ConfigureScheduler(scheduler);

            Console.WriteLine($"AFTER DELAY1: {TaskScheduler.Current.GetType().Name}");

            await Task.Delay(1000);


            Console.WriteLine($"AFTER DELAY2: {TaskScheduler.Current.GetType().Name}");

            await Task.CompletedTask.ConfigureScheduler(scheduler);

            Console.WriteLine($"AFTER AWAIT1: {TaskScheduler.Current.GetType().Name}");

            await Task.CompletedTask.ConfigureScheduler(TaskScheduler.Default);

            Console.WriteLine($"AFTER AWAIT2: {TaskScheduler.Current.GetType().Name}");

            return;
            */

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
