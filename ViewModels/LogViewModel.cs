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
        public ICommand OpenLogFolderCommand { get; }
        public ICommand OpenLatestLogCommand { get; }
        
        public bool IsLogFileEnabled => _logger.AutoSaveEnabled;

        public LogViewModel()
        {
            _logger = LoggingService.Instance;
            _logger.LogEntries.CollectionChanged += OnLogEntriesChanged;
            _logger.PropertyChanged += (s, e) => 
            {
                if (e.PropertyName == nameof(LoggingService.AutoSaveEnabled))
                    OnPropertyChanged(nameof(IsLogFileEnabled));
            };
            UpdateLogText();
            ClearCommand = new RelayCommand(Clear);
            SaveLogCommand = new RelayCommand(SaveLog);
            EnableLogFileCommand = new RelayCommand(EnableLogFile);
            DisableLogFileCommand = new RelayCommand(DisableLogFile);
            OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
            OpenLatestLogCommand = new RelayCommand(OpenLatestLog);
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
                OnPropertyChanged(nameof(IsLogFileEnabled));
            }
        }

        private void DisableLogFile()
        {
            _logger.CloseLogFile();
            OnPropertyChanged(nameof(IsLogFileEnabled));
        }

        private void OpenLogFolder()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SocketDeviceSimulator");
                System.Diagnostics.Process.Start("explorer.exe", appDataPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open folder: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void OpenLatestLog()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SocketDeviceSimulator");
                
                if (Directory.Exists(appDataPath))
                {
                    var latestLog = Directory.GetFiles(appDataPath, "*.log")
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .FirstOrDefault();
                    
                    if (latestLog != null)
                    {
                        System.Diagnostics.Process.Start("notepad.exe", latestLog);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("No log files found.", "Info",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open log: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
