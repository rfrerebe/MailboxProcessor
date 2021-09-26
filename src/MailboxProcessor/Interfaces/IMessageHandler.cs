using System.Threading;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IMessageHandler<TMsg>
    {
        void OnStart();

        Task Handle(TMsg message, CancellationToken token);
        
        void OnStop();
    }
}
