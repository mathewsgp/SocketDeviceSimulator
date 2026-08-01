using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SocketSimulator.Models;

namespace SocketSimulator.Services
{
    public class SocketService : INotifyPropertyChanged
    {
        private static SocketService? _instance;
        public static SocketService Instance => _instance ??= new SocketService();

        private TcpListener? _server;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _receiveTask;
        private bool _isConnected;
        private readonly LoggingService _logger = LoggingService.Instance;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataReceivedEventArgs>? DataReceived;
        public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

        public class DataReceivedEventArgs : EventArgs
        {
            public string Data { get; }
            public DataReceivedEventArgs(string data) => Data = data;
        }

        public class ConnectionChangedEventArgs : EventArgs
        {
            public bool IsConnected { get; }
            public ConnectionChangedEventArgs(bool isConnected) => IsConnected = isConnected;
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged(nameof(IsConnected));
                }
            }
        }

        private SocketService() { }

        public async Task<bool> StartServerAsync(string ipAddress, int port)
        {
            try
            {
                Stop();
                
                var ip = IPAddress.Parse(ipAddress);
                _server = new TcpListener(ip, port);
                _server.Start();
                
                _logger.Info("Socket", $"Server started on {ipAddress}:{port}");
                IsConnected = true;
                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(true));

                _cancellationTokenSource = new CancellationTokenSource();
                _ = AcceptClientsAsync(_cancellationTokenSource.Token);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Socket", $"Failed to start server: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConnectToServerAsync(string ipAddress, int port)
        {
            try
            {
                Stop();

                _client = new TcpClient();
                await _client.ConnectAsync(ipAddress, port);
                _stream = _client.GetStream();
                
                _logger.Info("Socket", $"Connected to {ipAddress}:{port}");
                IsConnected = true;
                ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(true));

                _cancellationTokenSource = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveLoop(_stream, _cancellationTokenSource.Token));
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Socket", $"Failed to connect: {ex.Message}");
                return false;
            }
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _server != null)
            {
                try
                {
                    var client = await _server.AcceptTcpClientAsync(cancellationToken);
                    _logger.Info("Socket", $"Client connected from {client.Client.RemoteEndPoint}");
                    
                    var stream = client.GetStream();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _ = ReceiveLoop(stream, _cancellationTokenSource.Token);
                    
                    // Keep reference to the client
                    _client = client;
                    _stream = stream;
                    IsConnected = true;
                    ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(true));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("Socket", $"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task ReceiveLoop(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var messageBuffer = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    if (stream.DataAvailable)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                        if (bytesRead == 0)
                        {
                            _logger.Info("Socket", "Connection closed by remote");
                            break;
                        }

                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuffer.Append(data);

                        // Process complete messages (newline delimited)
                        var content = messageBuffer.ToString();
                        while (content.Contains('\n'))
                        {
                            var newlineIndex = content.IndexOf('\n');
                            var message = content.Substring(0, newlineIndex).Trim('\r');
                            messageBuffer.Remove(0, newlineIndex + 1);
                            content = messageBuffer.ToString();

                            _logger.Info("Socket", $"RX: {message}", message);
                            DataReceived?.Invoke(this, new DataReceivedEventArgs(message));
                        }
                    }
                    else
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("Socket", $"Receive error: {ex.Message}");
                    break;
                }
            }

            IsConnected = false;
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(false));
            _logger.Info("Socket", "Connection closed");
        }

        public async Task<bool> SendAsync(string data)
        {
            if (_stream == null || !IsConnected)
            {
                _logger.Warning("Socket", "Cannot send: not connected");
                return false;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(data + "\n");
                await _stream.WriteAsync(bytes);
                _logger.Info("Socket", $"TX: {data}", data);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Socket", $"Send error: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            try
            {
                _stream?.Close();
                _stream?.Dispose();
            }
            catch { }
            _stream = null;

            try
            {
                _client?.Close();
                _client?.Dispose();
            }
            catch { }
            _client = null;

            try
            {
                _server?.Stop();
            }
            catch { }
            _server = null;

            IsConnected = false;
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(false));
            _logger.Info("Socket", "Connection stopped");
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
