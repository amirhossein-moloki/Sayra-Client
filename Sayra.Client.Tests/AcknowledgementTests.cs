using System;
using System.Threading.Tasks;
using Moq;
using Sayra.Client.Shared.Models;
using Sayra.UI.Notifications.Services;
using Xunit;

namespace Sayra.Client.Tests
{
    public class AcknowledgementTests
    {
        [Fact]
        public async Task AcknowledgementService_ShouldReportTransitions_Correctly()
        {
            // Arrange
            var mockIpc = new Mock<NotificationIpcClient>();
            var ackService = new NotificationAcknowledgementService(mockIpc.Object);
            var notificationId = Guid.NewGuid();

            // Act & Assert
            // 1. Received
            await ackService.ReportReceivedAsync(notificationId);
            mockIpc.Verify(x => x.SendAckAsync(It.Is<NotificationAckPayload>(p =>
                p.NotificationId == notificationId && p.Status == NotificationAckStatus.Received)), Times.Once);

            // 2. Displayed
            await ackService.ReportDisplayedAsync(notificationId);
            mockIpc.Verify(x => x.SendAckAsync(It.Is<NotificationAckPayload>(p =>
                p.NotificationId == notificationId && p.Status == NotificationAckStatus.Displayed)), Times.Once);

            // 3. Dismissed
            await ackService.ReportDismissedAsync(notificationId);
            mockIpc.Verify(x => x.SendAckAsync(It.Is<NotificationAckPayload>(p =>
                p.NotificationId == notificationId && p.Status == NotificationAckStatus.Dismissed)), Times.Once);
        }
    }
}
