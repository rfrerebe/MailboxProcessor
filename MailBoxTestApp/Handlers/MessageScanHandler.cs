using MailboxProcessor;
using MailBoxTestApp.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp.Handlers
{
    /// <summary>
    /// Scan (inspect) message before its processing
    /// </summary>
    internal class MessageScanHandler : IMessageScanHandler<Message>
    {
        private readonly IAgent<Message> _countAgent;

        public MessageScanHandler(IAgent<Message> countAgent)
        {
            _countAgent = countAgent;
        }

        void IMessageScanHandler<Message>.OnStart()
        {

        }

        public async Task<ScanResults> Handle(Message msg, CancellationToken token)
        {
            if (msg is AddLineMessage addLineMessage)
            {
                // send message for additional processing
                await _countAgent.Post(new CountMessage());

                return ScanResults.None; //ScanResults.Handled
            }

            // if ScanResults.None then message is not handled and will be processed as usual
            return ScanResults.None;
        }

        void IMessageScanHandler<Message>.OnEnd()
        {
            
        }
    }
}
