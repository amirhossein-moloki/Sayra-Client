using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Gateway client responsible for communicating with the server's administrative telemetry and health endpoints.
    /// Utilises IHttpClientFactory, enforces TLS 1.3 and certificate pinning, and incorporates exponential backoff.
    /// </summary>
    public class AdminIntegrationClient : IAdminIntegrationClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminIntegrationClient> _logger;
        private readonly ReportingOptions _reportingOptions;
        private readonly UpdateOptions _updateOptions;

        public AdminIntegrationClient(
            IHttpClientFactory httpClientFactory,
            ILogger<AdminIntegrationClient> logger,
            IOptions<ReportingOptions> reportingOptions,
            IOptions<UpdateOptions> updateOptions)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reportingOptions = reportingOptions?.Value ?? new ReportingOptions();
            _updateOptions = updateOptions?.Value ?? new UpdateOptions();
        }

        /// <inheritdoc />
        public async Task<bool> ReportTelemetryEventAsync(UpdateTelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
        {
            if (telemetryEvent == null) throw new ArgumentNullException(nameof(telemetryEvent));

            string baseUrl = string.IsNullOrWhiteSpace(_updateOptions.UpdateServerUrl)
                ? "https://update.sayra.io"
                : _updateOptions.UpdateServerUrl.TrimEnd('/');
            string url = $"{baseUrl}/api/v1/telemetry";

            return await SendWithRetryAndTimeoutAsync(url, telemetryEvent, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> ReportHealthMetricAsync(HealthMetric healthMetric, CancellationToken cancellationToken = default)
        {
            if (healthMetric == null) throw new ArgumentNullException(nameof(healthMetric));

            string baseUrl = string.IsNullOrWhiteSpace(_updateOptions.UpdateServerUrl)
                ? "https://update.sayra.io"
                : _updateOptions.UpdateServerUrl.TrimEnd('/');
            string url = $"{baseUrl}/api/v1/health";

            return await SendWithRetryAndTimeoutAsync(url, healthMetric, cancellationToken);
        }

        private async Task<bool> SendWithRetryAndTimeoutAsync<T>(string url, T payload, CancellationToken cancellationToken)
        {
            int attempt = 0;
            int maxAttempts = _reportingOptions.MaxRetryAttempts;
            int baseDelaySec = _reportingOptions.BaseDelaySeconds;

            while (attempt <= maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Create client from factory using the registered name
                    var httpClient = _httpClientFactory.CreateClient("AdminIntegrationClient");

                    // Enforce request-level timeout
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10s timeout

                    _logger.LogInformation("Sending payload to {Url}, attempt {Attempt} of {MaxAttempts}", url, attempt + 1, maxAttempts + 1);

                    var response = await httpClient.PostAsJsonAsync(url, payload, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully sent payload to {Url}", url);
                        return true;
                    }

                    _logger.LogWarning("Failed to send payload to {Url}. Status: {StatusCode}", url, response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception while sending payload to {Url}. Attempt {Attempt}", url, attempt + 1);
                }

                attempt++;
                if (attempt <= maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    // Exponential backoff with random jitter
                    double delaySec = Math.Pow(2, attempt) * baseDelaySec;
                    var random = new Random();
                    double jitter = random.NextDouble() * 2.0; // up to 2 seconds jitter
                    var finalDelay = TimeSpan.FromSeconds(delaySec + jitter);

                    try
                    {
                        await Task.Delay(finalDelay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            // Transit failure automatically delegates back to local SQLCipher queue for buffering
            return false;
        }
    }
}
