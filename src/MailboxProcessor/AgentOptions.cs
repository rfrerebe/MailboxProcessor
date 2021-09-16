using System.Threading;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public class AgentOptions<T>
    {
        public static readonly AgentOptions<T> Default = new AgentOptions<T>();

        public AgentOptions()
        {
            this.TaskScheduler = TaskScheduler.Default;
            this.TaskCreationOptions = TaskCreationOptions.None;
            this.BoundedCapacity = 100;

            this.ScanTaskScheduler = TaskScheduler.Default;
            this.ScanTaskCreationOptions = TaskCreationOptions.None;
            this.ScanHandler = null;
        }

        public int? BoundedCapacity { get; set; }

        public CancellationToken? CancellationToken { get; set; }

        public TaskScheduler TaskScheduler { get; set; }
        
        public TaskCreationOptions TaskCreationOptions { get; set; }

        public TaskScheduler ScanTaskScheduler { get; set; }

        public TaskCreationOptions ScanTaskCreationOptions { get; set; }

        public IMessageScanHandler<T> ScanHandler { get; set;  }
    }
}
