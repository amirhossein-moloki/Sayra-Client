# PHASE 8 — STAGE 4: Enterprise Distributed Tracing Platform
## Technical & Architectural Documentation

## 1. Architectural Overview

Distributed Tracing inside the SAYRA Enterprise Windows Client tracks operation execution spans and parent/child relationship hierarchies across asynchronous boundaries and workstation-to-UI boundaries (Named Pipe IPC).

The distributed tracing engine is decoupled from concrete transports and structured as a lightweight, thread-safe, non-blocking managed service registered in the Dependency Injection container.

### Core Architecture Components

```
+───────────────────────────────────────────────────────────────────────────+
|                          SAYRA UI APPLICATION                             |
|                                                                           |
|   [Using/Await Scopes]                                                     |
|            │                                                              |
|            ▼                                                              |
|   [IpcClientBridge] / [NotificationIpcClient]                             |
|            │                                                              |
|            │ (Propagates TraceId & CorrelationId inside JSON Payload)     |
+────────────┼──────────────────────────────────────────────────────────────+
             │
             │ Windows Named Pipe (SayraClientIpcPipe)
             │
+────────────┼──────────────────────────────────────────────────────────────+
|            ▼                                                              |
|   [IpcServer (Named Pipe Listener)]                                       |
|            │                                                              |
|            │ (Extracts trace context and creates new child ambient scope)  |
|            ▼                                                              |
|   [TracingService (ITracingService)] <───> [TracingContext (Static legacy)]|
|            │                                                              |
|            │ (AsyncLocal Context Storage & Nested Span Management)        |
|            ▼                                                              |
|   [TraceScope] (Track duration, handle exceptions, auto-restore parent)    |
|                                                                           |
|                          SAYRA BACKGROUND CORE HOST                       |
+───────────────────────────────────────────────────────────────────────────+
```

1. **`ITracingService`**: The abstraction defining distributed tracing contracts, ambient context retrieval, explicit correlation creation, and scoped tracing block instantiation.
2. **`TracingService`**: Concrete singleton implementation utilizing `AsyncLocal<TraceContext?>` to flow trace propagation down through nested `async/await` tasks and thread transitions without bleeding horizontally.
3. **`TraceScope`**: A disposable (`IDisposable` and `IAsyncDisposable`) boundary wrapping started scopes. Automatically captures execution duration via high-precision `Stopwatch`, handles exceptions, and restores the parent context upon disposal.
4. **`TracingContext`**: Static legacy compatibility bridge. `TracingService` automatically synchronizes current `TraceId` and `CorrelationId` to `TracingContext` on every context transition, ensuring pre-existing components (e.g., `AuditLogger`) receive correlation telemetry without code refactoring.
5. **Context Propagation Layer**: Handles boundary transitions through IPC messages. Outgoing `IpcMessage` instances serialize ambient `TraceId` and `CorrelationId`. Incoming handlers in `IpcServer` extract them to build child tracing scopes.

---

## 2. Trace Context Fields

Every trace execution context maintains the following telemetry:

* **`TraceId`**: The globally unique transaction identifier (generated on root span start or inherited).
* **`CorrelationId`**: The logical activity chain identifier used for correlating distinct logs and events.
* **`OperationId`**: The unique identifier of the specific operation span (generated per trace scope).
* **`ParentOperationId`**: The OperationId of the parent span, forming a structured DAG (Directed Acyclic Graph) of operations.
* **`MachineId`**: Machine hostname where execution occurred.
* **`SessionId`**: The active user session ID, if applicable.
* **`UserId`**: The logged-on user ID, if applicable.
* **`CenterId`**: The target gaming center ID, if applicable.
* **`Latency`**: High-precision timespan of execution (set on disposal).
* **`Result`**: The final status (`Success`, `Failed`, `Timeout`, `Aborted`).
* **`Exception`**: Non-sensitive truncated exception summary in case of failures.

---

## 3. Operations Sequence Diagrams

### 3.1 Nested Scopes & Lifetime Management

The sequence below illustrates starting a nested span sequentially or inside parallel tasks.

```text
Caller Code                    TracingService                 TraceScope (Parent)              TraceScope (Child)
    │                                │                                │                               │
    ├───── CreateScopeAsync() ──────>│                                │                               │
    │                              [StartTraceAsync()]                │                               │
    │                              [Verify Nesting Depth]             │                               │
    │                              [Increment Depth]                  │                               │
    │                              [Sync TracingContext]              │                               │
    │                                │                                │                               │
    │<──── Return ParentScope ───────┤                                │                               │
    │                                                                 │                               │
    │───── Execute Task Work ────────────────────────────────────────>│                               │
    │                                                                 │                               │
    │                                ├───── CreateScopeAsync(child) ─>│                               │
    │                                │                                │                               │
    │                                │                                ├────── CreateScopeAsync() ────>│
    │                                │                                │                             [StartTraceAsync()]
    │                                │                                │                             [Inherit Trace/CorrId]
    │                                │                                │                             [Increment Depth]
    │                                │                                │                               │
    │                                │                                │<───── Return ChildScope ──────┤
    │                                                                 │                               │
    │─────────────────────────────────────────────────────────────────┼──────────────────────────────>│ (Run Work)
    │                                                                 │                               │
    │                                                                 │<───── Exception Encounters ───┤ (Fails)
    │                                                                 │                               │
    │                                                                 │───── CaptureException() ─────>│ [State=Failed]
    │                                                                 │                               │
    │                                                                 │───── Dispose() (or Async) ───>│
    │                                                                 │                             [Stop Timer]
    │                                                                 │                             [EndTraceAsync()]
    │                                                                 │                             [Decrement Depth]
    │                                                                 │                             [Restore Parent Context]
    │                                                                 │<──────────────────────────────┤
    │                                                                 │
    │<──── Dispose() ─────────────────────────────────────────────────┤
    │      [Stop Timer]                                               │
    │      [EndTraceAsync()]                                          │
    │      [Restore Null Context]                                     │
    ▼                                                                 ▼
```

---

## 4. Integration Guidance

For subsequent observatory modules (Performance Monitor, Diagnostics, Alert Engine):

1. **Performance Monitor Integration**:
   Consume `TraceCompletedEvent` by registering an event handler in the container:
   ```csharp
   eventDispatcher.RegisterHandler<TraceCompletedEvent>(evt => {
       if (evt.Duration.TotalMilliseconds > warningThreshold) {
           // Log high latency transaction metrics
       }
   });
   ```

2. **Diagnostics Integration**:
   Collect active snapshots of long-running active `TraceContext` hierarchies. Since `TracingService` manages parent identifiers, diagnostics can render real-time transaction trees.

3. **Alert Engine Integration**:
   Inspect exceptions or timeouts (`TraceResult.Timeout`) raised from core subsystem loops and generate critical/emergency infrastructure alerts.

---

### End of Documentation
