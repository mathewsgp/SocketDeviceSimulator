using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SocketSimulator.Commands;
using SocketSimulator.Models;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class LogViewModel : ViewModelBase
    {
        private readonly LoggingService _logger;

        public ObservableCollection<LogEntry> LogEntries => _logger.LogEntries;

        public string LogText => string.Join("\n", LogEntries.Select(e => e.ToString()));

        public ICommand ClearCommand { get; }

        public LogViewModel()
        {
            _logger = LoggingService.Instance;
            _logger.LogEntries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogText));
            ClearCommand = new RelayCommand(Clear);
        }

        private void Clear()
        {
            _logger.Clear();
            OnPropertyChanged(nameof(LogText));
        }
    }
}
