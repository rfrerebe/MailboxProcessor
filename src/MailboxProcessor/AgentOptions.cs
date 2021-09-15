using System;
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
            this.scanFunction = null;
        }

        public int? BoundedCapacity { get; set; }

        public CancellationToken? CancellationToken { get; set; }

        public TaskScheduler TaskScheduler { get; set; }

        public TaskCreationOptions TaskCreationOptions { get; set; }

        public Func<T, Task<ScanResults>> scanFunction { get; set;  }
    }
}
