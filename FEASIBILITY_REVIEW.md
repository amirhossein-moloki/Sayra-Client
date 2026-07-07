# Sayra Client - Product Design Feasibility Review

## 1. Product Architecture Compatibility

*   **Analysis:** The current dual-layer architecture (.NET 8 Windows Service + WPF Thin Client) is **fundamentally compatible** with the proposed UI. The use of gRPC over Named Pipes for IPC is an excellent choice for real-time state synchronization (session timers, cost updates).
*   **API Availability:**
    *   **Existing:** Session lifecycle (`START_SESSION`, `STOP_SESSION`), basic app execution (`RUN_APP`), and telemetry (CPU/RAM usage percentages).
    *   **Missing:** The `DiagnosticsService` lacks APIs for hardware model identification (e.g., "i7 13500f", "RTX 4090"). `GameModel` lacks fields for the rich metadata (screenshots, descriptions) shown in the UI designs.
*   **Conflicts:** The "Unlock PC" feature is currently a stub. Implementing a true remote unlock from a Session 0 service to an interactive user session (Session 1+) is technically complex on modern Windows and may require a Custom Credential Provider.

---

## 2. UI/UX Feasibility Review

*   **Launcher Screen:**
    *   **Feasibility:** High. The current `LauncherView.xaml` already uses a `WrapPanel` and `ItemsControl` which can be styled to match the high-fidelity tiles.
    *   **Requirement:** Needs **Right-to-Left (RTL)** support (`FlowDirection="RightToLeft"`) which is missing in the current UI codebase.
*   **Game Detail Screen:**
    *   **Feasibility:** Medium. Requires a new View and ViewModel.
    *   **Real-time Needs:** The "Session Information" panel requires a continuous stream of data from the `SessionManager`. gRPC streaming is already architected but not yet fully utilized in the UI layer for these specific fields (Current Cost, Hourly Rate).
*   **System Detail Panel:**
    *   **Technical Concern:** Collecting GPU models and Display resolution (e.g., "4k Oled") requires integration with WMI or Win32 APIs (DXGI for GPU) which is not yet implemented in `DiagnosticsService.cs`.

---

## 3. Missing Requirements Detection

*   **Hardware Discovery:** A service to poll WMI for specific hardware names (CPU/GPU) and Total Physical RAM.
*   **Localization Layer:** Support for Persian (Farsi) text rendering and Jalali calendar date formats (as seen in `1405/04/15`).
*   **Metadata Sync:** A mechanism to download and cache game screenshots and descriptions from the master server, as these should not be hardcoded.
*   **Power Management:** Implementation of `SHUTDOWN_PC` and `RESTART_PC` using `ExitWindowsEx` or `shutdown.exe` integration.

---

## 4. Technical Implementation Estimate

| Feature | Complexity | Effort | Dependencies | Risks |
| :--- | :--- | :--- | :--- | :--- |
| **Game Detail View** | Medium | 3-5 Days | Metadata API | UI performance with high-res images. |
| **Hardware Detection** | Low | 2 Days | WMI / Win32 | Inconsistent naming across vendors. |
| **RTL & Localization** | Medium | 3 Days | WPF Styles | Layout breaking in complex grids. |
| **Real-time Cost Sync** | Low | 1 Day | IPC Stream | Precision issues between Core and UI. |
| **Power Commands** | Low | 1 Day | Win32 API | Unsaved user data loss. |

---

## 5. Architecture Recommendations

1.  **Enhance `DiagnosticsService`:** Implement a hardware inventory module to fetch static specs once on startup.
2.  **Expand `SharedModels`:** Add `DetailedDescription` and `ScreenshotUrls` to `GameModel`.
3.  **Reactive UI:** Use `System.Reactive` (already a dependency) to pipe gRPC events directly into the ViewModels to ensure the timer and cost never "lag."
4.  **Shell Replacement:** For true Kiosk mode, consider setting the Sayra UI as the Windows Shell (`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell`).

---

## 6. UI Quality Review

*   **Commercial Appeal:** Very High. The design is modern, professional, and competitive with established platforms like GGLeap or Senet.
*   **Intuition:** The split between "Session Information" and "System Detail" on the game page is highly effective for user transparency.
*   **UX Improvements:** The "Play" (اجرای بازی) button is prominent. However, the "Back" button (بازگشت) in the top-left should be more visually distinct to ensure easy navigation on larger displays.

---

## 7. Final Decision

**Verdict: B) Implementable With Changes**

The product design is technically sound and fits the existing .NET 8 / WPF architecture. However, several data gaps and missing system-level implementations must be addressed before the UI can be fully functional.

### Strengths
*   Modern, high-fidelity visual design.
*   Clear separation of Core Service and UI Shell.
*   Robust IPC architecture (gRPC) already in place.

### Weaknesses
*   Missing hardware identification logic.
*   Lack of RTL/Localization support in the current UI foundation.
*   Stubbed system commands (Shutdown/Restart/Unlock).

### Recommended Roadmap
1.  **Phase 1:** Implement Hardware Discovery and expand Shared Metadata models.
2.  **Phase 2:** Implement RTL support and high-fidelity global styles in WPF.
3.  **Phase 3:** Develop the Game Detail screen and wire up real-time cost/timer gRPC streams.
4.  **Phase 4:** Finalize power management and "Unlock PC" logic for production readiness.
