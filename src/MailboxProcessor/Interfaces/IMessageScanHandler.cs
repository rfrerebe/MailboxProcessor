using System.Threading;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IMessageScanHandler<TMsg>
    {
        void OnStart();

        Task<ScanResults> Handle(TMsg message, CancellationToken token);
        
        void OnEnd();
    }
}
