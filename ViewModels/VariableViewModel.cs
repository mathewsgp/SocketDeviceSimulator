using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SocketSimulator.Commands;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class VariableViewModel : ViewModelBase
    {
        private readonly VariableService _variableService;
        private readonly LoggingService _logger;

        private string _newVariableName = string.Empty;
        private string _newVariableValue = string.Empty;
        private VariableItem? _selectedVariable;

        public ObservableCollection<VariableItem> Variables { get; } = new();

        public string NewVariableName
        {
            get => _newVariableName;
            set => SetProperty(ref _newVariableName, value);
        }

        public string NewVariableValue
        {
            get => _newVariableValue;
            set => SetProperty(ref _newVariableValue, value);
        }

        public VariableItem? SelectedVariable
        {
            get => _selectedVariable;
            set => SetProperty(ref _selectedVariable, value);
        }

        public ICommand AddVariableCommand { get; }
        public ICommand RemoveVariableCommand { get; }
        public ICommand ResetVariablesCommand { get; }
        public ICommand RefreshCommand { get; }

        public VariableViewModel()
        {
            _variableService = VariableService.Instance;
            _logger = LoggingService.Instance;

            _variableService.VariableChanged += OnVariableChanged;

            AddVariableCommand = new RelayCommand(AddVariable, () => !string.IsNullOrWhiteSpace(NewVariableName));
            RemoveVariableCommand = new RelayCommand(RemoveVariable, () => SelectedVariable != null);
            ResetVariablesCommand = new RelayCommand(ResetVariables);
            RefreshCommand = new RelayCommand(Refresh);

            Refresh();
        }

        private void OnVariableChanged(object? sender, VariableService.VariableChangedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var item = Variables.FirstOrDefault(v => v.Name == e.Name);
                if (item != null)
                {
                    item.Value = e.NewValue?.ToString() ?? string.Empty;
                }
            });
        }

        private void Refresh()
        {
            Variables.Clear();
            foreach (var (name, value) in _variableService.Variables)
            {
                Variables.Add(new VariableItem { Name = name, Value = value?.ToString() ?? string.Empty });
            }
        }

        private void AddVariable()
        {
            if (string.IsNullOrWhiteSpace(NewVariableName))
                return;

            _variableService.SetVariable(NewVariableName, NewVariableValue);
            Variables.Add(new VariableItem { Name = NewVariableName, Value = NewVariableValue });
            NewVariableName = string.Empty;
            NewVariableValue = string.Empty;
        }

        private void RemoveVariable()
        {
            if (SelectedVariable == null)
                return;

            // Variables are read-only in this implementation
            _logger.Warning("Variable", "Cannot remove built-in variables");
        }

        private void ResetVariables()
        {
            _variableService.Reset();
            Refresh();
            _logger.Info("Variable", "Variables reset to defaults");
        }
    }

    public class VariableItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
