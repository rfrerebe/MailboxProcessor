using System.Threading;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public record AgentOptions<T>
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
            this.ScanBoundedCapacity = 100;
        }

        public AgentOptions(AgentOptions<T> agentOptions)
        {
            this.TaskScheduler = agentOptions.TaskScheduler;
            this.TaskCreationOptions = agentOptions.TaskCreationOptions;
            this.BoundedCapacity = agentOptions.BoundedCapacity;

            this.ScanTaskScheduler = agentOptions.ScanTaskScheduler;
            this.ScanTaskCreationOptions = agentOptions.ScanTaskCreationOptions;
            this.ScanHandler = agentOptions.ScanHandler;
            this.ScanBoundedCapacity = agentOptions.ScanBoundedCapacity;
        }

        public int? BoundedCapacity { get; init; }

        public CancellationToken? CancellationToken { get; init; }

        public TaskScheduler TaskScheduler { get; init; }

        public TaskCreationOptions TaskCreationOptions { get; init; }

        public TaskScheduler ScanTaskScheduler { get; init; }

        public TaskCreationOptions ScanTaskCreationOptions { get; init; }

        public int? ScanBoundedCapacity { get; init; }

        public IMessageScanHandler<T> ScanHandler { get; init; }
    }
}
