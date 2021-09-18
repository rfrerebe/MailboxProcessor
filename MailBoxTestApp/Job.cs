using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public static class Job
    {
        public static async Task Run(IAgent<Message> agent, string workPath, string jobName)
        { 
            var jobReply = await agent.Ask<StartJobReply>(channel => new StartJob(channel, workPath));

            Console.WriteLine($"{jobName} took: {jobReply.JobTimeMilliseconds} milliseconds");
        }
    }
}
