using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery
{
    public class LoopDetector
    {
        private readonly ConcurrentDictionary<string, List<DateTime>> _failures = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<DateTime>> _recoveries = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, bool> _escalated = new(StringComparer.OrdinalIgnoreCase);

        public void RecordFailure(string subsystemName)
        {
            _failures.AddOrUpdate(subsystemName,
                _ => new List<DateTime> { DateTime.UtcNow },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(DateTime.UtcNow);
                        // Clean up entries older than 1 hour to prevent leak
                        var cutoff = DateTime.UtcNow.AddHours(-1);
                        list.RemoveAll(t => t < cutoff);
                    }
                    return list;
                });
        }

        public void RecordRecovery(string subsystemName)
        {
            _recoveries.AddOrUpdate(subsystemName,
                _ => new List<DateTime> { DateTime.UtcNow },
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(DateTime.UtcNow);
                        var cutoff = DateTime.UtcNow.AddHours(-1);
                        list.RemoveAll(t => t < cutoff);
                    }
                    return list;
                });
        }

        public bool IsCooldownActive(string subsystemName, CooldownPolicy policy, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (_cooldowns.TryGetValue(subsystemName, out var cooldownEnd))
            {
                if (DateTime.UtcNow < cooldownEnd)
                {
                    remaining = cooldownEnd - DateTime.UtcNow;
                    return true;
                }
                else
                {
                    _cooldowns.TryRemove(subsystemName, out _);
                }
            }

            // Check if threshold is breached in the evaluation window
            if (_failures.TryGetValue(subsystemName, out var list))
            {
                lock (list)
                {
                    var windowStart = DateTime.UtcNow - policy.EvaluationWindow;
                    int count = list.Count(t => t >= windowStart);
                    if (count >= policy.FailureThreshold)
                    {
                        var end = DateTime.UtcNow + policy.CooldownDuration;
                        _cooldowns[subsystemName] = end;
                        remaining = policy.CooldownDuration;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsEscalated(string subsystemName)
        {
            return _escalated.TryGetValue(subsystemName, out var isEsc) && isEsc;
        }

        public void MarkEscalated(string subsystemName)
        {
            _escalated[subsystemName] = true;
        }

        public void Reset(string subsystemName)
        {
            _failures.TryRemove(subsystemName, out _);
            _recoveries.TryRemove(subsystemName, out _);
            _cooldowns.TryRemove(subsystemName, out _);
            _escalated.TryRemove(subsystemName, out _);
        }
    }
}
