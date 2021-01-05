namespace MailboxProcessor
{
    using System;
    using System.Runtime.ExceptionServices;
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
        private readonly Observable<Exception> _errorEvent;
        private volatile int _started;
        private Task _agentTask;

        public event EventHandler<EventArgs> AgentStarting;
        public event EventHandler<EventArgs> AgentStopping;
        public event EventHandler<EventArgs> AgentStopped;

        public Agent(Func<Agent<TMsg>, Task> body, CancellationToken? cancellationToken = null, int? capacity = null)
        {
            _body = body;
            _mailbox = new Mailbox<TMsg>(cancellationToken, capacity);
            DefaultTimeout = Timeout.Infinite;
            _errorEvent = new Observable<Exception>();
            _started = 0;
        }

        public IObservable<Exception> Errors => _errorEvent;

        public bool IsRunning => _started == 1 && !this.CancellationToken.IsCancellationRequested;

        public int DefaultTimeout { get; set; }

        public CancellationToken CancellationToken => _mailbox.CancellationToken;


        public void Start()
        {
            int oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);

            if (oldStarted == 1)
                throw new InvalidOperationException("MailboxProcessor already started");

            async Task StartAsync()
            {
                try
                {
                    AgentStarting?.Invoke(this, EventArgs.Empty);
                    await _body(this);
                }
                catch (Exception exception)
                {
                    // var err = ExceptionDispatchInfo.Capture(exception);
                    _errorEvent.OnNext(exception);
                    throw;
                }
            }

            this._agentTask = Task.Factory.StartNew(StartAsync, this.CancellationToken, TaskCreationOptions.None, TaskScheduler.Default).Unwrap();
            this._agentTask.ContinueWith((antecedent) => {
                var error = antecedent.Exception;
                try
                {
                    if (error != null)
                    {
                        this.ReportError(error);
                    }
                }
                finally
                {
                    // proceed anyway (error or not - clean up anyway)
                    Interlocked.CompareExchange(ref _started, 0, 1);
                    Interlocked.CompareExchange(ref _agentTask, null, _agentTask);
                }
            });
        }

        /// <summary>
        /// Stops the agent
        /// </summary>
        /// <param name="force">If force is true then all message processing stops immediately</param>
        /// <returns></returns>
        public async Task Stop (bool force = false)
        {
            int oldStarted = Interlocked.CompareExchange(ref _started, 0, 1);
            if (oldStarted == 1)
            {
                try
                {
                    try
                    {
                        AgentStarting = null;
                        AgentStopping?.Invoke(this, EventArgs.Empty);
                        AgentStopping = null;
                    }
                    finally
                    {
                        try
                        {
                            var savedTask = _agentTask ?? Task.CompletedTask;
                            _mailbox.Stop(force);
                            await savedTask;
                        }
                        finally
                        {
                            AgentStopped?.Invoke(this, EventArgs.Empty);
                            AgentStopped = null;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // NOOP
                }
            }
        }

        private static void HandleException(bool isRunning, CancellationToken token, ExceptionDispatchInfo ex)
        {
            if (ex.SourceException is AggregateException aggex)
            {
                Exception firstError = null;
                aggex.Flatten().Handle((err) =>
                {
                    if (!(err is OperationCanceledException))
                    {
                        firstError = firstError ?? err;
                        return false;
                    }
                    return true;
                });

                // Channel was closed
                if (!isRunning)
                {
                    throw new OperationCanceledException(token);
                }
                else
                {
                    if (firstError != null)
                    {
                        ex.Throw();
                    }
                }
            }
            else if (ex.SourceException is OperationCanceledException oppex)
            {
                throw oppex;
            }
            else
            {
                // Channel was closed
                if (!isRunning)
                {
                    throw new OperationCanceledException(token);
                }
                else
                {
                    ex.Throw();
                }
            }
            
            // should never happen here (but in any case)
            ex.Throw();
        }

        public async Task Post(TMsg message)
        {
            try
            {
                await _mailbox.Post(message);
            }
            catch (Exception ex)
            {
                HandleException(this.IsRunning, this.CancellationToken, ExceptionDispatchInfo.Capture(ex));
            }
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

                using (cts.Token.Register(() => tcs.TrySetCanceled(this.CancellationToken), useSynchronizationContext: false))
                {
                    var msg = msgf(new ReplyChannel<TReply>(reply =>
                    {
                        tcs.TrySetResult(reply);
                    }));

                    try
                    {
                        await this.Post(msg);
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(this.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }

                    return await tcs.Task;
                }
            }
        }

        public async Task<TMsg> Receive()
        {
            try
            {
                return await _mailbox.Receive();
            }
            catch (Exception ex)
            {
                HandleException(this.IsRunning, this.CancellationToken, ExceptionDispatchInfo.Capture(ex));
            }
            // should never happen here
            return default(TMsg);
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
            this.Stop(true).Wait(1000);
        }
    }
}
