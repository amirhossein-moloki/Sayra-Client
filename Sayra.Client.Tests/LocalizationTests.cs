using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models;
using Sayra.UI.Notifications.ViewModels;
using Sayra.UI.Notifications.Services;
using Moq;
using Xunit;

namespace Sayra.Client.Tests
{
    public class LocalizationTests
    {
        [Fact]
        public void CardViewModel_ShouldResolvePersianFallbackTranslations_WhenAppIsNull()
        {
            // Arrange
            var payload = new NotificationPayload
            {
                Id = Guid.NewGuid(),
                Title = "Alert",
                Body = "Notice",
                ActionCallbackToken = "EXTEND_SESSION_1H",
                Signature = "sig"
            };

            var mockIpc = new Mock<NotificationIpcClient>();
            var ackService = new NotificationAcknowledgementService(mockIpc.Object);
            var mockHandler = new Mock<INotificationActionHandler>();

            // Act
            var cardVm = new NotificationCardViewModel(payload, ackService, mockHandler.Object, null);

            // Assert
            Assert.Equal("تمدید نشست", cardVm.ActionText);
        }

        [Fact]
        public void CardViewModel_ShouldSupportVaryingTokens()
        {
            // Arrange
            var mockIpc = new Mock<NotificationIpcClient>();
            var ackService = new NotificationAcknowledgementService(mockIpc.Object);
            var mockHandler = new Mock<INotificationActionHandler>();

            var payload1 = new NotificationPayload { ActionCallbackToken = "CONFIRM_SHUTDOWN", Signature = "sig" };
            var payload2 = new NotificationPayload { ActionCallbackToken = "ACCEPT_UPDATE", Signature = "sig" };

            // Act
            var cardVm1 = new NotificationCardViewModel(payload1, ackService, mockHandler.Object, null);
            var cardVm2 = new NotificationCardViewModel(payload2, ackService, mockHandler.Object, null);

            // Assert
            Assert.Equal("تایید خاموشی", cardVm1.ActionText);
            Assert.Equal("قبول بروزرسانی", cardVm2.ActionText);
        }
    }
}
