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
        private readonly ProtocolService _protocolService;

        private string _newVariableName = string.Empty;
        private string _newVariableValue = string.Empty;
        private VariableItem? _selectedVariable;

        public ObservableCollection<VariableItem> Variables { get; } = new();
        public ObservableCollection<VariableItem> ParsedItems { get; } = new();

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
        public ICommand ClearParsedCommand { get; }

        public VariableViewModel()
        {
            _variableService = VariableService.Instance;
            _logger = LoggingService.Instance;
            _protocolService = ProtocolService.Instance;

            _variableService.VariableChanged += OnVariableChanged;
            _protocolService.ProtocolChanged += OnProtocolChanged;

            AddVariableCommand = new RelayCommand(AddVariable, () => !string.IsNullOrWhiteSpace(NewVariableName));
            RemoveVariableCommand = new RelayCommand(RemoveVariable, () => SelectedVariable != null);
            ResetVariablesCommand = new RelayCommand(ResetVariables);
            RefreshCommand = new RelayCommand(_ => RefreshAll());
            ClearParsedCommand = new RelayCommand(ClearParsed);

            RefreshAll();
        }

        private void OnProtocolChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(RefreshAll);
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

        private void RefreshAll()
        {
            RefreshVariables();
            RefreshParsedItems();
        }

        private void RefreshVariables()
        {
            Variables.Clear();
            foreach (var (name, value) in _variableService.Variables)
            {
                Variables.Add(new VariableItem 
                { 
                    Name = name, 
                    Value = value?.ToString() ?? string.Empty,
                    Category = "Variable"
                });
            }
        }

        private void RefreshParsedItems()
        {
            ParsedItems.Clear();
            
            // Add LastCommand and LastResponse
            if (_protocolService.ParsedData.TryGetValue("LastCommand", out var lastCmd) && !string.IsNullOrEmpty(lastCmd))
            {
                ParsedItems.Add(new VariableItem 
                { 
                    Name = "LastCommand", 
                    Value = lastCmd,
                    Category = "Parsed"
                });
            }
            
            if (_protocolService.ParsedData.TryGetValue("LastResponse", out var lastResp) && !string.IsNullOrEmpty(lastResp))
            {
                ParsedItems.Add(new VariableItem 
                { 
                    Name = "LastResponse", 
                    Value = lastResp,
                    Category = "Parsed"
                });
            }

            // Add all other parsed data
            foreach (var kvp in _protocolService.ParsedData)
            {
                // Skip system keys
                if (kvp.Key == "LastCommand" || kvp.Key == "LastResponse")
                    continue;
                    
                // Skip raw command messages (those without dots - they are the full command string)
                if (!kvp.Key.Contains('.'))
                    continue;

                ParsedItems.Add(new VariableItem 
                { 
                    Name = kvp.Key, 
                    Value = kvp.Value,
                    Category = "Parsed"
                });
            }
        }

        private void AddVariable()
        {
            if (string.IsNullOrWhiteSpace(NewVariableName))
                return;

            _variableService.SetVariable(NewVariableName, NewVariableValue);
            Variables.Add(new VariableItem 
            { 
                Name = NewVariableName, 
                Value = NewVariableValue,
                Category = "Variable"
            });
            NewVariableName = string.Empty;
            NewVariableValue = string.Empty;
        }

        private void RemoveVariable()
        {
            if (SelectedVariable == null)
                return;

            if (SelectedVariable.Category == "Variable")
            {
                _variableService.RemoveVariable(SelectedVariable.Name);
                Variables.Remove(SelectedVariable);
            }
            // Parsed items cannot be removed
        }

        private void ClearParsed()
        {
            _protocolService.ClearParsedData();
            RefreshParsedItems();
            _logger.Info("Variable", "Parsed items cleared");
        }

        private void ResetVariables()
        {
            _variableService.Reset();
            RefreshVariables();
            _logger.Info("Variable", "Variables reset to defaults");
        }
    }

    public class VariableItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Category { get; set; } = "Variable";
    }
}
