using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using SocketSimulator.Commands;
using SocketSimulator.Models;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class ConsoleViewModel : ViewModelBase
    {
        private readonly SocketService _socketService;
        private readonly LoggingService _logger;
        private readonly ProtocolService _protocolService;

        private string _commandText = string.Empty;
        private string _lastResponse = string.Empty;

        public ObservableCollection<ConsoleEntry> History { get; } = new();

        public string CommandText
        {
            get => _commandText;
            set => SetProperty(ref _commandText, value);
        }

        public string LastResponse
        {
            get => _lastResponse;
            set => SetProperty(ref _lastResponse, value);
        }

        public ICommand SendCommandCommand { get; }
        public ICommand SendResponseCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public ConsoleViewModel()
        {
            _socketService = SocketService.Instance;
            _logger = LoggingService.Instance;
            _protocolService = ProtocolService.Instance;

            _socketService.DataReceived += OnDataReceived;

            SendCommandCommand = new AsyncRelayCommand(SendCommandAsync);
            SendResponseCommand = new AsyncRelayCommand(SendResponseAsync);
            ClearHistoryCommand = new RelayCommand(ClearHistory);
        }

        private void OnDataReceived(object? sender, SocketService.DataReceivedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                History.Add(new ConsoleEntry
                {
                    Type = ConsoleEntryType.Received,
                    Message = e.Data,
                    Timestamp = System.DateTime.Now
                });
                LastResponse = e.Data;
            });
        }

        private async Task SendCommandAsync()
        {
            if (string.IsNullOrWhiteSpace(CommandText))
                return;

            var command = _protocolService.SubstituteVariables(CommandText);
            var success = await _socketService.SendAsync(command);

            History.Add(new ConsoleEntry
            {
                Type = ConsoleEntryType.Sent,
                Message = command,
                Timestamp = System.DateTime.Now,
                Success = success
            });

            CommandText = string.Empty;
        }

        private async Task SendResponseAsync()
        {
            if (string.IsNullOrWhiteSpace(CommandText))
                return;

            var response = _protocolService.SubstituteVariables(CommandText);
            var success = await _socketService.SendAsync(response);

            History.Add(new ConsoleEntry
            {
                Type = ConsoleEntryType.Response,
                Message = response,
                Timestamp = System.DateTime.Now,
                Success = success
            });

            CommandText = string.Empty;
        }

        private void ClearHistory()
        {
            History.Clear();
            LastResponse = string.Empty;
        }
    }

    public class ConsoleEntry
    {
        public ConsoleEntryType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public System.DateTime Timestamp { get; set; }
        public bool Success { get; set; } = true;

        public string TypePrefix => Type switch
        {
            ConsoleEntryType.Sent => "TX>",
            ConsoleEntryType.Received => "RX>",
            ConsoleEntryType.Response => "RS>",
            _ => ">"
        };
    }

    public enum ConsoleEntryType
    {
        Sent,
        Received,
        Response
    }
}
