using System;

namespace Sayra.Client.Authentication.Models
{
    public class AuthenticationResult
    {
        public bool Success { get; }
        public string? FailureReason { get; }
        public AuthenticatedUser? AuthenticatedUser { get; }
        public string? AuthenticationType { get; }
        public bool RequiresPasswordChange { get; }
        public bool RequiresServerConnection { get; }
        public bool RequiresSynchronization { get; }

        private AuthenticationResult(
            bool success,
            string? failureReason,
            AuthenticatedUser? authenticatedUser,
            string? authenticationType,
            bool requiresPasswordChange,
            bool requiresServerConnection,
            bool requiresSynchronization)
        {
            Success = success;
            FailureReason = failureReason;
            AuthenticatedUser = authenticatedUser;
            AuthenticationType = authenticationType;
            RequiresPasswordChange = requiresPasswordChange;
            RequiresServerConnection = requiresServerConnection;
            RequiresSynchronization = requiresSynchronization;
        }

        public static AuthenticationResult CreateSuccess(
            AuthenticatedUser authenticatedUser,
            string authenticationType,
            bool requiresPasswordChange = false,
            bool requiresServerConnection = false,
            bool requiresSynchronization = false)
        {
            if (authenticatedUser == null)
                throw new ArgumentNullException(nameof(authenticatedUser));
            if (string.IsNullOrEmpty(authenticationType))
                throw new ArgumentNullException(nameof(authenticationType));

            return new AuthenticationResult(
                success: true,
                failureReason: null,
                authenticatedUser: authenticatedUser,
                authenticationType: authenticationType,
                requiresPasswordChange: requiresPasswordChange,
                requiresServerConnection: requiresServerConnection,
                requiresSynchronization: requiresSynchronization);
        }

        public static AuthenticationResult CreateFailure(
            string failureReason,
            bool requiresServerConnection = false,
            bool requiresSynchronization = false)
        {
            if (string.IsNullOrEmpty(failureReason))
                throw new ArgumentNullException(nameof(failureReason));

            return new AuthenticationResult(
                success: false,
                failureReason: failureReason,
                authenticatedUser: null,
                authenticationType: null,
                requiresPasswordChange: false,
                requiresServerConnection: requiresServerConnection,
                requiresSynchronization: requiresSynchronization);
        }
    }
}
