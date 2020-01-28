using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Buffered.Channel.Tests
{
    class Program
    {
        private static int _producedCount;
        private static int _consumedCount;
        private static int _consumerOps;

        static async Task Main(string[] args)
        {
            var cancellation = new CancellationTokenSource();

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                var options = new BufferedChannelOptions
                {
                    BufferSize = 1000,
                    FlushInterval = TimeSpan.FromSeconds(1)
                };

                using var collection = new BufferedChannel<Message>(options);
                collection.RegisterConsumer(Consume);

                const int producerCount = 11;

                for (int i = 0; i < producerCount; i++)
                {
#pragma warning disable 4014
                    Task.Factory.StartNew(() => Produce(collection, cancellation.Token), CancellationToken.None);
#pragma warning restore 4014
                }

                await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
                
                Console.WriteLine("Cancelling producer task..");
                cancellation.Cancel();
                cancellation.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadLine();
            }

            Console.WriteLine($"Time spent: {timer.Elapsed}");
            Console.WriteLine($"Produced: {_producedCount}");
            Console.WriteLine($"Consumed: {_consumedCount}");
            Console.WriteLine($"Operations: {_consumerOps}");

            Console.ReadLine();
        }

        private static Task Consume(IList<Message> list)
        {
            Interlocked.Add(ref _consumerOps, 1);
            Interlocked.Add(ref _consumedCount, list.Count);
            return Task.CompletedTask;
        }

        private static void Produce(BufferedChannel<Message> collection, CancellationToken cancellationToken)
        {
            try
            {
                var message = new Message { Content = "" };
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (collection.TryWrite(message))
                    {
                        Interlocked.Add(ref _producedCount, 1);
                    }
                }
            }
            finally
            {
                Console.WriteLine("producer finished..");
            }
        }
    }

    public class Message
    {
        public string Content { get; set; }
    }
}
