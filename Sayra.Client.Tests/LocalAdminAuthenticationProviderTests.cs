using System;
using System.Threading.Tasks;
using Moq;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;
using Sayra.Client.Authentication.Models;
using Sayra.Client.Authentication.Providers;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.LocalAdmin.Services;
using Xunit;

namespace Sayra.Client.Tests
{
    public class LocalAdminAuthenticationProviderTests
    {
        [Fact]
        public void CanHandle_ReturnsTrue_OnlyForAdminAndAfmin()
        {
            // Arrange
            var mockLocalAdminService = new Mock<ILocalAdminService>();
            var provider = new LocalAdminAuthenticationProvider(mockLocalAdminService.Object);

            // Act & Assert
            Assert.True(provider.CanHandle("admin", "password"));
            Assert.True(provider.CanHandle("afmin", "password"));
            Assert.True(provider.CanHandle("ADMIN", "password"));
            Assert.False(provider.CanHandle("amir", "password"));
            Assert.False(provider.CanHandle(string.Empty, "password"));
            Assert.False(provider.CanHandle(null!, "password"));
        }

        [Fact]
        public async Task AuthenticateAsync_ValidLocalAdmin_ReturnsSuccessfulAuthenticationResult()
        {
            // Arrange
            var mockLocalAdminService = new Mock<ILocalAdminService>();
            mockLocalAdminService.Setup(s => s.Authenticate("admin", "correct_pass"))
                .ReturnsAsync(new AdminAuthenticationResult { Success = true });

            var provider = new LocalAdminAuthenticationProvider(mockLocalAdminService.Object);

            // Act
            var result = await provider.AuthenticateAsync("admin", "correct_pass");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AuthenticatedUser);
            Assert.Equal("admin", result.AuthenticatedUser.Username);
            Assert.Equal(UserRole.LocalAdministrator, result.AuthenticatedUser.Role);
            Assert.Contains(UserPermission.OpenAdminPanel, result.AuthenticatedUser.Permissions);
            Assert.Contains(UserPermission.ManageLibrary, result.AuthenticatedUser.Permissions);
            Assert.Equal("LocalAdmin", result.AuthenticationType);
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidLocalAdmin_ReturnsFailedAuthenticationResult()
        {
            // Arrange
            var mockLocalAdminService = new Mock<ILocalAdminService>();
            mockLocalAdminService.Setup(s => s.Authenticate("admin", "wrong_pass"))
                .ReturnsAsync(new AdminAuthenticationResult { Success = false, ErrorReason = "Invalid password." });

            var provider = new LocalAdminAuthenticationProvider(mockLocalAdminService.Object);

            // Act
            var result = await provider.AuthenticateAsync("admin", "wrong_pass");

            // Assert
            Assert.False(result.Success);
            Assert.Null(result.AuthenticatedUser);
            Assert.Equal("Invalid password.", result.FailureReason);
        }

        [Fact]
        public async Task AuthenticateAsync_ReservationProvider_AuthenticatesAmirWithPlayerRole()
        {
            // Arrange
            var provider = new ReservationAuthenticationProvider();

            // Act
            var result = await provider.AuthenticateAsync("amir", "amir");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AuthenticatedUser);
            Assert.Equal("amir", result.AuthenticatedUser.Username);
            Assert.Equal(UserRole.Player, result.AuthenticatedUser.Role);
            Assert.Contains(UserPermission.LaunchGame, result.AuthenticatedUser.Permissions);
            Assert.Equal("Reservation", result.AuthenticationType);
        }

        [Fact]
        public async Task AuthenticateAsync_OfflineProvider_AuthenticatesGuestWithGuestRole()
        {
            // Arrange
            var provider = new OfflineAuthenticationProvider();

            // Act
            var result = await provider.AuthenticateAsync("guest", "");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AuthenticatedUser);
            Assert.Equal("guest", result.AuthenticatedUser.Username);
            Assert.Equal(UserRole.Guest, result.AuthenticatedUser.Role);
            Assert.Contains(UserPermission.LaunchGame, result.AuthenticatedUser.Permissions);
            Assert.Equal("Offline", result.AuthenticationType);
        }
    }
}
