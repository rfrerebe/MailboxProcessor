namespace MailboxProcessor
{
    using System;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    internal class Mailbox<TMsg> : IDisposable
    {
        private readonly Channel<TMsg> _channel;
        private ChannelWriter<TMsg> _writer;
        private ChannelReader<TMsg> _reader;
        private readonly CancellationToken _token;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _linkedCts;
        public Mailbox(CancellationToken? token = null, int? boundedCapacity = null, bool singleWriter = false)
        {
            _cts = new CancellationTokenSource();
            _linkedCts = token.HasValue ? CancellationTokenSource.CreateLinkedTokenSource(token.Value, _cts.Token) : _cts;
            _token = _linkedCts.Token;

            if (boundedCapacity == null)
            {
                _channel = Channel.CreateUnbounded<TMsg>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = singleWriter, AllowSynchronousContinuations = true });
            }
            else
            {
                _channel = Channel.CreateBounded<TMsg>(new BoundedChannelOptions(boundedCapacity.Value) { SingleReader = true, SingleWriter = singleWriter, AllowSynchronousContinuations = true });
            }

            _writer = _channel.Writer;
            _reader = _channel.Reader;
        }

        public int CurrentQueueLength
        {
            get
            {
                return _reader.CanCount ? _reader.Count : -1;
            }
        }

        public CancellationToken CancellationToken => _token;


        public void Dispose()
        {
            this.Stop(true);
        }

        public Task Completion => _reader.Completion;

        internal void Stop(bool force)
        {
            _writer.TryComplete();
            if (force)
            {
                _cts.Cancel();
            }
        }

        internal ValueTask Post(TMsg msg)
        {
            return _writer.WriteAsync(msg, _token);
        }

        internal async Task<TMsg> Receive()
        {
            _token.ThrowIfCancellationRequested();

            if (_reader.TryRead(out var msg1))
            {
                return msg1;
            }

            bool isOk = await _reader.WaitToReadAsync(_token); //if this returns false the channel is completed
            if (isOk)
            {
                if (_reader.TryRead(out var msg))
                {
                    return msg;
                }
                else
                {
                    throw new OperationCanceledException(_token);
                }
            }
            else
            {
                throw new OperationCanceledException(_token);
            }
        }

    }
}