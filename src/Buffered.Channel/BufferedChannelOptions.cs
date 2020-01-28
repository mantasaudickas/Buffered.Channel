using System;
using System.Threading.Channels;

namespace Buffered.Channel
{
    public class BufferedChannelOptions
    {
        public static readonly int DefaultBufferSize = 100;
        public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(10);

        public int BufferSize { get; set; }
        public TimeSpan FlushInterval { get; set; }
        public BoundedChannelOptions BoundedChannelOptions { get; set; }
    }
}
