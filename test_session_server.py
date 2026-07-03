import socket
import json
import time
import threading

def handle_client(conn, addr):
    print(f"Connected by {addr}")
    try:
        # Start a thread to read from client
        def read_loop():
            f = conn.makefile('r', encoding='utf-8')
            for line in f:
                if not line:
                    break
                print(f"Received from client: {line.strip()}")

        threading.Thread(target=read_loop, daemon=True).start()

        # 1. Send START_SESSION
        start_session = {
            "type": "COMMAND",
            "action": "START_SESSION",
            "payload": {
                "sessionId": "123",
                "pcId": "PC-01",
                "duration": 1 # 1 minute for quick test
            }
        }
        conn.sendall((json.dumps(start_session) + "\n").encode('utf-8'))
        print("Sent START_SESSION")
        time.sleep(5)

        # 2. Send PAUSE_SESSION
        pause_session = {
            "type": "COMMAND",
            "action": "PAUSE_SESSION"
        }
        conn.sendall((json.dumps(pause_session) + "\n").encode('utf-8'))
        print("Sent PAUSE_SESSION")
        time.sleep(5)

        # 3. Send RESUME_SESSION
        resume_session = {
            "type": "COMMAND",
            "action": "RESUME_SESSION"
        }
        conn.sendall((json.dumps(resume_session) + "\n").encode('utf-8'))
        print("Sent RESUME_SESSION")

        print("Waiting for session to timeout (approx 1 min)...")
        time.sleep(70)

    except Exception as e:
        print(f"Error: {e}")
    finally:
        conn.close()
        print("Connection closed")

def start_server():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind(('127.0.0.1', 5000))
        s.listen()
        print("Mock server listening on 127.0.0.1:5000")
        while True:
            conn, addr = s.accept()
            handle_client(conn, addr)

if __name__ == "__main__":
    start_server()
