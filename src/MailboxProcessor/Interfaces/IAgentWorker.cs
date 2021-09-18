using System;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IAgentWorker<TMsg>: IAgent<TMsg>
    {
        bool IsRunning { get; }
        void ReportError(Exception ex);
        Task<TMsg> Receive();
    }
}