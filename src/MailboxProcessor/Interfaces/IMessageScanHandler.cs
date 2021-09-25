using System.Threading;
using System.Threading.Tasks;

namespace MailboxProcessor
{
    public interface IMessageScanHandler<TMsg>
    {
        void OnStart(IAgent<TMsg> agent);

        /// <summary>
        ///  scans (inspects) a message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="token"></param>
        /// <returns>it can be null, or an array containing the original or new messages</returns>
        Task<TMsg[]> Handle(TMsg message, CancellationToken token);
        
        void OnEnd();
    }
}
