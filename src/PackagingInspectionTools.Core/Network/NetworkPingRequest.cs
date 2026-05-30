namespace PackagingInspectionTools.Core.Network
{
    public sealed class NetworkPingRequest
    {
        public NetworkPingRequest(
            string target,
            int count,
            int timeoutMilliseconds,
            int bufferSize,
            int ttl,
            bool dontFragment,
            string sourceAddress)
        {
            Target = target;
            Count = count;
            TimeoutMilliseconds = timeoutMilliseconds;
            BufferSize = bufferSize;
            Ttl = ttl;
            DontFragment = dontFragment;
            SourceAddress = sourceAddress;
        }

        public string Target { get; }

        public int Count { get; }

        public int TimeoutMilliseconds { get; }

        public int BufferSize { get; }

        public int Ttl { get; }

        public bool DontFragment { get; }

        public string SourceAddress { get; }
    }
}
