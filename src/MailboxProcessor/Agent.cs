namespace MailboxProcessor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Agent
    {
        public static Agent<T> Start<T>(Func<Agent<T>, Task> body, CancellationToken? cancellationToken = null, int? capacity = null)
            where T : class
        {
            var agent = new Agent<T>(body, cancellationToken, capacity);
            agent.Start();
            return agent;
        }
    }

    public class Agent<TMsg> : IDisposable
    {
        private readonly Func<Agent<TMsg>, Task> _body;
        private readonly Mailbox<TMsg> _mailbox;
        private bool _started;
        private readonly Observable<Exception> _errorEvent;

        public Agent(Func<Agent<TMsg>, Task> body, CancellationToken? cancellationToken = null, int? capacity = null)
        {
            _body = body;
            _mailbox = new Mailbox<TMsg>(cancellationToken, capacity);
            DefaultTimeout = Timeout.Infinite;
            _started = false;
            _errorEvent = new Observable<Exception>();
        }

        public IObservable<Exception> Errors => _errorEvent;

        public int DefaultTimeout { get; set; }

        public CancellationToken CancellationToken => _mailbox.CancellationToken;

        public void Start()
        {
            if (_started)
                throw new InvalidOperationException("MailboxProcessor already started");

            _started = true;

            // Protect the execution and send errors to the event.
            // Note that exception stack traces are lost in this design - in an extended design
            // the event could propagate an ExceptionDispatchInfo instead of an Exception.

            async Task StartAsync()
            {
                try
                {
                   await _body(this);
                }
                catch (Exception exception)
                {
                    // var err = ExceptionDispatchInfo.Capture(exception);
                    _errorEvent.OnNext(exception);
                }
            }

            Task.Factory.StartNew(StartAsync, this.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
        }

        public Task Stop ()
        {
            return _mailbox.Stop();
        }

        public Task Post(TMsg message)
        {
            return _mailbox.Post(message).AsTask();
        }

        public async Task<TReply> PostAndReply<TReply>(Func<IReplyChannel<TReply>, TMsg> msgf, int? timeout = null)
        {
            timeout = timeout ?? DefaultTimeout;
            var tcs = new TaskCompletionSource<TReply>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(this.CancellationToken))
            {
                if (timeout.Value != Timeout.Infinite)
                {
                    cts.CancelAfter(timeout.Value);
                }

                using (cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
                {
                    var msg = msgf(new ReplyChannel<TReply>(reply =>
                    {
                        tcs.TrySetResult(reply);
                    }));

                    await _mailbox.Post(msg);

                    return await tcs.Task;
                }
            }
        }

        public Task<TMsg> Receive()
        {
            return _mailbox.Receive().AsTask();
        }

        public bool TryReceive(out TMsg msg)
        {
            return _mailbox.TryReceive(out msg);
        }

        public void ReportError(Exception ex)
        {
            _errorEvent.OnNext(ex);
        }

        public void Dispose()
        {
            _mailbox.Dispose();
        }
    }
}
