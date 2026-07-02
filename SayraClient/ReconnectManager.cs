using Microsoft.Extensions.Logging;

namespace SayraClient;

public class ReconnectManager
{
    private readonly ILogger<ReconnectManager> _logger;
    private readonly int _baseDelayMilliseconds;
    private readonly int _maxDelayMilliseconds;
    private int _retryCount;

    public ReconnectManager(ILogger<ReconnectManager> logger, int baseDelayMilliseconds = 2000, int maxDelayMilliseconds = 30000)
    {
        _logger = logger;
        _baseDelayMilliseconds = baseDelayMilliseconds;
        _maxDelayMilliseconds = maxDelayMilliseconds;
        _retryCount = 0;
    }

    public async Task WaitForNextRetry(CancellationToken cancellationToken)
    {
        _retryCount++;
        int delay = Math.Min(_baseDelayMilliseconds * (int)Math.Pow(2, _retryCount - 1), _maxDelayMilliseconds);

        _logger.LogInformation("Waiting {delay}ms before next reconnection attempt (retry #{count})...", delay, _retryCount);

        await Task.Delay(delay, cancellationToken);
    }

    public void Reset()
    {
        if (_retryCount > 0)
        {
            _logger.LogInformation("Reconnection successful, resetting retry counter.");
            _retryCount = 0;
        }
    }
}
