using System.Collections.ObjectModel;
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

        public ICommand ClearCommand { get; }

        public LogViewModel()
        {
            _logger = LoggingService.Instance;
            ClearCommand = new RelayCommand(Clear);
        }

        private void Clear()
        {
            _logger.Clear();
        }
    }
}
