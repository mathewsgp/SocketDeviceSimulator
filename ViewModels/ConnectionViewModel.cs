using System;
using System.Windows.Input;
using SocketSimulator.Commands;
using SocketSimulator.Models;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class ConnectionViewModel : ViewModelBase
    {
        private readonly SocketService _socketService;
        private readonly LoggingService _logger;

        private ConnectionMode _mode = ConnectionMode.Server;
        private string _ipAddress = "127.0.0.1";
        private int _port = 9000;
        private bool _isConnected;
        private string _connectionStatus = "Disconnected";

        public ConnectionMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    ConnectionStatus = value ? "Connected" : "Disconnected";
                }
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ToggleModeCommand { get; }

        public ConnectionViewModel()
        {
            _socketService = SocketService.Instance;
            _logger = LoggingService.Instance;

            _socketService.ConnectionChanged += OnConnectionChanged;

            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !IsConnected);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
            ToggleModeCommand = new RelayCommand(ToggleMode);
        }

        private void OnConnectionChanged(object? sender, SocketService.ConnectionChangedEventArgs e)
        {
            IsConnected = e.IsConnected;
        }

        private async System.Threading.Tasks.Task ConnectAsync()
        {
            if (Mode == ConnectionMode.Server)
            {
                var success = await _socketService.StartServerAsync(IpAddress, Port);
                if (success)
                {
                    _logger.Info("Connection", $"Started server on {IpAddress}:{Port}");
                }
            }
            else
            {
                var success = await _socketService.ConnectToServerAsync(IpAddress, Port);
                if (success)
                {
                    _logger.Info("Connection", $"Connected to {IpAddress}:{Port}");
                }
            }
        }

        private void Disconnect()
        {
            _socketService.Stop();
            _logger.Info("Connection", "Disconnected");
        }

        private void ToggleMode(object? parameter)
        {
            Mode = Mode == ConnectionMode.Server ? ConnectionMode.Client : ConnectionMode.Server;
        }

        public void ApplySettings(ConnectionSettings settings)
        {
            Mode = settings.Mode;
            IpAddress = settings.IpAddress;
            Port = settings.Port;
        }

        public ConnectionSettings GetSettings()
        {
            return new ConnectionSettings
            {
                Mode = Mode,
                IpAddress = IpAddress,
                Port = Port
            };
        }
    }
}
