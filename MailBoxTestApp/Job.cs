using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public static class Job
    {
        public static async Task Run(IAgent<Message> agent, string workPath, string jobName, IAgent<Message> countAgent)
        { 
            StartJobReply jobReply = await agent.Ask<StartJobReply>(channel => new StartJob()
            {
                Channel = channel,
                WorkPath = workPath,
                countAgent = countAgent
            });


            Console.WriteLine($"{jobName} took: {jobReply.JobTimeMilliseconds} milliseconds");
        }
    }
}
