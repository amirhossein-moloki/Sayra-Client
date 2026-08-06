using System;
using System.Collections.Generic;
using System.Linq;
using Sayra.Client.Shared.GameDistribution.Cache.Models;
using Sayra.Client.Shared.GameDistribution.Selection.Interfaces;

namespace Sayra.Client.Shared.GameDistribution.Selection.Services
{
    public class CacheNodeSelector : ICacheNodeSelector
    {
        public CacheNode? SelectBestNode(IEnumerable<CacheNode> nodes)
        {
            if (nodes == null) return null;

            CacheNode? bestNode = null;
            double bestScore = -double.MaxValue;

            foreach (var node in nodes)
            {
                double score = CalculateNodeScore(node);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestNode = node;
                }
            }

            return bestNode;
        }

        private double CalculateNodeScore(CacheNode node)
        {
            double score = 0;

            // 1. SSD Priority: +50 points
            if (node.IsSsd)
            {
                score += 50.0;
            }

            // 2. Available Space (GB): +1 point per GB up to 50 GB
            double freeGb = node.FreeStorageBytes / (1024.0 * 1024.0 * 1024.0);
            score += Math.Min(freeGb, 50.0);

            // 3. Network Speed (Mbps): +1 point per 10 Mbps up to 500 Mbps (+50 max)
            score += Math.Min(node.NetworkSpeedMbps / 10.0, 50.0);

            // 4. CPU Load: Subtract load penalty. Lower load gets higher score (+50 max)
            score += (100.0 - Math.Clamp(node.CpuLoadPercent, 0, 100)) * 0.5;

            // 5. Cache Completeness: +0.5 point per 1% up to 100% (+50 max)
            score += Math.Clamp(node.CacheCompletenessPercent, 0, 100) * 0.5;

            // 6. Health Score: direct addition (+100 max)
            score += Math.Clamp(node.HealthScore, 0, 100);

            return score;
        }
    }
}
