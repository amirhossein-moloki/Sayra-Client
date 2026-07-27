using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Enums
{
    /// <summary>
    /// Specifies the packaging structure of the update archive.
    /// </summary>
    public enum PackageType
    {
        /// <summary>
        /// A complete standalone zip/archive deployment package.
        /// </summary>
        FullPackage,

        /// <summary>
        /// A differential binary patch archive package.
        /// </summary>
        DeltaPackage
    }
}
