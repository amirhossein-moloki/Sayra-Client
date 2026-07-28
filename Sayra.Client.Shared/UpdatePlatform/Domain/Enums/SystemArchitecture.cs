using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Specifies the target system architecture of the package.
    /// </summary>
    public enum SystemArchitecture
    {
        /// <summary>
        /// 32-bit Intel/AMD architecture.
        /// </summary>
        X86,

        /// <summary>
        /// 64-bit Intel/AMD architecture.
        /// </summary>
        X64,

        /// <summary>
        /// 64-bit ARM architecture.
        /// </summary>
        Arm64
    }
}
