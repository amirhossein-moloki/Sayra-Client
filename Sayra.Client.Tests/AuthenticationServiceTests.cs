using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;
using Sayra.Client.Authentication.Events;
using Sayra.Client.Authentication.Exceptions;
using Sayra.Client.Authentication.Models;
using Sayra.Client.Authentication.Services;
using Sayra.Client.Authentication.Providers;
using Xunit;

namespace Sayra.Client.Tests
{
    public class AuthenticationServiceTests
    {
        [Fact]
        public async Task AuthenticateAsync_ValidCredentials_SetsUserContextAndRaisesSuccessEvent()
        {
            // Arrange
            var mockProvider = new Mock<IAuthenticationProvider>();
            mockProvider.Setup(p => p.ProviderName).Returns("TestProvider");
            mockProvider.Setup(p => p.CanHandle("testuser", "password")).Returns(true);

            var testUser = new AuthenticatedUser(
                "123",
                "testuser",
                "Test User",
                UserRole.Player,
                new[] { UserPermission.LaunchGame },
                null,
                null,
                "fa",
                "Dark",
                null
            );

            mockProvider.Setup(p => p.AuthenticateAsync("testuser", "password"))
                .ReturnsAsync(AuthenticationResult.CreateSuccess(testUser, "TestProvider"));

            var providers = new List<IAuthenticationProvider> { mockProvider.Object };
            var userContext = new UserContext();
            var service = new AuthenticationService(providers, userContext);

            bool startedRaised = false;
            bool succeededRaised = false;
            string? eventSessionId = null;

            service.AuthenticationStarted += (s, e) => { startedRaised = true; };
            service.AuthenticationSucceeded += (s, e) =>
            {
                succeededRaised = true;
                eventSessionId = e.SessionId;
            };

            // Act
            var result = await service.AuthenticateAsync("testuser", "password");

            // Assert
            Assert.True(result.Success);
            Assert.True(startedRaised);
            Assert.True(succeededRaised);
            Assert.NotNull(eventSessionId);
            Assert.Equal(AuthenticationState.Authenticated, service.GetAuthenticationState());
            Assert.True(userContext.IsAuthenticated);
            Assert.Equal("testuser", userContext.Username);
            Assert.Equal(UserRole.Player, userContext.Role);
        }

        [Fact]
        public async Task AuthenticateAsync_InvalidCredentials_RaisesFailedEventAndReturnsFailure()
        {
            // Arrange
            var mockProvider = new Mock<IAuthenticationProvider>();
            mockProvider.Setup(p => p.ProviderName).Returns("TestProvider");
            mockProvider.Setup(p => p.CanHandle("testuser", "wrong")).Returns(true);
            mockProvider.Setup(p => p.AuthenticateAsync("testuser", "wrong"))
                .ReturnsAsync(AuthenticationResult.CreateFailure("Incorrect password."));

            var providers = new List<IAuthenticationProvider> { mockProvider.Object };
            var userContext = new UserContext();
            var service = new AuthenticationService(providers, userContext);

            bool failedRaised = false;
            string? failureReason = null;

            service.AuthenticationFailed += (s, e) =>
            {
                failedRaised = true;
                failureReason = e.Reason;
            };

            // Act
            var result = await service.AuthenticateAsync("testuser", "wrong");

            // Assert
            Assert.False(result.Success);
            Assert.True(failedRaised);
            Assert.Equal("Incorrect password.", failureReason);
            Assert.Equal(AuthenticationState.Unauthenticated, service.GetAuthenticationState());
            Assert.False(userContext.IsAuthenticated);
        }

        [Fact]
        public async Task LogoutAsync_ClearsUserContextAndRaisesLogoutEvents()
        {
            // Arrange
            var mockProvider = new Mock<IAuthenticationProvider>();
            mockProvider.Setup(p => p.ProviderName).Returns("TestProvider");
            mockProvider.Setup(p => p.CanHandle("testuser", "password")).Returns(true);

            var testUser = new AuthenticatedUser(
                "123",
                "testuser",
                "Test User",
                UserRole.Player,
                new[] { UserPermission.LaunchGame },
                null,
                null,
                "fa",
                "Dark",
                null
            );

            mockProvider.Setup(p => p.AuthenticateAsync("testuser", "password"))
                .ReturnsAsync(AuthenticationResult.CreateSuccess(testUser, "TestProvider"));

            var providers = new List<IAuthenticationProvider> { mockProvider.Object };
            var userContext = new UserContext();
            var service = new AuthenticationService(providers, userContext);

            // Populate context
            await service.AuthenticateAsync("testuser", "password");

            bool logoutStarted = false;
            bool logoutCompleted = false;

            service.LogoutStarted += (s, e) => { logoutStarted = true; };
            service.LogoutCompleted += (s, e) => { logoutCompleted = true; };

            // Act
            await service.LogoutAsync();

            // Assert
            Assert.True(logoutStarted);
            Assert.True(logoutCompleted);
            Assert.False(userContext.IsAuthenticated);
            Assert.Equal(AuthenticationState.Unauthenticated, service.GetAuthenticationState());
        }

        [Fact]
        public void AuthorizationService_HasPermission_And_HasRole_WorksCorrectly()
        {
            // Arrange
            var userContext = new UserContext();
            var authz = new AuthorizationService(userContext);

            var user = new AuthenticatedUser(
                "1",
                "admin",
                "Admin",
                UserRole.LocalAdministrator,
                new[] { UserPermission.OpenAdminPanel, UserPermission.ManageLibrary },
                null,
                null,
                "fa",
                "Dark",
                null
            );

            // Act & Assert (Before Authentication)
            Assert.False(authz.HasPermission(UserPermission.OpenAdminPanel));
            Assert.False(authz.HasRole(UserRole.LocalAdministrator));

            // Populate Context
            userContext.SetUser(user, "Local");

            // Act & Assert (After Authentication)
            Assert.True(authz.HasPermission(UserPermission.OpenAdminPanel));
            Assert.True(authz.HasPermission(UserPermission.ManageLibrary));
            Assert.False(authz.HasPermission(UserPermission.ManageUsers));

            Assert.True(authz.HasRole(UserRole.LocalAdministrator));
            Assert.True(authz.HasRole(UserRole.Guest)); // local admin is hierarchically above guest
            Assert.False(authz.HasRole(UserRole.Administrator));
        }
    }
}
