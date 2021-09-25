namespace MailboxProcessor
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class Agent<TMsg> : IAgentWorker<TMsg>, IDisposable
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
            _inputMailbox = new Mailbox<TMsg>(
                agentOptions.CancellationToken, 
                agentOptions.BoundedCapacity, 
                singleWriter: false);

            if (_agentOptions.ScanHandler != null)
            {
                // unbounded capacity, single writer
                _outputMailbox = new Mailbox<TMsg>(
                    agentOptions.CancellationToken, 
                    boundedCapacity: agentOptions.ScanBoundedCapacity, 
                    singleWriter: true);
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

        /// <summary>
        /// Initializes the task which inspects incomming messages
        /// </summary>
        /// <param name="onTaskError">handler for the task errors</param>
        private void _InitScan(Action<Task> onTaskError)
        {
            var scanHandler = _agentOptions.ScanHandler;

            _scanTask = Task.Factory.StartNew(async () => {

                var token = this.CancellationToken;
                scanHandler.OnStart();
                TMsg[] scanResults = null;

                try
                {
                    while (IsRunning)
                    {
                        TMsg msg = await _inputMailbox.Receive();
                        // processing scanned messages it can be null, or an array containing the original or new messages
                        if ((scanResults = await scanHandler.Handle(msg, token)) != null)
                        {
                            foreach (TMsg scannedMsg in scanResults)
                            {
                                await _outputMailbox.Post(scannedMsg);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleException(this.IsRunning, this.CancellationToken, ExceptionDispatchInfo.Capture(ex));
                }
                finally
                {
                    scanHandler.OnEnd();
                }

            }, this.CancellationToken, _agentOptions.ScanTaskCreationOptions, _agentOptions.ScanTaskScheduler).Unwrap();

            this._scanTask.ContinueWith(onTaskError, TaskContinuationOptions.ExecuteSynchronously);
        }

        protected bool IsScanAvailable => _agentOptions.ScanHandler != null;

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
                this._InitScan(onTaskError);
            }
        }

        /// <summary>
        /// Stops the agent
        /// </summary>
        /// <param name="force">If force is true then all message processing stops immediately</param>
        /// <returns></returns>
        public async Task Stop(bool force = false, TimeSpan? timeout = null)
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
                            var savedTask1 = _agentTask ?? Task.CompletedTask;
                            var savedTask2 = _scanTask ?? Task.CompletedTask;
                            var completion = _inputMailbox.Completion;
                            Task waitAllTask = Task.CompletedTask;

                            if (!force)
                            {
                                _inputMailbox.Stop(false);

                                if (IsScanAvailable)
                                {
                                    await completion.ContinueWith((antecedent) => _outputMailbox.Stop(false));
                                    completion = _outputMailbox.Completion;
                                }
                            }
                            else
                            {
                                _inputMailbox.Stop(true);
                                if (IsScanAvailable)
                                {
                                    _outputMailbox.Stop(true);
                                }
                            }

                            waitAllTask = Task.WhenAll(savedTask1, savedTask2);


                            if (timeout != null)
                            {
                                await Task.WhenAny(waitAllTask, Task.Delay(timeout.Value));
                            }
                            else
                            {
                                await waitAllTask;
                            }
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
            this.Stop(true).Wait(0);
        }
    }
}
