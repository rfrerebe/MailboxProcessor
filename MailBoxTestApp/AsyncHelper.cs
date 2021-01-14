using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public static class AsyncHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<TResult> AsTask<T, TResult>(this Task<T> task)
            where T : TResult
            where TResult : class
        {
            return await task;
        }

        public static Task ForEachAsync<T>(this IEnumerable<T> source, int maxDegreeOfParallelism, Func<T, Task> body)
        {
            return Task.WhenAll(from partition in Partitioner.Create(source).GetPartitions(maxDegreeOfParallelism)
                                select Task.Run(async () =>
                                {
                                    using (partition)
                                        while (partition.MoveNext())
                                            await body(partition.Current);
                                }));
        }

        
        /// <summary>
        /// Configures to run continuation on a custom scheduler
        /// </summary>
        /// <param name="task"></param>
        /// <param name="scheduler"></param>
        /// <returns></returns>
        /// <example>
        /// public static async Task Foo() {
        ///   Console.WriteLine("Foo Started");
        ///   await SomeAsyncTask().ConfigureScheduler(scheduler);
        ///   Console.WriteLine("Foo Finished");
        /// }
        /// </example>
        public static CustomTaskAwaitable ConfigureScheduler(this Task task, TaskScheduler scheduler)
        {
            return new CustomTaskAwaitable(scheduler);
        }
 
        public static CustomTaskAwaitable<TResult> ConfigureScheduler<TResult>(this Task<TResult> task, TaskScheduler scheduler)
        {
            return new CustomTaskAwaitable<TResult>(task, scheduler);
        }

        public struct CustomTaskAwaitable
        {
            CustomTaskAwaiter awaitable;

            public CustomTaskAwaitable(TaskScheduler scheduler)
            {
                awaitable = new CustomTaskAwaiter(scheduler);
            }

            public CustomTaskAwaiter GetAwaiter() { return awaitable; }

            public readonly struct CustomTaskAwaiter : INotifyCompletion
            {
                readonly TaskScheduler scheduler;

                public CustomTaskAwaiter(TaskScheduler scheduler)
                {
                    this.scheduler = scheduler;
                }

                public void OnCompleted(Action continuation)
                {
                    Task.Factory.StartNew(continuation, default(CancellationToken), TaskCreationOptions.PreferFairness, scheduler);
                }

                /// <summary>
                /// Continuation is always required so it returns false to always run the continuation
                /// </summary>
                public bool IsCompleted { get { return false; } }

                public void GetResult() {
                    // NOOP
                }
            }
        }

        public struct CustomTaskAwaitable<T>
        {
            CustomTaskAwaiter<T> awaitable;

            public CustomTaskAwaitable(Task<T> task, TaskScheduler scheduler)
            {
                awaitable = new CustomTaskAwaiter<T>(task, scheduler);
            }

            public CustomTaskAwaiter<T> GetAwaiter() { return awaitable; }

            public readonly struct CustomTaskAwaiter<TResult> : INotifyCompletion
            {
                readonly Task<TResult> task;
                readonly TaskScheduler scheduler;

                public CustomTaskAwaiter(Task<TResult> task, TaskScheduler scheduler)
                {
                    this.task = task;
                    this.scheduler = scheduler;
                }

                public void OnCompleted(Action continuation)
                {
                    // Action action = () => { Console.WriteLine($"before continuation .... {Thread.CurrentThread.ManagedThreadId}"); continuation(); Console.WriteLine($"after continuation .... {Thread.CurrentThread.ManagedThreadId}"); };
                    Task.Factory.StartNew(continuation, default(CancellationToken), TaskCreationOptions.PreferFairness, scheduler);
                }

                public bool IsCompleted { get { return false; } }

                public TResult GetResult() { return task.Result; } 
            }
        }
    }
}
