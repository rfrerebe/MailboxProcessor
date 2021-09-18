using System;

namespace MailboxProcessor
{
    public interface IAgentErrors
    {
        IObservable<Exception> Errors { get; }
    }
}
