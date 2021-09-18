using MailboxProcessor;
using MailBoxTestApp.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp.Handlers
{
    /// <summary>
    /// Scan (inspect) message before its processing
    /// </summary>
    internal class MessageScanHandler : IMessageScanHandler<Message>
    {
        void IMessageScanHandler<Message>.OnStart()
        {

        }

        public Task<ScanResults> Handle(Message msg, CancellationToken token)
        {
            if (msg is AddLineMessage addLineMessage)
            {
                // Console.WriteLine($"Scanned {addLineMessage.Line.Substring(0, 35)}");
                return Task.FromResult(ScanResults.None);
            }

            // if ScanResults.None then message is not handled and will be processed as usual
            return Task.FromResult(ScanResults.None);
        }

        void IMessageScanHandler<Message>.OnEnd()
        {

        }
    }
}
