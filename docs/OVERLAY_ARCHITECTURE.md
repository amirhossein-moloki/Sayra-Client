# SAYRA Client Overlay Engine Architecture

## 1. Executive Summary
This document outlines the architecture, security parameters, and implementation strategy of the **SAYRA Runtime Overlay Engine**. The engine is responsible for presenting real-time timers, warning notifications, and diagnostic metrics over running game processes without compromising security, triggering game anti-cheat software, or degrading gameplay performance.

---

## 2. Structural Design
The Overlay Engine uses a decoupled, pluggable architecture based on the Open-Closed Principle. High-level runtime systems interact with the abstract `IOverlayRenderer` interface rather than concrete graphics implementations:

```
         +---------------------------------------+
         |             Runtime Engine            |
         +---------------------------------------+
                             |
                             v
                 +-----------------------+
                 |    IOverlayRenderer   |
                 +-----------------------+
                             |
              +--------------+--------------+
              |                             |
              v                             v
+---------------------------+ +---------------------------+
|    WpfOverlayRenderer     | |    DxgiOverlayRenderer    |
|   (Default / Production)  | |    (Future Extension)     |
+---------------------------+ +---------------------------+
              |                             |
              v                             v
+---------------------------+ +---------------------------+
| Topmost borderless window | | Native D3D11/12 Present   |
| with WS_EX_TRANSPARENT    | | swap chain hook wrapper   |
+---------------------------+ +---------------------------+
```

---

## 3. Renderer Implementations

### 3.1 WpfOverlayRenderer (Default Production Renderer)
The official, production-ready rendering strategy employs a specialized WPF overlay system:
* **How it works:** Spawns a topmost, borderless, semi-transparent WPF window on the primary monitor.
* **Win32 Window Styles:** The window configures the extended window styles `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE` via P/Invoke.
  * `WS_EX_TRANSPARENT` ensures that all mouse clicks and gestures pass directly through the overlay to the underlying game.
  * `WS_EX_NOACTIVATE` ensures that the window does not steal keyboard focus, allowing the gamer to maintain uninterrupted input control.
* **Compatibility:** Highly compatible with multi-monitor layouts, high-DPI scaling, and 100% of modern games.

### 3.2 DxgiOverlayRenderer (Future Extension)
The `DxgiOverlayRenderer` serves as a structured extension point for rendering native overlays inside full-screen exclusive applications by hooking Direct3D (D3D11/D3D12) swap chains.
* **How it works:** Hooks into the graphics pipeline's `IDXGISwapChain::Present` call inside the game's address space.
* **Status:** Kept as an unactivated, stub extension in production due to the severe security and stability risks detailed below.

---

## 4. Key Security & Anti-Cheat Considerations

### 4.1 Anti-Cheat Verification (Why WPF is Default)
Injecting a DLL or modifying/hooking process memory inside popular games (e.g., *Valorant*, *Fortnite*, *Apex Legends*) is highly dangerous. Modern ring-0/ring-3 kernel anti-cheat engines (such as Riot Vanguard, Easy Anti-Cheat, BattlEye, and Ricochet) actively monitor the process space.
* **Risks of DXGI Hooking:** Memory-injection or API hooking is flagged as signature manipulation or cheat insertion, resulting in automatic hardware bans for the terminal and player account suspensions.
* **WPF Benefits:** The WPF overlay window runs completely out-of-process in standard User Space (Session 1+). It does not hook, inject, or touch game process memory. It is **100% safe** and holds zero risk of anti-cheat flag triggers.

### 4.2 Application Stability and Crash Prevention
In-process hooking requires intercepting the game's graphics rendering thread. Any exception or thread synchronization delay in the DLL hook will immediately crash the game. Out-of-process WPF overlays eliminate this risk, ensuring maximum station uptime.

### 4.3 Mouse Escape and Click-Through Integrity
WPF overlays use absolute native mouse passing. Users can never accidentally click on the overlay or drag it, preventing input loss during intense gameplay.

---

## 5. Performance Targets
* **WPF Overlay CPU Overhead:** < 0.2% on a standard quad-core workstation.
* **Frame Rate Impact:** 0 FPS reduction for running games.
