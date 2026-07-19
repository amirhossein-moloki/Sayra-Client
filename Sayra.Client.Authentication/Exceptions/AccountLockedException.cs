using System;

namespace Sayra.Client.Authentication.Exceptions
{
    public class AccountLockedException : AuthenticationException
    {
        public AccountLockedException() : base() { }

        public AccountLockedException(string message) : base(message) { }

        public AccountLockedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
