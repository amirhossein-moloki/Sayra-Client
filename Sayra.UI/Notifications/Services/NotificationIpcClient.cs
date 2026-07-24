using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Ipc;
using Sayra.Client.Shared.Models;

namespace Sayra.UI.Notifications.Services
{
    public class NotificationIpcClient : IDisposable
    {
        private const string PipeName = "SayraClientIpcPipe";
        private NamedPipeClientStream? _clientStream;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public event Action<NotificationPayload>? NotificationReceived;
        public event Action<bool>? ConnectionStateChanged;

        public bool IsConnected => _clientStream?.IsConnected == true;

        public NotificationIpcClient()
        {
            Start();
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ConnectLoopAsync(_cts.Token));
        }

        private async Task ConnectLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _clientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await _clientStream.ConnectAsync(3000, ct);

                    _writer = new StreamWriter(_clientStream) { AutoFlush = true };
                    var reader = new StreamReader(_clientStream);

                    ConnectionStateChanged?.Invoke(true);

                    while (!ct.IsCancellationRequested && _clientStream.IsConnected)
                    {
                        var line = await reader.ReadLineAsync(ct);
                        if (line == null) break;

                        var message = JsonSerializer.Deserialize<IpcMessage>(line);
                        if (message != null && message.MessageType == IpcMessageType.NOTIFICATION_RECEIVED)
                        {
                            var payload = JsonSerializer.Deserialize<NotificationPayload>(message.Payload ?? "{}");
                            if (payload != null)
                            {
                                NotificationReceived?.Invoke(payload);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    ConnectionStateChanged?.Invoke(false);
                }
                finally
                {
                    Cleanup();
                }

                if (!ct.IsCancellationRequested)
                {
                    await Task.Delay(2000, ct); // Reconnect interval
                }
            }
        }

        public async Task SendAckAsync(NotificationAckPayload ack)
        {
            if (ack == null) return;

            await _writeLock.WaitAsync();
            try
            {
                if (_writer != null && _clientStream?.IsConnected == true)
                {
                    var msg = new IpcMessage
                    {
                        MessageType = IpcMessageType.NOTIFICATION_ACK,
                        Payload = JsonSerializer.Serialize(ack)
                    };
                    await _writer.WriteLineAsync(JsonSerializer.Serialize(msg));
                    await _writer.FlushAsync();
                }
            }
            catch (Exception)
            {
                // Failed to send ACK, will be retried or logged.
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private void Cleanup()
        {
            _writer?.Dispose();
            _writer = null;
            _clientStream?.Dispose();
            _clientStream = null;
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            Cleanup();
        }
    }
}
