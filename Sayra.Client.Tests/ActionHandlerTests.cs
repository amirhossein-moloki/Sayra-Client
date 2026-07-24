using System;
using System.Threading.Tasks;
using Moq;
using Sayra.Client.Shared.Models;
using Sayra.UI.Notifications.Services;
using Xunit;

namespace Sayra.Client.Tests
{
    public class ActionHandlerTests
    {
        [Fact]
        public async Task ActionHandler_ShouldFail_WhenTokenIsMismatch()
        {
            // Arrange
            var mockIpc = new Mock<NotificationIpcClient>();
            var ackService = new NotificationAcknowledgementService(mockIpc.Object);
            var handler = new NotificationActionHandler(ackService);

            var payload = new NotificationPayload
            {
                Id = Guid.NewGuid(),
                ActionCallbackToken = "CONFIRM_SHUTDOWN",
                Signature = "sig"
            };

            // Act
            await handler.HandleActionAsync(payload, "WRONG_TOKEN");

            // Assert
            mockIpc.Verify(x => x.SendAckAsync(It.Is<NotificationAckPayload>(p => p.Status == NotificationAckStatus.Failed)), Times.Once);
        }

        [Fact]
        public async Task ActionHandler_ShouldSucceed_WhenTokenMatches()
        {
            // Arrange
            var mockIpc = new Mock<NotificationIpcClient>();
            var ackService = new NotificationAcknowledgementService(mockIpc.Object);
            var handler = new NotificationActionHandler(ackService);

            var payload = new NotificationPayload
            {
                Id = Guid.NewGuid(),
                ActionCallbackToken = "CONFIRM_SHUTDOWN",
                Signature = "sig"
            };

            // Act
            await handler.HandleActionAsync(payload, "CONFIRM_SHUTDOWN");

            // Assert
            mockIpc.Verify(x => x.SendAckAsync(It.Is<NotificationAckPayload>(p =>
                p.Status == NotificationAckStatus.Clicked && p.ActionToken == "CONFIRM_SHUTDOWN")), Times.Once);
        }
    }
}
