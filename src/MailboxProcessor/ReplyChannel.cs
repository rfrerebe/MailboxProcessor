using System;

namespace MailboxProcessor
{
    public interface IReplyChannel<TReply>
    {
        void ReplyResult(TReply reply);
        void ReplyError(Exception exception);
    }

    class ReplyChannel<TReply> : IReplyChannel<TReply>
    {
        private readonly Action<TReply> _replyResultFunc;
        private readonly Action<Exception> _replyErrorFunc;

        internal ReplyChannel(Action<TReply> onResultFunc, Action<Exception> onErrorFunc = null)
        {
            _replyResultFunc = onResultFunc;
            _replyErrorFunc = onErrorFunc;
        }

        public void ReplyResult(TReply reply)
        {
            _replyResultFunc(reply);
        }

        public void ReplyError(Exception exception)
        {
            _replyErrorFunc(exception);
        }
    }
}