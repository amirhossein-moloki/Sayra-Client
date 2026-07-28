using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Thread-safe implementation of mirror and CDN selector with automatic failover, priority and health-based routing.
    /// </summary>
    public class MirrorSelector : IMirrorSelector
    {
        private readonly List<MirrorEndpoint> _endpoints = new List<MirrorEndpoint>();
        private readonly object _lock = new object();
        private readonly IHttpClientFactory _httpClientFactory;

        public MirrorSelector(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IReadOnlyList<MirrorEndpoint> GetEndpoints()
        {
            lock (_lock)
            {
                return _endpoints.ToList();
            }
        }

        public void RegisterEndpoint(MirrorEndpoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (endpoint.BaseUri == null) throw new ArgumentException("BaseUri must be specified", nameof(endpoint));

            lock (_lock)
            {
                if (!_endpoints.Any(e => e.BaseUri.Equals(endpoint.BaseUri)))
                {
                    _endpoints.Add(endpoint);
                }
            }
        }

        public MirrorEndpoint GetBestEndpoint()
        {
            lock (_lock)
            {
                var best = _endpoints
                    .Where(e => e.IsHealthy)
                    .OrderBy(e => e.Priority)
                    .ThenBy(e => e.LastLatency)
                    .FirstOrDefault();

                if (best == null)
                {
                    throw new MirrorUnavailableException("No healthy mirror or CDN endpoints are available for downloading.");
                }

                return best;
            }
        }

        public void ReportFailure(MirrorEndpoint endpoint)
        {
            if (endpoint == null) return;

            lock (_lock)
            {
                var match = _endpoints.FirstOrDefault(e => e.BaseUri.Equals(endpoint.BaseUri));
                if (match != null)
                {
                    match.FailureCount++;
                    if (match.FailureCount >= 3)
                    {
                        match.IsHealthy = false;
                    }
                }
            }
        }

        public async Task ProbeHealthAsync(CancellationToken cancellationToken = default)
        {
            List<MirrorEndpoint> endpointsToProbe;
            lock (_lock)
            {
                endpointsToProbe = _endpoints.ToList();
            }

            if (!endpointsToProbe.Any()) return;

            using (var client = _httpClientFactory.CreateClient("MirrorProbe"))
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                var tasks = endpointsToProbe.Select(async ep =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        // Request HEAD or small request to verify endpoint connection/health
                        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, ep.BaseUri), cancellationToken);
                        sw.Stop();

                        lock (_lock)
                        {
                            var match = _endpoints.FirstOrDefault(e => e.BaseUri.Equals(ep.BaseUri));
                            if (match != null)
                            {
                                match.IsHealthy = response.IsSuccessStatusCode;
                                match.LastLatency = sw.Elapsed;
                                match.LastCheckedUtc = DateTime.UtcNow;
                                if (response.IsSuccessStatusCode)
                                {
                                    match.FailureCount = 0; // Reset failures on success
                                }
                            }
                        }
                    }
                    catch
                    {
                        sw.Stop();
                        lock (_lock)
                        {
                            var match = _endpoints.FirstOrDefault(e => e.BaseUri.Equals(ep.BaseUri));
                            if (match != null)
                            {
                                match.FailureCount++;
                                if (match.FailureCount >= 3)
                                {
                                    match.IsHealthy = false;
                                }
                                match.LastLatency = TimeSpan.FromSeconds(5); // Penalty
                                match.LastCheckedUtc = DateTime.UtcNow;
                            }
                        }
                    }
                });

                await Task.WhenAll(tasks);
            }
        }
    }
}
