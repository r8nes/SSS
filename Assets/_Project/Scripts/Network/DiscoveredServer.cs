using System;

namespace ShooterShop.Network
{
    public sealed class DiscoveredServer
    {
        public long Id { get; }
        public Uri Uri { get; }
        public string Address { get; }

        public DiscoveredServer(long id, Uri uri, string address)
        {
            Id = id;
            Uri = uri;
            Address = address;
        }
    }
}
