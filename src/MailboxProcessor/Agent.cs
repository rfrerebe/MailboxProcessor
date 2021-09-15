namespace MailboxProcessor
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Agent
    {
        public static Agent<T> Start<T>(Func<Agent<T>, Task> body, AgentOptions agentOptions = null)
            where T : class
        {
            var agent = new Agent<T>(body, agentOptions);
            agent.Start();
            return agent;
        }
    }

    public enum ScanResults
    {
        None = 0,
        Handled = 1
    }

    public class Agent<TMsg> : IAgent<TMsg>, IDisposable
    {
        private readonly Func<Agent<TMsg>, Task> _body;
        private readonly Mailbox<TMsg> _mailbox;
        private readonly Observable<Exception> _errorsObservable;
        private volatile int _started;
        private Task _agentTask;
        private Task _scanTask;
        private readonly AgentOptions _agentOptions;
        private readonly Mailbox<TMsg> _scanMailbox;
        private readonly Func<TMsg, Task<ScanResults>> _scan;

        public event EventHandler<EventArgs> AgentStarting;
        public event EventHandler<EventArgs> AgentStopping;
        public event EventHandler<EventArgs> AgentStopped;

        public Agent(Func<Agent<TMsg>, Task> body, AgentOptions agentOptions = null, Func<TMsg, Task<ScanResults>> scan = null)
        {
            _agentOptions = agentOptions ?? AgentOptions.Default;
            _body = body;
            _scan = scan;
            if (scan != null)
            {
                _scanMailbox = new Mailbox<TMsg>(agentOptions.CancellationToken, 100);
            }
            _mailbox = new Mailbox<TMsg>(agentOptions.CancellationToken, agentOptions.BoundedCapacity);
            _errorsObservable = new Observable<Exception>();
            _started = 0;
            DefaultTimeout = Timeout.Infinite;
        }

        public IObservable<Exception> Errors => _errorsObservable;

        public bool IsRunning => !(this.CancellationToken.IsCancellationRequested || this._mailbox.Completion.IsCompleted);

        public bool IsStarted => this._started == 1;

        public int DefaultTimeout { get; set; }

        public CancellationToken CancellationToken => _mailbox.CancellationToken;

        public void Start()
        {
            int oldStarted = Interlocked.CompareExchange(ref _started, 1, 0);

            if (oldStarted == 1)
                throw new InvalidOperationException("MailboxProcessor already started");

            void onTaskError(Task antecedent)
            {
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
                    // Stop if it's running
                    Interlocked.CompareExchange(ref _started, 0, 1);
                    Interlocked.CompareExchange(ref _agentTask, null, _agentTask);
                    Interlocked.CompareExchange(ref _scanTask, null, _scanTask);
                }
            }

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
                    _errorsObservable.OnNext(exception);
                    throw;
                }
            }

            this._agentTask = Task.Factory.StartNew(StartAsync, this.CancellationToken, _agentOptions.TaskCreationOptions, _agentOptions.TaskScheduler).Unwrap();
            this._agentTask.ContinueWith(onTaskError, TaskContinuationOptions.ExecuteSynchronously);

            if (_scanMailbox != null)
            {
                _scanTask = Task.Run(async () => {
                    while (IsRunning)
                    {
                        try
                        {
                            TMsg msg = await _scanMailbox.Receive();
                            if (ScanResults.Handled != await _scan(msg))
                            {
                                await _mailbox.Post(msg);
                            }
                        }
                        catch (Exception ex)
                        {
                            HandleException(this.IsRunning, this.CancellationToken, ExceptionDispatchInfo.Capture(ex));
                        }
                    }
                }, this.CancellationToken);

                this._scanTask.ContinueWith(onTaskError, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        /// <summary>
        /// Stops the agent
        /// </summary>
        /// <param name="force">If force is true then all message processing stops immediately</param>
        /// <returns></returns>
        public async Task Stop(bool force = false)
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
                var mailbox = _scanMailbox ?? _mailbox;
                await mailbox.Post(message);
            }
            catch (Exception ex)
            {
                HandleException(this.IsRunning, this.CancellationToken, ExceptionDispatchInfo.Capture(ex));
            }
        }

        public async Task<TReply> Ask<TReply>(Func<IReplyChannel<TReply>, TMsg> callback, int? timeout = null)
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
                    var msg = callback(new ReplyChannel<TReply>(reply =>
                    {
                        tcs.TrySetResult(reply);
                    },
                    error =>
                    {
                        tcs.TrySetException(error);
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

        public void ReportError(Exception ex)
        {
            _errorsObservable.OnNext(ex);
        }

        public void Dispose()
        {
            try
            {
                var oldStarted = Interlocked.CompareExchange(ref _started, 0, 1);
                if (oldStarted == 1)
                {
                    AgentStopping?.Invoke(this, EventArgs.Empty);
                }
                if (_scanMailbox != null)
                {
                    _scanMailbox.Stop(true);
                }
                _mailbox.Stop(true);
                if (oldStarted == 1)
                {
                    AgentStopped?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                // NOOP
            }
            finally
            {
                AgentStopping = null;
                AgentStopped = null;
                AgentStarting = null;
            }
        }
    }
}
