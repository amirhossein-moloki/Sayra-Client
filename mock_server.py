import socket
import json
import time
import threading

def handle_client(conn, addr):
    print(f"Connected by {addr}")
    try:
        # Send PING message with newline framing
        ping_msg = json.dumps({"type": "PING"}) + "\n"
        conn.sendall(ping_msg.encode('utf-8'))
        print("Sent PING")

        # Use makefile to read line by line
        f = conn.makefile('r', encoding='utf-8')
        for line in f:
            if not line:
                break

            msg_str = line.strip()
            print(f"Received from client: {msg_str}")

            try:
                msg = json.loads(msg_str)
                if msg.get("type") == "PONG":
                    print("Confirmed: Received PONG from client!")
                elif msg.get("type") == "HEARTBEAT":
                    print("Confirmed: Received HEARTBEAT from client!")
            except json.JSONDecodeError:
                print(f"Error decoding JSON: {msg_str}")

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
        conn, addr = s.accept()
        handle_client(conn, addr)

if __name__ == "__main__":
    start_server()
