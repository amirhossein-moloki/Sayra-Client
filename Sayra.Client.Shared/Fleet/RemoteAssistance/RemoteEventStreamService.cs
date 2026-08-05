using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.Fleet.RemoteAssistance
{
    /// <summary>
    /// Thread-safe event distribution stream allowing pub-sub of live telemetry, system and application events.
    /// </summary>
    public class RemoteEventStreamService : IRemoteEventStreamService
    {
        private readonly RemoteSessionCoordinator _coordinator;
        private readonly ILogger<RemoteEventStreamService> _logger;

        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _sessionEventQueues = new();

        /// <summary>
        /// Initializes a new instance of RemoteEventStreamService.
        /// </summary>
        public RemoteEventStreamService(
            RemoteSessionCoordinator coordinator,
            ILogger<RemoteEventStreamService> logger)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<string> StreamEventsAsync(
            string sessionId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));

            _logger.LogInformation("Event streaming subscription established for session '{Id}'", sessionId);

            var q = _sessionEventQueues.GetOrAdd(sessionId, _ => new ConcurrentQueue<string>());

            // Seed introductory event
            q.Enqueue($"{{\"Event\":\"TelemetryStreamAttached\",\"SessionId\":\"{sessionId}\",\"Timestamp\":\"{DateTime.UtcNow:O}\"}}");

            while (!ct.IsCancellationRequested)
            {
                _coordinator.KeepAlive(sessionId);

                if (q.TryDequeue(out var ev))
                {
                    yield return ev;
                }
                else
                {
                    // Non-blocking wait interval
                    await Task.Delay(200, ct);
                }
            }
        }

        /// <inheritdoc />
        public Task PublishEventAsync(string sessionId, string eventJson, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (string.IsNullOrEmpty(eventJson)) throw new ArgumentNullException(nameof(eventJson));

            _coordinator.KeepAlive(sessionId);

            var q = _sessionEventQueues.GetOrAdd(sessionId, _ => new ConcurrentQueue<string>());
            q.Enqueue(eventJson);

            _logger.LogDebug("Published event JSON to session '{Id}' queue. Size={Size}", sessionId, q.Count);
            return Task.CompletedTask;
        }
    }
}
