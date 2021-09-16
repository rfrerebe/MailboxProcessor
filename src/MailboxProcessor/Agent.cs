namespace MailboxProcessor
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    public static class Agent
    {
        public static Agent<T> Start<T>(IMessageHandler<T> messageHandler, AgentOptions<T> agentOptions = null)
            where T : class
        {
            var agent = new Agent<T>(messageHandler, agentOptions);
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
        private readonly IMessageHandler<TMsg> _messageHandler;
        private readonly Mailbox<TMsg> _outputMailbox;
        private readonly Mailbox<TMsg> _inputMailbox;
        private readonly AgentOptions<TMsg> _agentOptions;

        private readonly Observable<Exception> _errorsObservable;
        private volatile int _started;
        private Task _agentTask;
        private Task _scanTask;
        
        public event EventHandler<EventArgs> AgentStarting;
        public event EventHandler<EventArgs> AgentStopping;
        public event EventHandler<EventArgs> AgentStopped;

        public Agent(IMessageHandler<TMsg> messageHandler, AgentOptions<TMsg> agentOptions = null)
        {
            _agentOptions = agentOptions ?? AgentOptions<TMsg>.Default;
            _messageHandler = messageHandler;
            _inputMailbox = new Mailbox<TMsg>(agentOptions.CancellationToken, agentOptions.BoundedCapacity, singleWriter: false);

            if (_agentOptions.scanFunction != null)
            {
                // unbounded capacity, single writer
                _outputMailbox = new Mailbox<TMsg>(agentOptions.CancellationToken, boundedCapacity: null, singleWriter: true);
            }
            else
            {
                // the same as input mailbox
                _outputMailbox = _inputMailbox;
            }

            _errorsObservable = new Observable<Exception>();
            _started = 0;
            DefaultTimeout = Timeout.Infinite;
        }

        protected bool IsScanAvailable => _agentOptions.scanFunction != null;

        public IObservable<Exception> Errors => _errorsObservable;

        public bool IsRunning => !(this.CancellationToken.IsCancellationRequested || this._outputMailbox.Completion.IsCompleted);

        public bool IsStarted => this._started == 1;

        public int DefaultTimeout { get; set; }

        public CancellationToken CancellationToken => _outputMailbox.CancellationToken;

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

                    try
                    {
                        _messageHandler.OnStart();

                        var token = this.CancellationToken;
                        while (IsRunning)
                        {
                            await _messageHandler.Handle(await Receive(), token);
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleException(this.IsRunning, this.CancellationToken, ExceptionDispatchInfo.Capture(ex));
                    }
                    finally
                    {
                        _messageHandler.OnEnd();
                    }
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

            if (IsScanAvailable)
            {
                var scan = _agentOptions.scanFunction;

                _scanTask = Task.Factory.StartNew(async () => {
                    while (IsRunning)
                    {
                        try
                        {
                            TMsg msg = await _inputMailbox.Receive();
                            if (ScanResults.Handled != await scan(msg))
                            {
                                await _outputMailbox.Post(msg);
                            }
                        }
                        catch (Exception ex)
                        {
                            HandleException(this.IsRunning, this.CancellationToken, ExceptionDispatchInfo.Capture(ex));
                        }
                    }
                }, this.CancellationToken, _agentOptions.TaskCreationOptions, _agentOptions.TaskScheduler).Unwrap();

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
                            _outputMailbox.Stop(force);
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
                var mailbox = _inputMailbox ?? _outputMailbox;
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
                return await _outputMailbox.Receive();
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

                _inputMailbox.Stop(true);
                _outputMailbox.Stop(true);

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
