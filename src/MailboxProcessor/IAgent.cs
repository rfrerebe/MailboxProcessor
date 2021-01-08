using System;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IAgentErrors 
    {
        IObservable<Exception> Errors { get; }
    }

    public interface IAgentWriter<in TMsg>: IAgentErrors, IDisposable
    {
        bool IsStarted { get; }
        Task Post(TMsg message);
        Task<TReply> Ask<TReply>(Func<IReplyChannel<TReply>, TMsg> msgf, int? timeout = null);
        Task Stop(bool force = false);
    }

    public interface IAgent<TMsg>: IAgentWriter<TMsg>
    {
        bool IsRunning { get; }
        void ReportError(Exception ex);
        Task<TMsg> Receive();
    }
}