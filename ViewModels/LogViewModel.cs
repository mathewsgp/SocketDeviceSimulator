using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using SocketSimulator.Commands;
using SocketSimulator.Models;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class LogViewModel : ViewModelBase
    {
        private readonly LoggingService _logger;

        public ObservableCollection<LogEntry> LogEntries => _logger.LogEntries;

        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            private set => SetProperty(ref _logText, value);
        }

        public ICommand ClearCommand { get; }
        public ICommand SaveLogCommand { get; }
        public ICommand EnableLogFileCommand { get; }
        public ICommand DisableLogFileCommand { get; }
        
        private bool _isLogFileEnabled;
        public bool IsLogFileEnabled
        {
            get => _isLogFileEnabled;
            set => SetProperty(ref _isLogFileEnabled, value);
        }

        public LogViewModel()
        {
            _logger = LoggingService.Instance;
            _logger.LogEntries.CollectionChanged += OnLogEntriesChanged;
            UpdateLogText();
            ClearCommand = new RelayCommand(Clear);
            SaveLogCommand = new RelayCommand(SaveLog);
            EnableLogFileCommand = new RelayCommand(EnableLogFile);
            DisableLogFileCommand = new RelayCommand(DisableLogFile);
        }

        private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateLogText();
        }

        private void UpdateLogText()
        {
            LogText = string.Join("\n", LogEntries.Select(e => e.ToString()));
        }

        private void Clear()
        {
            _logger.Clear();
            UpdateLogText();
        }

        private void SaveLog()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".log",
                FileName = $"socket_log_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(dialog.FileName, LogEntries.Select(e => e.ToString()));
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to save log: {ex.Message}", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void EnableLogFile()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".log",
                FileName = $"socket_log_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (dialog.ShowDialog() == true)
            {
                _logger.SetLogFile(dialog.FileName);
                IsLogFileEnabled = true;
            }
        }

        private void DisableLogFile()
        {
            _logger.CloseLogFile();
            IsLogFileEnabled = false;
        }
    }
}
