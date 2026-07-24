using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sayra.Client.OfflineQueue;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;
using SayraClient.Services;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Sayra.Client.Tests
{
    public class NotificationSystemTests
    {
        [Fact]
        public void NotificationPayload_Validation_ShouldVerifyRequiredFields()
        {
            // Arrange
            var payload = new NotificationPayload
            {
                Id = Guid.Empty, // Invalid
                Title = "",
                Body = "",
                Signature = ""
            };

            // Act
            bool isValid = payload.Validate(out string error);

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(error);
        }

        [Fact]
        public void NotificationPayload_Validation_ShouldSucceed_WhenValid()
        {
            // Arrange
            var payload = new NotificationPayload
            {
                Id = Guid.NewGuid(),
                Title = "Test Title",
                Body = "Test Body",
                Signature = "valid_sig_123"
            };

            // Act
            bool isValid = payload.Validate(out string error);

            // Assert
            Assert.True(isValid);
            Assert.Empty(error);
        }

        [Fact]
        public void NotificationPayload_ExpiryCheck_ShouldReturnTrue_WhenExpired()
        {
            // Arrange
            var payload = new NotificationPayload
            {
                Id = Guid.NewGuid(),
                Title = "Alert",
                Body = "Expired alert",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                TtlSeconds = 120, // 2 minutes TTL
                Signature = "sig"
            };

            // Act
            bool isExpired = payload.IsExpired();

            // Assert
            Assert.True(isExpired);
        }
    }
}
