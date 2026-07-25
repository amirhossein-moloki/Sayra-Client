using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Launch.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Launch.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Launch.Application.Services
{
    public class LaunchProfileProvider : ILaunchProfileProvider
    {
        private readonly ILogger<LaunchProfileProvider> _logger;

        public LaunchProfileProvider(ILogger<LaunchProfileProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<LaunchProfile> GetProfileAsync(string gameId)
        {
            _logger.LogInformation("Generating launch profile for GameId: '{GameId}'", gameId);

            // Constructing a default profile with secure boundaries, safe timeouts, and empty sandbox config.
            var profile = new LaunchProfile
            {
                GameId = gameId,
                Arguments = string.Empty,
                WorkingDirectory = string.Empty,
                Priority = "High",
                LaunchTimeoutSeconds = 30
            };

            return Task.FromResult(profile);
        }
    }
}
