using System;

namespace SocketSimulator.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Data { get; set; }

        public override string ToString()
        {
            var dataStr = string.IsNullOrEmpty(Data) ? "" : $" [{Data}]";
            return $"[{Timestamp:HH:mm:ss.fff}] [{Level}] [{Category}] {Message}{dataStr}";
        }
    }

    public enum LogLevel
    {
        Info,
        Debug,
        Warning,
        Error
    }
}
