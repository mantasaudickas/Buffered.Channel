using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Buffered.Channel
{
    public interface IBufferedChannel<T> : IDisposable
    {
        void RegisterConsumer(Func<IList<T>, Task> callback);
        bool TryWrite(T item);
    }
}