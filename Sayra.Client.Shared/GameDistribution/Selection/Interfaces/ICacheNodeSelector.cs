using System.Collections.Generic;
using Sayra.Client.Shared.GameDistribution.Cache.Models;

namespace Sayra.Client.Shared.GameDistribution.Selection.Interfaces
{
    public interface ICacheNodeSelector
    {
        CacheNode? SelectBestNode(IEnumerable<CacheNode> nodes);
    }
}
