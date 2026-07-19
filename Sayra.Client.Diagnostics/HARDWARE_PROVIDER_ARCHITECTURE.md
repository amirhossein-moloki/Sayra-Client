# Hardware Provider Layer Architecture Spec
## Sayra Client Desktop Subsystem (Phase 2)

This documentation provides an architectural and implementation blueprint for the **Hardware Provider Layer** of Sayra Client's Diagnostics Subsystem.

---

## 1. Provider Hierarchy & Architectural Design

The Hardware Provider Layer is designed using clean architecture patterns, ensuring high-cohesion, low-coupling, and extreme isolation from UI code and business workflows. It consists of physical data acquisition providers that run independently and report structured domain models.

### Interface & Base Structure

All physical hardware providers implement the base `IHardwareProvider` interface, providing a unified pattern for identifying, executing, and self-validating each hardware provider module:

```csharp
namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IHardwareProvider
    {
        string ProviderName { get; }
        Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
    }
}
```

Each specific hardware device corresponds to a strongly typed interface and provider:
- `ICpuProvider` -> `CpuProvider`
- `IMemoryProvider` -> `MemoryProvider`
- `IStorageProvider` -> `StorageProvider`
- `IMotherboardProvider` -> `MotherboardProvider`
- `IOperatingSystemProvider` -> `OperatingSystemProvider`
- `IDirectXProvider` -> `DirectXProvider`
- `IVulkanProvider` -> `VulkanProvider`
- `IOpenGLProvider` -> `OpenGLProvider`
- `IDisplayProvider` -> `DisplayProvider`
- `IGpuProvider` -> `GpuProvider`
- `INetworkProvider` -> `NetworkProvider`

---

## 2. Dependency Graph & Platform Isolation

```
       [ Core Diagnostics Orchestrator / Telemetry Engine ]
                              |
       +----------------------+----------------------+
       |                      |                      |
       v                      v                      v
[ ICpuProvider ]       [ IMemoryProvider ]    [ IGpuProvider ]   ... (other interfaces)
       |                      |                      |
       v                      v                      v
[ CpuProvider ]        [ MemoryProvider ]     [ GpuProvider ]    ... (concrete classes)
       \                      /                      /
        v                    v                      v
              [ IWmiProvider / Platform APIs ]
```

### Key Rules:
1. **No Provider-to-Provider Coupling**: Providers operate completely independently. A provider must never inject, reference, or call another provider.
2. **Zero Platform Leaks**: Windows-specific libraries, structures, or objects (e.g. `System.Management`, WMI `ManagementObject`, registry keys) are encapsulated entirely inside the concrete provider implementations. No platform-specific types are exposed in the public interface layer.
3. **Graceful Fallbacks**: When executing under non-Windows OS (e.g. Linux, macOS) or inside virtual machines, WMI/Registry logic is bypassed using runtime checks, reverting to safe cross-platform .NET APIs and realistic domain placeholders.

---

## 3. Provider Responsibilities & Physical Sources

| Provider | Data Collected | Source on Windows | Fallback Source |
|---|---|---|---|
| **CpuProvider** | Name, Vendor, Arch, Logical/Physical Cores, Threads, Clocks, Cache Sizes, Virt-Support, Instr-sets | WMI `Win32_Processor`, `.NET Intrinsics` | Environment statistics, core counts |
| **MemoryProvider** | Installed RAM, Available/Used RAM, speed, type, slot count, ECC, DIMM module list | WMI `Win32_PhysicalMemory` & `Win32_OperatingSystem` | System diagnostics, generic DIMMs |
| **StorageProvider** | Drive letter, volume label, filesystem, SSD/HDD indicator, capacity, used/free, health, serial | `.NET DriveInfo` & WMI `Win32_DiskDrive` | Safe DriveInfo details, generic UUIDs |
| **MotherboardProvider** | Manufacturer, product, board serial, BIOS version, release date, UEFI support, Secure Boot status | WMI `Win32_BaseBoard`/`Win32_BIOS` & Secure Boot Registry | Safe realistic BIOS placeholders |
| **OperatingSystemProvider** | OS Edition, build number, version, architecture, install date, last boot time, user details | WMI `Win32_OperatingSystem` & `.NET Environment` | Cross-platform Environment details |
| **DirectXProvider** | DirectX major/minor versions, Feature levels support status | Windows Registry & Direct3D profiles | Not Supported (non-Windows) |
| **VulkanProvider** | Vulkan loader library version, hardware support status | `vulkan-1.dll` presence in System32 | Not Supported |
| **OpenGLProvider** | OpenGL standard version, hardware support status | `opengl32.dll` presence in System32 | Not Supported |

---

## 4. Error Handling & Custom Exception Hierarchy

A robust custom exception tree is provided to cleanly isolate physical hardware access errors:

* `HardwareProviderException`: Base exception class for diagnostics layer.
* `ProviderUnavailableException`: Thrown when a hardware subsystem is disconnected, unsupported, or disabled.
* `HardwareReadException`: Thrown when physical read queries (WMI or IO exceptions) fail.
* `ValidationException`: Thrown when validation checks fail.

Additionally, to prevent system-wide crashes, providers **never throw unhandled exceptions**. All calls are wrapped in robust `try-catch` structures. On failures, warnings are logged, and fallbacks are gracefully populated.

---

## 5. Performance and Optimization

1. **Selective WMI Queries**: Providers query only the exact attributes required (e.g., `SELECT Name FROM Win32_Processor`), preventing wasteful table allocations.
2. **Execution Timing**: Each provider logs its start, completion, and total elapsed duration (measured using a high-precision `Stopwatch`) using structured logging.
3. **Startup-Caching Strategy**: Static hardware specs (Motherboard, CPU details, total storage) should be queried only once during application startup and cached. Only telemetry metrics should be actively polled.

---

## 6. Future Telemetry & Polling Integration

In future phases, the `Telemetry Engine` will orchestrate the periodic polling of performance metrics. The providers written in Phase 2 form the single trusted source of data for compiling `HardwareSpecification` and `HardwareMetrics` profiles during these subsequent integration stages.
