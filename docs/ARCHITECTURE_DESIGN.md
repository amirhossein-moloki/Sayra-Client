# Sayra Client - Architecture Design Document

## 1. System Architecture

The Sayra Client is designed with a strict decoupling between the system-level control logic and the user interface. This ensures that the workstation remains secure and managed even if the UI process is terminated, crashes, or is bypassed.

### 1.1 Layer Separation

*   **Core Service Layer (.NET 8 Windows Service)**
    *   **Authority:** The "Single Source of Truth" for the client state.
    *   **Responsibilities:** TCP communication with the master server, AES-256 encryption/decryption, session timing, process monitoring, kiosk lockdown (registry/policy enforcement), and hardware-level diagnostics.
    *   **Lifecycle:** Runs in Session 0; starts automatically with Windows; independent of user login.

*   **UI Layer (WPF .NET 8)**
    *   **Role:** A "Thin Client" shell for user interaction.
    *   **Responsibilities:** Rendering the game launcher, displaying session timers, handling user login (if applicable), and providing a localized interface for site-specific news or branding.
    *   **Architecture:** MVVM (Model-View-ViewModel).
    *   **Lifecycle:** Runs in the user's interactive session (Session 1+); launched and monitored by the Core Service.

### 1.2 Inter-Process Communication (IPC)

*   **Mechanism:** **gRPC over Named Pipes**.
*   **Rationale:**
    *   **Performance:** Extremely low latency (<1ms local overhead), meeting the <10ms requirement.
    *   **Streaming:** Supports bi-directional streaming for real-time state synchronization (Core pushes state changes to UI).
    *   **Strong Typing:** Protobuf contracts ensure both layers remain in sync during development.
    *   **Security:** Named Pipes can be secured using Windows Access Control Lists (ACLs) to ensure only the authorized UI process can communicate with the Service.

---

## 2. IPC Design Details

### 2.1 Communication Flow

*   **Core → UI (Server Streaming):**
    *   Continuous stream of `ClientState` updates.
    *   Session heartbeat (seconds remaining, current cost).
    *   System notifications (e.g., "Server Maintenance in 5 minutes").
*   **UI → Core (Unary Calls):**
    *   Request to launch a specific application.
    *   Request to end or pause session (if permitted).
    *   UI Readiness signal (sent when WPF is fully loaded).

### 2.2 Transport & Reliability

*   **Format:** Protobuf (Binary) for performance; JSON can be used for logging/debugging.
*   **Threading Model:**
    *   **Core:** Non-blocking asynchronous handlers using `IAsyncStreamReader/Writer`.
    *   **UI:** Reactive updates using `INotifyPropertyChanged` or `Observable` patterns to ensure UI thread remains responsive.
*   **Reliability:**
    *   The UI process will attempt to reconnect to the Named Pipe automatically if the service restarts.
    *   Core Service monitors the UI process PID; if UI crashes, Core attempts a restart.

---

## 3. Project Structure

The solution follows a modular structure to facilitate testing and clear ownership of logic.

### Solution: `Sayra.Client.sln`

*   **`Sayra.Client.Core` (Console/Service)**
    *   `Services/`: Network, Session, Kiosk, Process, Security.
    *   `Handlers/`: TCP Message handlers, Command executors.
    *   `Infrastructure/`: Windows Service host, Background Workers.
*   **`Sayra.Client.UI` (WPF Application)**
    *   `Views/`: XAML Windows and Controls.
    *   `ViewModels/`: MVVM logic, IPC Client integration.
    *   `Resources/`: Themes, Styles, Assets.
*   **`Sayra.Client.Shared` (Class Library)**
    *   `Models/`: Shared DTOs (aligned with openapi.yaml).
    *   `Constants/`: Status codes, Registry keys, Policy names.
    *   `Interfaces/`: Logging and common service abstractions.
*   **`Sayra.Client.IPC` (Class Library)**
    *   `Protos/`: `.proto` definition files.
    *   Generated gRPC base classes and clients.

---

## 4. State Management Model

The Core Service manages the authoritative `ClientState`. The UI is a reactive observer of this state.

### 4.1 ClientState Lifecycle

1.  **STARTING:** Core service initializing, loading local configuration.
2.  **CONNECTING:** Attempting to establish TCP handshake with the LAN Server.
3.  **AUTHENTICATING:** Performing HMAC-SHA256 challenge-response.
4.  **READY:** Connected and authenticated. Workstation is LOCKED (Kiosk mode active).
5.  **IN_SESSION:** User has started a session. UI is unlocked; game launcher is active.
6.  **LOCKED:** Session ended or paused. Kiosk mode re-enforced.
7.  **DISCONNECTED:** LAN connection lost. Local grace period or lockdown triggered.

### 4.2 State Synchronization

*   **Push Model:** Whenever `ClientStateManager` transitions, a `StateChanged` event is fired in the Core.
*   **IPC Stream:** The IPC Service catches the event and writes the new state to the gRPC stream.
*   **UI Reactivity:** The UI's `MainViewModel` observes the stream and triggers View transitions (e.g., switching from "Locked Overlay" to "Game Launcher").

---

## 5. System Lifecycle Flow

1.  **BOOT:** Windows starts.
2.  **SERVICE START:** `SayraClient` Windows Service starts (Core Layer).
3.  **CONNECT:** Core attempts TCP connection to Server IP (Port 5000).
4.  **AUTH:** Secure handshake using `SAYRA_MASTER_KEY` and `SessionKey` rotation.
5.  **SYNC:** Core sends `CLIENT_CONNECTED` event. Server responds with authoritative session state.
6.  **READY:** Core enforces Kiosk mode (Disable TaskMgr, etc.). Core launches UI Process.
7.  **SESSION:**
    *   Server sends `START_SESSION`.
    *   Core updates `SessionManager`.
    *   Core notifies UI via IPC.
    *   UI displays launcher.
8.  **LOCK:**
    *   Session expires or `STOP_SESSION` received.
    *   Core kills running games.
    *   Core re-enforces lockdown.
    *   UI switches to "Session Ended" screen.
9.  **RECOVERY:**
    *   If Core crashes, SCM restarts it.
    *   Core reads `session_state.json`.
    *   Core re-establishes Server connection and syncs state.

---

## 6. Design Rules

*   **Production-Grade:** Heavy use of Structured Logging (Serilog) and Error Auditing.
*   **Scalability:** Core logic is asynchronous to handle high-frequency telemetry and server commands without blocking.
*   **Low Latency:** Named Pipes ensure IPC overhead does not impact UI responsiveness or game performance.
*   **SOLID:** Services are injected via Dependency Injection (Microsoft.Extensions.DependencyInjection).
*   **Security:** Kiosk enforcement is handled by the high-privilege Service, not the low-privilege UI.
