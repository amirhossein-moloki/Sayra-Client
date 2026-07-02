# Roadmap - GameNet Client Implementation

---

## Phase 1 - Foundation

### Goal:
Create a running Windows Service that connects to a LAN server.

### Tasks:
- Create .NET Windows Service project
- Implement startup auto-run
- Implement WebSocket/TCP connection
- Implement heartbeat system

### Output:
Client appears online in server dashboard

---

## Phase 2 - Communication Layer

### Goal:
Enable real-time command exchange.

### Tasks:
- Implement message parser (JSON)
- Implement command dispatcher
- Implement reconnect logic
- Implement error handling

### Output:
Server can send commands to client successfully

---

## Phase 3 - System Control Layer

### Goal:
Enable control of Windows OS.

### Tasks:
- Implement Lock/Unlock functions
- Implement Restart/Shutdown
- Implement Logoff user

### Output:
Server can control PC state remotely

---

## Phase 4 - Process & Game Control

### Goal:
Control applications and games.

### Tasks:
- Process manager implementation
- Start/kill applications
- Game launcher integration
- Whitelist/blacklist system

### Output:
Server can launch and stop games remotely

---

## Phase 5 - Session System

### Goal:
Enable time-based usage control.

### Tasks:
- Session timer implementation
- Sync with server
- Auto stop session
- State management

### Output:
Time-based PC control works

---

## Phase 6 - Stability & Security

### Goal:
Make client production-ready.

### Tasks:
- Watchdog service implementation
- Auto restart on crash
- Anti-close protection
- Persistent reconnection

### Output:
Client cannot be easily terminated and remains stable

---

## Final Output
A fully functional LAN-based GameNet Client Agent ready for production use.
