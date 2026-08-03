using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using SocketSimulator.Models;

namespace SocketSimulator.Services
{
    public class LoggingService : INotifyPropertyChanged
    {
        private static LoggingService? _instance;
        public static LoggingService Instance => _instance ??= new LoggingService();

        private readonly ObservableCollection<LogEntry> _logEntries = new();
        private readonly object _lock = new();
        private StreamWriter? _logFileWriter;
        private string _logFilePath = string.Empty;
        private bool _autoSaveEnabled;

        public ObservableCollection<LogEntry> LogEntries => _logEntries;
        
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                if (_autoSaveEnabled != value)
                {
                    _autoSaveEnabled = value;
                    if (value)
                        EnableAutoSave();
                    else
                        CloseLogFile();
                    OnPropertyChanged(nameof(AutoSaveEnabled));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private LoggingService()
        {
            // Enable auto-save by default to AppData folder
            EnableAutoSave();
        }

        public void EnableAutoSave()
        {
            try
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SocketDeviceSimulator");
                
                Directory.CreateDirectory(appDataPath);
                
                var logFileName = $"socket_log_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                var logPath = Path.Combine(appDataPath, logFileName);
                
                CloseLogFile();
                _logFilePath = logPath;
                _logFileWriter = new StreamWriter(logPath, append: true) { AutoFlush = true };
                _autoSaveEnabled = true;
                
                Info("Logging", $"Auto-save enabled: {logPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enable auto-save: {ex.Message}");
            }
        }

        public void SetLogFile(string path)
        {
            CloseLogFile();
            _logFilePath = path;
            _logFileWriter = new StreamWriter(path, append: true) { AutoFlush = true };
            _autoSaveEnabled = true;
        }

        public void CloseLogFile()
        {
            try
            {
                _logFileWriter?.Close();
                _logFileWriter?.Dispose();
            }
            catch { }
            finally
            {
                _logFileWriter = null;
                _autoSaveEnabled = false;
            }
        }

        public void Log(LogLevel level, string category, string message, string? data = null)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = category,
                Message = message,
                Data = data
            };

            lock (_lock)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _logEntries.Add(entry);
                    // Keep only last 10000 entries
                    while (_logEntries.Count > 10000)
                    {
                        _logEntries.RemoveAt(0);
                    }
                });

                try
                {
                    _logFileWriter?.WriteLine(entry.ToString());
                }
                catch { }
            }
        }

        public void Info(string category, string message, string? data = null)
            => Log(LogLevel.Info, category, message, data);

        public void Debug(string category, string message, string? data = null)
            => Log(LogLevel.Debug, category, message, data);

        public void Warning(string category, string message, string? data = null)
            => Log(LogLevel.Warning, category, message, data);

        public void Error(string category, string message, string? data = null)
            => Log(LogLevel.Error, category, message, data);

        public void Clear()
        {
            lock (_lock)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => _logEntries.Clear());
            }
        }
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
