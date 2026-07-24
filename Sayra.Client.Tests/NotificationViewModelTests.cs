using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using Moq;
using Sayra.Client.Shared.Models;
using Sayra.UI.Notifications.Services;
using Sayra.UI.Notifications.ViewModels;
using Xunit;

namespace Sayra.Client.Tests
{
    public class NotificationViewModelTests
    {
        [Fact]
        public void CardViewModel_ShouldInitialize_Correctly()
        {
            // Arrange
            var payload = new NotificationPayload
            {
                Id = Guid.NewGuid(),
                Title = "Session expiring",
                Body = "Only 10 minutes left.",
                Priority = NotificationPriority.HIGH,
                Category = NotificationCategory.BILLING,
                ActionCallbackToken = "EXTEND_SESSION",
                Signature = "valid_sig"
            };

            var mockIpc = new Mock<NotificationIpcClient>();
            var mockLogger = new Mock<Sayra.Client.Shared.Interfaces.IAuditLogger>();
            var ackService = new NotificationAcknowledgementService(mockIpc.Object, mockLogger.Object);
            var mockHandler = new Mock<INotificationActionHandler>();

            // Act
            var cardVm = new NotificationCardViewModel(payload, ackService, mockHandler.Object, null);

            // Assert
            Assert.Equal("Session expiring", cardVm.Title);
            Assert.Equal("Only 10 minutes left.", cardVm.Body);
            Assert.Equal("HIGH", cardVm.PriorityText);
            Assert.Equal("BILLING", cardVm.CategoryText);
            Assert.True(cardVm.HasAction);
            Assert.Equal("تمدید نشست", cardVm.ActionText);
        }

        [Fact]
        public async Task CardViewModel_ExecuteAction_ShouldInvokeHandlerAndDismiss()
        {
            // Arrange
            var payload = new NotificationPayload
            {
                Id = Guid.NewGuid(),
                Title = "Session Warning",
                Body = "Extend session now.",
                ActionCallbackToken = "EXTEND_SESSION_1H",
                Signature = "sig"
            };

            var mockIpc = new Mock<NotificationIpcClient>();
            var ackService = new NotificationAcknowledgementService(mockIpc.Object);
            var mockHandler = new Mock<INotificationActionHandler>();
            bool isDismissedCalled = false;

            var cardVm = new NotificationCardViewModel(payload, ackService, mockHandler.Object, vm => isDismissedCalled = true);

            // Act
            await cardVm.ExecuteActionCommand.ExecuteAsync(null);

            // Assert
            mockHandler.Verify(x => x.HandleActionAsync(payload, "EXTEND_SESSION_1H"), Times.Once);
            Assert.True(isDismissedCalled);
        }
    }
}
