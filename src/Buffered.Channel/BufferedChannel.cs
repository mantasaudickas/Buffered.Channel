using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Buffered.Channel
{
    public class BufferedChannel<T>: IBufferedChannel<T>
    {
        private readonly List<Func<IList<T>, Task>> _callbacks = new List<Func<IList<T>, Task>>();
        
        private readonly CancellationTokenSource _stopBufferConsumer = new CancellationTokenSource();
        private readonly CancellationTokenSource _stopTimedConsumer = new CancellationTokenSource();

        private readonly int _bufferSize;
        private readonly TimeSpan _flushInterval;
        private readonly Channel<T> _channel;

        private IList<T> _buffer = new List<T>();

        public BufferedChannel(BufferedChannelOptions options)
        {
            BoundedChannelOptions boundedChannelOptions = null;
            if (options != null)
            {
                _bufferSize = options.BufferSize;
                _flushInterval = options.FlushInterval;
                boundedChannelOptions = options.BoundedChannelOptions;
            }

            _channel = boundedChannelOptions != null
                ? System.Threading.Channels.Channel.CreateBounded<T>(boundedChannelOptions)
                : System.Threading.Channels.Channel.CreateUnbounded<T>();

            if (_bufferSize <= 0)
                _bufferSize = BufferedChannelOptions.DefaultBufferSize;

            if (_flushInterval <= TimeSpan.Zero)
                _flushInterval = BufferedChannelOptions.DefaultFlushInterval;

            Task.Factory.StartNew(StartChannelReader, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            Task.Factory.StartNew(StartBufferFlushes, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private async Task StartChannelReader()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(_stopBufferConsumer.Token))
                {
                    if (_channel.Reader.TryRead(out var item))
                    {
                        _buffer.Add(item);

                        if (_buffer.Count >= _bufferSize)
                        {
                            var buffer = Interlocked.Exchange(ref _buffer, new List<T>());
                            if (buffer.Count > 0)
                            {
                                await ExecuteCallbacks(buffer).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }

            // finalize reading..
            while (_channel.Reader.TryRead(out var item))
            {
                _buffer.Add(item);
            }
            
            var finalBuffer = Interlocked.Exchange(ref _buffer, new List<T>());
            if (finalBuffer.Count > 0)
            {
                await ExecuteCallbacks(finalBuffer).ConfigureAwait(false);
            }
        }

        private async Task StartBufferFlushes()
        {
            try
            {
                while (!_stopTimedConsumer.IsCancellationRequested)
                {
                    await Task.Delay(_flushInterval, _stopTimedConsumer.Token);

                    var buffer = Interlocked.Exchange(ref _buffer, new List<T>());
                    if (buffer.Count > 0)
                    {
                        await ExecuteCallbacks(buffer).ConfigureAwait(false);
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }

            // lets make sure buffer copy from channel is finished
            await Task.Delay(100);

            var finalBuffer = Interlocked.Exchange(ref _buffer, new List<T>());
            if (finalBuffer.Count > 0)
            {
                await ExecuteCallbacks(finalBuffer).ConfigureAwait(false);
            }
        }

        private async Task ExecuteCallbacks(IList<T> buffer)
        {
            foreach (var callback in _callbacks)
            {
                try
                {
                    await callback(buffer).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        public void RegisterConsumer(Func<IList<T>, Task> callback)
        {
            _callbacks.Add(callback);
        }

        public bool TryWrite(T item)
        {
            return _channel.Writer.TryWrite(item);
        }

        public void Dispose()
        {
            _channel.Writer.Complete();

            // make sure everything is read from channel to buffer
            Thread.Sleep(100);

            _stopBufferConsumer?.Cancel();
            _stopBufferConsumer?.Dispose();

            _stopTimedConsumer?.Cancel();
            _stopTimedConsumer?.Dispose();
        }
    }
}
