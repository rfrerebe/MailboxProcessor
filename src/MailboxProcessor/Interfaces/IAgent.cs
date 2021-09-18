using System;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IAgent : IAgentErrors, IDisposable
    {
        bool IsStarted { get; }
        Task Stop(bool force = false, TimeSpan? timeout = null);
    }

    public interface IAgent<in TMsg> : IAgent
    {
        Task Post(TMsg message);
        Task<TReply> Ask<TReply>(Func<IReplyChannel<TReply>, TMsg> msgf, int? timeout = null);
    }
}
