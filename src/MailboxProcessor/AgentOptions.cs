using System.Threading;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public class AgentOptions
    {
        public static readonly AgentOptions Default = new AgentOptions();

        public AgentOptions()
        {
            this.TaskScheduler = TaskScheduler.Default;
            this.TaskCreationOptions = TaskCreationOptions.None;
            this.BoundedCapacity = 100;
        }

        public int? BoundedCapacity { get; set; }

        public CancellationToken? CancellationToken { get; set; }

        public TaskScheduler TaskScheduler { get; set; }

        public TaskCreationOptions TaskCreationOptions { get; set; }
    }
}
