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

        public async Task<Message[]> Handle(Message msg, CancellationToken token)
        {
            if (msg is AddLineMessage addLineMessage)
            {
                // send message for additional processing
                await _countAgent.Post(new CountMessage());

                // returns the message to continue its processing in the main handler 
                return new[] { addLineMessage };
            }

            // returns the message to continue its processing in the main handler 
            // if it returns 'null' then the message will not be processed in the main handler (treated as already handled here)
            // it can also return multiple messages
            return new[] { msg };
        }

        void IMessageScanHandler<Message>.OnEnd()
        {
            
        }
    }
}
