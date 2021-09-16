using System.Threading;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IMessageHandler<TMsg>
    {
        Task Handle(TMsg message, CancellationToken token);
    }
}
