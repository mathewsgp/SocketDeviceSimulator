using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using SocketSimulator.Commands;
using SocketSimulator.Models;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class ProtocolViewModel : ViewModelBase
    {
        private readonly ProtocolService _protocolService;
        private readonly LoggingService _logger;

        private ProtocolDefinition? _currentProtocol;
        private CommandDefinition? _selectedCommand;

        public ProtocolDefinition? CurrentProtocol
        {
            get => _currentProtocol;
            private set => SetProperty(ref _currentProtocol, value);
        }

        public CommandDefinition? SelectedCommand
        {
            get => _selectedCommand;
            set => SetProperty(ref _selectedCommand, value);
        }

        public ObservableCollection<CommandDefinition> Commands { get; } = new();

        public string ProtocolName => CurrentProtocol?.Name ?? "No Protocol Loaded";
        public string ProtocolVersion => CurrentProtocol?.Version ?? "";
        public bool HasProtocol => CurrentProtocol != null;

        public ICommand LoadProtocolCommand { get; }
        public ICommand SaveProtocolCommand { get; }
        public ICommand NewProtocolCommand { get; }
        public ICommand AddCommandCommand { get; }
        public ICommand RemoveCommandCommand { get; }

        public event EventHandler? ProtocolLoaded;

        public ProtocolViewModel()
        {
            _protocolService = ProtocolService.Instance;
            _logger = LoggingService.Instance;

            _protocolService.ProtocolChanged += OnProtocolChanged;

            LoadProtocolCommand = new RelayCommand(LoadProtocol);
            SaveProtocolCommand = new RelayCommand(SaveProtocol, () => CurrentProtocol != null);
            NewProtocolCommand = new RelayCommand(NewProtocol);
            AddCommandCommand = new RelayCommand(AddCommand, () => CurrentProtocol != null);
            RemoveCommandCommand = new RelayCommand(RemoveCommand, () => SelectedCommand != null);
        }

        private void OnProtocolChanged(object? sender, EventArgs e)
        {
            CurrentProtocol = _protocolService.CurrentProtocol;
            RefreshCommands();
            OnPropertyChanged(nameof(ProtocolName));
            OnPropertyChanged(nameof(ProtocolVersion));
            OnPropertyChanged(nameof(HasProtocol));
            ProtocolLoaded?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshCommands()
        {
            Commands.Clear();
            if (CurrentProtocol != null)
            {
                foreach (var cmd in CurrentProtocol.Commands)
                {
                    Commands.Add(cmd);
                }
            }
        }

        private void LoadProtocol()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Load Protocol Definition"
            };

            if (dialog.ShowDialog() == true)
            {
                var protocol = _protocolService.LoadProtocol(dialog.FileName);
                if (protocol != null)
                {
                    CurrentProtocol = protocol;
                    _logger.Info("Protocol", $"Loaded protocol from {dialog.FileName}");
                }
            }
        }

        private void SaveProtocol()
        {
            if (CurrentProtocol == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Save Protocol Definition",
                FileName = $"{CurrentProtocol.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                _protocolService.SaveProtocol(dialog.FileName, CurrentProtocol);
                _logger.Info("Protocol", $"Saved protocol to {dialog.FileName}");
            }
        }

        private void NewProtocol()
        {
            CurrentProtocol = new ProtocolDefinition
            {
                Name = "New Protocol",
                Version = "1.0"
            };
            _logger.Info("Protocol", "Created new protocol");
        }

        private void AddCommand()
        {
            if (CurrentProtocol == null) return;

            var command = new CommandDefinition
            {
                Name = $"CMD_{CurrentProtocol.Commands.Count + 1:D3}",
                Description = "New Command"
            };

            CurrentProtocol.Commands.Add(command);
            Commands.Add(command);
            SelectedCommand = command;
            OnPropertyChanged(nameof(CurrentProtocol));
            _logger.Info("Protocol", $"Added command: {command.Name}");
        }

        private void RemoveCommand()
        {
            if (SelectedCommand == null || CurrentProtocol == null) return;

            CurrentProtocol.Commands.Remove(SelectedCommand);
            Commands.Remove(SelectedCommand);
            _logger.Info("Protocol", $"Removed command: {SelectedCommand.Name}");
            SelectedCommand = Commands.LastOrDefault();
        }
    }
}
