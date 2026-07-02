# Project Specification - GameNet Client System

## 1. Project Name
Sayra Client

---

## 2. Project Goal
Develop a Windows-based background service that acts as a client in a LAN-based GameNet management system.

The client must:
- Connect to a local server over LAN
- Receive real-time commands
- Control Windows PC operations
- Execute games and applications
- Enforce session-based restrictions

---

## 3. System Context

The system consists of:
- 1 Local Server (Master PC)
- Multiple Client PCs (this project)

The client runs on each PC and is fully controlled by the server.

---

## 4. Core Responsibilities

The client must:

### System Control
- Lock / Unlock Windows session
- Restart / Shutdown PC
- Logoff current user

### Session Execution
- Start session timer
- Sync session state with server
- Auto terminate session

### Application Control
- Launch games/programs
- Kill processes
- Prevent unauthorized apps

### Communication
- Maintain persistent connection to server
- Send status updates
- Receive real-time commands

---

## 5. Constraints
- Must work offline (LAN only)
- Must run as Windows Service
- Must be always active
- Must auto-reconnect if disconnected
- Must be lightweight and stable

---

## 6. Non-Goals
- No UI dependency
- No cloud dependency
- No user interaction interface
