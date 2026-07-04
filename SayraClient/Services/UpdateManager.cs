using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;

namespace SayraClient.Services;

public class UpdateManager : BackgroundService
{
    private readonly ILogger<UpdateManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _currentVersion;
    private readonly HttpClient _httpClient;

    public UpdateManager(ILogger<UpdateManager> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _currentVersion = typeof(UpdateManager).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool autoUpdate = _configuration.GetValue<bool>("UpdateConfig:AutoUpdate", true);
        int checkIntervalMinutes = _configuration.GetValue<int>("UpdateConfig:CheckIntervalMinutes", 60);

        if (!autoUpdate)
        {
            _logger.LogInformation("Auto-update is disabled.");
            return;
        }

        _logger.LogInformation("UpdateManager started. Current version: {version}", _currentVersion);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates.");
            }

            await Task.Delay(TimeSpan.FromMinutes(checkIntervalMinutes), stoppingToken);
        }
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        string updateUrl = _configuration["UpdateConfig:UpdateUrl"] ?? "http://127.0.0.1:5000/api/updates";
        _logger.LogDebug("Checking for updates at {url}...", updateUrl);

        try
        {
            // In a real scenario, this would be:
            // var response = await _httpClient.GetAsync($"{updateUrl}?current={_currentVersion}", cancellationToken);
            // if (response.IsSuccessStatusCode) { ... parse JSON ... }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Update check failed: {message}", ex.Message);
        }
    }

    public async Task<bool> ForceUpdateAsync(string downloadUrl, string expectedChecksum, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Force update requested from {url}", downloadUrl);

        try
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "sayra_update.zip");

            _logger.LogInformation("Downloading update...");
            var data = await _httpClient.GetByteArrayAsync(downloadUrl, cancellationToken);
            await File.WriteAllBytesAsync(tempPath, data, cancellationToken);

            if (!VerifyChecksum(tempPath, expectedChecksum))
            {
                _logger.LogError("Checksum verification failed for update package.");
                return false;
            }

            _logger.LogInformation("Update package verified. Ready for installation.");

            // Trigger external update script/utility
            // In a commercial product, we'd spawn a separate process here and exit.

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform update.");
            return false;
        }
    }

    private bool VerifyChecksum(string filePath, string expectedChecksum)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return hashString.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
    }
}
