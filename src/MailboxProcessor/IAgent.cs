using System;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IAgentErrors 
    {
        IObservable<Exception> Errors { get; }
    }

    public interface IAgent<TMsg>: IAgentErrors, IDisposable
    {
        bool IsRunning { get; }

        Task Post(TMsg message);
        Task<TReply> Ask<TReply>(Func<IReplyChannel<TReply>, TMsg> msgf, int? timeout = null);
        Task<TMsg> Receive();
        void ReportError(Exception ex);
        Task Stop(bool force = false);
    }
}