import socket
import json
import time
import base64
import hashlib
import hmac
import threading
from datetime import datetime, timezone
from Crypto.Cipher import AES as Aes
from Crypto.Util.Padding import pad, unpad
from Crypto.Random import get_random_bytes

import os

# Configuration
MASTER_KEY_BASE64 = os.environ.get("SAYRA_MASTER_KEY")
if not MASTER_KEY_BASE64:
    print("Error: SAYRA_MASTER_KEY environment variable not set.")
    exit(1)
MASTER_KEY = base64.b64decode(MASTER_KEY_BASE64)

class SecureServer:
    def __init__(self, conn):
        self.conn = conn
        self.session_key = None
        self._buffer = b""

    def send_raw(self, data):
        self.conn.sendall((json.dumps(data) + "\n").encode('utf-8'))

    def receive_raw(self):
        while b"\n" not in self._buffer:
            chunk = self.conn.recv(4096)
            if not chunk:
                return None
            self._buffer += chunk

        line_bytes, self._buffer = self._buffer.split(b"\n", 1)
        line = line_bytes.decode('utf-8')
        if not line: return self.receive_raw()
        return json.loads(line)

    def authenticate(self):
        print("Starting authentication handshake...")
        challenge = base64.b64encode(get_random_bytes(32)).decode('utf-8')
        self.send_raw({"type": "AUTH_CHALLENGE", "challenge": challenge})

        while True:
            response = self.receive_raw()
            if not response: return False
            if response.get("type") == "AUTH_RESPONSE":
                break
            print(f"Ignoring non-auth message: {response.get('type')}")

        # Verify response HMAC
        expected_hash = hmac.new(MASTER_KEY, challenge.encode('utf-8'), hashlib.sha256).digest()
        received_hash = base64.b64decode(response["response"])

        if not hmac.compare_digest(expected_hash, received_hash):
            print("Auth HMAC verification failed")
            self.send_raw({"type": "AUTH_STATUS", "status": "FAILED", "message": "Invalid HMAC"})
            return False

        # Decrypt session key
        encrypted_sk_full = base64.b64decode(response["session_key"])
        iv = encrypted_sk_full[:16]
        ciphertext = encrypted_sk_full[16:]

        cipher = Aes.new(MASTER_KEY, Aes.MODE_CBC, iv=iv)
        self.session_key = unpad(cipher.decrypt(ciphertext), Aes.block_size)

        print(f"Authenticated! Session key: {base64.b64encode(self.session_key).decode('utf-8')}")
        self.send_raw({"type": "AUTH_STATUS", "status": "SUCCESS"})
        return True

    def send_secure(self, message):
        plaintext = json.dumps(message)

        # Encrypt
        iv = get_random_bytes(16)
        cipher = Aes.new(self.session_key, Aes.MODE_CBC, iv=iv)
        encrypted_payload = iv + cipher.encrypt(pad(plaintext.encode('utf-8'), Aes.block_size))
        encrypted_payload_b64 = base64.b64encode(encrypted_payload).decode('utf-8')

        # Sign
        timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f0") + "Z"

        message_to_sign = f"{timestamp}|{encrypted_payload_b64}"
        sig = hmac.new(self.session_key, message_to_sign.encode('utf-8'), hashlib.sha256).digest()
        sig_b64 = base64.b64encode(sig).decode('utf-8')

        secure_msg = {
            "payload": encrypted_payload_b64,
            "signature": sig_b64,
            "timestamp": timestamp
        }
        self.send_raw(secure_msg)

    def receive_secure(self):
        wrapped = self.receive_raw()
        if not wrapped: return None

        payload_b64 = wrapped["payload"]
        sig_b64 = wrapped["signature"]
        timestamp = wrapped["timestamp"]

        # Verify Signature
        message_to_verify = f"{timestamp}|{payload_b64}"
        expected_sig = hmac.new(self.session_key, message_to_verify.encode('utf-8'), hashlib.sha256).digest()
        if not hmac.compare_digest(expected_sig, base64.b64decode(sig_b64)):
            print("Secure message signature verification failed")
            return None

        # Decrypt
        encrypted_payload = base64.b64decode(payload_b64)
        iv = encrypted_payload[:16]
        ciphertext = encrypted_payload[16:]
        cipher = Aes.new(self.session_key, Aes.MODE_CBC, iv=iv)
        plaintext = unpad(cipher.decrypt(ciphertext), Aes.block_size).decode('utf-8')

        return json.loads(plaintext)

def handle_client(conn, addr):
    print(f"Connected by {addr}")
    server = SecureServer(conn)
    try:
        if not server.authenticate():
            return

        # Send a secure command
        time.sleep(1)
        print("Sending secure GET_DIAGNOSTICS command...")
        server.send_secure({"type": "COMMAND", "action": "GET_DIAGNOSTICS"})

        # Listen for secure responses or heartbeats
        while True:
            msg = server.receive_secure()
            if msg is None: break
            print(f"Received secure message: {msg}")
            if msg.get("action") == "GET_DIAGNOSTICS":
                print("Diagnostics received successfully!")

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
        print("Test server listening on 127.0.0.1:5000")
        while True:
            conn, addr = s.accept()
            threading.Thread(target=handle_client, args=(conn, addr)).start()

if __name__ == "__main__":
    start_server()
