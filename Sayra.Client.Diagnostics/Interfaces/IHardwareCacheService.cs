using System;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces
{
    public interface IHardwareCacheService
    {
        HardwareSpecification? Get();
        void Set(HardwareSpecification spec);
        bool IsExpired();
        void Clear();
    }
}
