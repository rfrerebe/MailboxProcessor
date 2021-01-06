using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    public static class AsyncHelper
    {
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

    }
}
