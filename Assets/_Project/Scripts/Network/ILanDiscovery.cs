using System;
using System.Collections.Generic;

namespace ShooterShop.Network
{
    public interface ILanDiscovery
    {
        event Action ServersChanged;
        IReadOnlyCollection<DiscoveredServer> Servers { get; }
        void StartSearch();
        void StopSearch();
        void Advertise();
    }
}
