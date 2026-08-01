using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SocketSimulator.Models;

namespace SocketSimulator.Services
{
    public class VariableService : INotifyPropertyChanged
    {
        private static VariableService? _instance;
        public static VariableService Instance => _instance ??= new VariableService();

        private readonly Dictionary<string, object> _variables = new();
        private readonly LoggingService _logger = LoggingService.Instance;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<VariableChangedEventArgs>? VariableChanged;

        public class VariableChangedEventArgs : EventArgs
        {
            public string Name { get; }
            public object? OldValue { get; }
            public object? NewValue { get; }

            public VariableChangedEventArgs(string name, object? oldValue, object? newValue)
            {
                Name = name;
                OldValue = oldValue;
                NewValue = newValue;
            }
        }

        private VariableService()
        {
            // Initialize default variables
            SetVariable("State", "IDLE");
            SetVariable("Counter", 0);
            SetVariable("Temperature", 25.0);
            SetVariable("Pressure", 101.325);
        }

        public void SetVariable(string name, object value)
        {
            var oldValue = GetVariable(name);
            _variables[name] = value;
            _logger.Info("Variable", $"Set {name} = {value}");
            OnVariableChanged(name, oldValue, value);
            OnPropertyChanged(nameof(Variables));
        }

        public T GetVariable<T>(string name)
        {
            if (_variables.TryGetValue(name, out var value))
            {
                if (value is T typedValue)
                    return typedValue;
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default!;
                }
            }
            return default!;
        }

        public object? GetVariable(string name)
        {
            return _variables.TryGetValue(name, out var value) ? value : null;
        }

        public string GetVariableString(string name)
        {
            var value = GetVariable(name);
            return value?.ToString() ?? string.Empty;
        }

        public void IncrementVariable(string name, int amount = 1)
        {
            var current = GetVariable<int>(name);
            SetVariable(name, current + amount);
        }

        public void DecrementVariable(string name, int amount = 1)
        {
            var current = GetVariable<int>(name);
            SetVariable(name, current - amount);
        }

        public void ToggleVariable(string name)
        {
            var current = GetVariable<string>(name).ToUpper();
            SetVariable(name, current == "TRUE" ? "FALSE" : "TRUE");
        }

        public IEnumerable<(string Name, object Value)> Variables
        {
            get
            {
                return _variables.Select(kv => (kv.Key, kv.Value));
            }
        }

        public void Reset()
        {
            _variables.Clear();
            SetVariable("State", "IDLE");
            SetVariable("Counter", 0);
            SetVariable("Temperature", 25.0);
            SetVariable("Pressure", 101.325);
        }

        protected virtual void OnVariableChanged(string name, object? oldValue, object? newValue)
        {
            VariableChanged?.Invoke(this, new VariableChangedEventArgs(name, oldValue, newValue));
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
