using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SocketSimulator.Commands;
using SocketSimulator.Models;
using SocketSimulator.Services;

namespace SocketSimulator.ViewModels
{
    public class ScenarioViewModel : ViewModelBase
    {
        private readonly ScenarioService _scenarioService;
        private readonly LoggingService _logger;
        private readonly ProtocolService _protocolService;

        private Scenario _scenario = new();
        private ScenarioStep? _selectedStep;
        private bool _isRunning;
        private int _nextStepOrder = 1;
        private string? _selectedProtocolCommandName;

        // Strongly-typed step properties for XAML binding
        public WaitCommandStep? SelectedWaitCommandStep => SelectedStep as WaitCommandStep;
        public WaitResponseStep? SelectedWaitResponseStep => SelectedStep as WaitResponseStep;
        public SendResponseStep? SelectedSendResponseStep => SelectedStep as SendResponseStep;
        public SendCommandStep? SelectedSendCommandStep => SelectedStep as SendCommandStep;
        public DelayStep? SelectedDelayStep => SelectedStep as DelayStep;
        public SetVariableStep? SelectedSetVariableStep => SelectedStep as SetVariableStep;
        public IfElseStep? SelectedIfElseStep => SelectedStep as IfElseStep;
        public LabelStep? SelectedLabelStep => SelectedStep as LabelStep;
        public GotoStep? SelectedGotoStep => SelectedStep as GotoStep;

        // All label names for goto dropdown
        public ObservableCollection<string> AvailableLabels { get; } = new();

        // Protocol command names for dropdowns
        public ObservableCollection<string> ProtocolCommandNames { get; } = new();
        
        // Protocol response templates for dropdowns
        public ObservableCollection<string> ProtocolResponseTemplates { get; } = new();

        public string? SelectedProtocolCommandName
        {
            get => _selectedProtocolCommandName;
            set
            {
                if (SetProperty(ref _selectedProtocolCommandName, value) && !string.IsNullOrEmpty(value))
                {
                    ApplyProtocolTemplate(value);
                }
            }
        }

        // Called when a command is selected from the protocol dropdown
        public void OnProtocolCommandSelected(string commandName)
        {
            if (string.IsNullOrEmpty(commandName)) return;

            var command = _protocolService.GetCommand(commandName);
            if (command == null) return;

            switch (SelectedStep)
            {
                case SendResponseStep sendResponse:
                    sendResponse.CommandName = command.Name;
                    if (!string.IsNullOrEmpty(command.ResponseTemplate))
                    {
                        sendResponse.Response = command.ResponseTemplate;
                    }
                    OnPropertyChanged(nameof(SelectedSendResponseStep));
                    break;

                case SendCommandStep sendCommand:
                    sendCommand.CommandName = command.Name;
                    if (!string.IsNullOrEmpty(command.Payload))
                    {
                        sendCommand.Payload = command.Payload;
                    }
                    OnPropertyChanged(nameof(SelectedSendCommandStep));
                    break;
            }
        }

        public ICommand ApplyProtocolTemplateCommand { get; }
        public ICommand AddPreActionCommand { get; }
        public ICommand AddOnSuccessCommand { get; }
        public ICommand AddOnTimeoutCommand { get; }

        public Scenario Scenario
        {
            get => _scenario;
            set => SetProperty(ref _scenario, value);
        }

        public ScenarioStep? SelectedStep
        {
            get => _selectedStep;
            set
            {
                if (SetProperty(ref _selectedStep, value))
                {
                    // Notify all strongly-typed step properties
                    OnPropertyChanged(nameof(SelectedWaitCommandStep));
                    OnPropertyChanged(nameof(SelectedWaitResponseStep));
                    OnPropertyChanged(nameof(SelectedSendResponseStep));
                    OnPropertyChanged(nameof(SelectedSendCommandStep));
                    OnPropertyChanged(nameof(SelectedDelayStep));
                    OnPropertyChanged(nameof(SelectedSetVariableStep));
                    OnPropertyChanged(nameof(SelectedIfElseStep));
                    OnPropertyChanged(nameof(SelectedLabelStep));
                    OnPropertyChanged(nameof(SelectedGotoStep));
                    RefreshAvailableLabels();
                }
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set => SetProperty(ref _isRunning, value);
        }

        public ObservableCollection<ScenarioStep> Steps { get; } = new();

        public ICommand RunScenarioCommand { get; }
        public ICommand StopScenarioCommand { get; }
        public ICommand AddStepCommand { get; }
        public ICommand RemoveStepCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand NewScenarioCommand { get; }

        public ScenarioViewModel()
        {
            _scenarioService = ScenarioService.Instance;
            _logger = LoggingService.Instance;
            _protocolService = ProtocolService.Instance;

            _scenarioService.ScenarioStarted += OnScenarioStarted;
            _scenarioService.ScenarioCompleted += OnScenarioCompleted;
            _scenarioService.StepExecuted += OnStepExecuted;
            _protocolService.ProtocolChanged += OnProtocolChanged;

            // Initialize protocol commands
            RefreshProtocolCommands();

            RunScenarioCommand = new AsyncRelayCommand(RunScenarioAsync, () => !IsRunning && Steps.Count > 0);
            StopScenarioCommand = new RelayCommand(StopScenario, () => IsRunning);
            AddStepCommand = new RelayCommand(AddStep);
            RemoveStepCommand = new RelayCommand(RemoveStep, () => SelectedStep != null);
            MoveUpCommand = new RelayCommand(MoveUp, () => CanMoveUp());
            MoveDownCommand = new RelayCommand(MoveDown, () => CanMoveDown());
            NewScenarioCommand = new RelayCommand(NewScenario);
            ApplyProtocolTemplateCommand = new RelayCommand(ApplyProtocolTemplateManual);
            AddPreActionCommand = new RelayCommand(AddLoopAction);
            AddOnSuccessCommand = new RelayCommand(AddLoopAction);
            AddOnTimeoutCommand = new RelayCommand(AddLoopAction);
        }

        private void OnProtocolChanged(object? sender, EventArgs e)
        {
            RefreshProtocolCommands();
        }

        private void RefreshProtocolCommands()
        {
            ProtocolCommandNames.Clear();
            ProtocolResponseTemplates.Clear();
            
            foreach (var name in _protocolService.GetCommandNames())
            {
                ProtocolCommandNames.Add(name);
            }
            
            foreach (var template in _protocolService.GetResponseTemplates())
            {
                ProtocolResponseTemplates.Add(template);
            }
        }

        private void RefreshAvailableLabels()
        {
            AvailableLabels.Clear();
            foreach (var step in Steps)
            {
                if (step is LabelStep label)
                {
                    AvailableLabels.Add(label.LabelName);
                }
            }
        }

        private void ApplyProtocolTemplateManual()
        {
            if (!string.IsNullOrEmpty(_selectedProtocolCommandName))
            {
                ApplyProtocolTemplate(_selectedProtocolCommandName);
            }
        }

        private void ApplyProtocolTemplate(string commandName)
        {
            var command = _protocolService.GetCommand(commandName);
            if (command == null) return;

            switch (SelectedStep)
            {
                case SendResponseStep sendResponse:
                    sendResponse.CommandName = command.Name;
                    if (!string.IsNullOrEmpty(command.ResponseTemplate))
                    {
                        sendResponse.Response = command.ResponseTemplate;
                        _logger.Info("Scenario", $"Applied response template from Protocol for {commandName}");
                    }
                    OnPropertyChanged(nameof(SelectedSendResponseStep));
                    break;

                case SendCommandStep sendCommand:
                    sendCommand.CommandName = command.Name;
                    if (!string.IsNullOrEmpty(command.Payload))
                    {
                        sendCommand.Payload = command.Payload;
                        _logger.Info("Scenario", $"Applied payload from Protocol for {commandName}");
                    }
                    OnPropertyChanged(nameof(SelectedSendCommandStep));
                    break;

                case WaitCommandStep waitCommand:
                    waitCommand.CommandName = command.Name;
                    OnPropertyChanged(nameof(SelectedWaitCommandStep));
                    break;

                case LoopUntilStep loopUntil:
                    loopUntil.CommandToSend = command.Name;
                    if (!string.IsNullOrEmpty(command.Payload))
                    {
                        loopUntil.Payload = command.Payload;
                    }
                    OnPropertyChanged(nameof(SelectedLoopUntilStep));
                    break;
            }
        }

        private void AddLoopAction(object? parameter)
        {
            if (SelectedLoopUntilStep == null) return;
            var actionType = parameter as string;
            if (string.IsNullOrEmpty(actionType)) return;

            var step = new SetVariableStep
            {
                Order = 1,
                VariableName = "Counter",
                Operation = VariableOperation.Increment,
                Value = "1"
            };

            switch (actionType)
            {
                case "PreAction":
                    SelectedLoopUntilStep.PreActions.Add(step);
                    break;
                case "OnSuccess":
                    SelectedLoopUntilStep.OnSuccess.Add(new SendCommandStep
                    {
                        Order = SelectedLoopUntilStep.OnSuccess.Count + 1,
                        CommandName = "OUTPUT",
                        Payload = "COLLECT_OUTPUT"
                    });
                    break;
                case "OnTimeout":
                    SelectedLoopUntilStep.OnTimeout.Add(new SendResponseStep
                    {
                        Order = SelectedLoopUntilStep.OnTimeout.Count + 1,
                        CommandName = "ERROR",
                        Response = "ERROR timeout"
                    });
                    break;
            }
            OnPropertyChanged(nameof(SelectedLoopUntilStep));
        }

        private void OnScenarioStarted(object? sender, EventArgs e)
        {
            IsRunning = true;
        }

        private void OnScenarioCompleted(object? sender, EventArgs e)
        {
            IsRunning = false;
        }

        private void OnStepExecuted(object? sender, ScenarioService.StepExecutedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                SelectedStep = e.Step;
            });
        }

        private async System.Threading.Tasks.Task RunScenarioAsync()
        {
            Scenario.Steps = Steps.ToList();
            await _scenarioService.RunScenarioAsync(Scenario);
        }

        private void StopScenario()
        {
            _scenarioService.StopScenario();
        }

        private void AddStep(object? parameter)
        {
            var stepType = parameter as string;
            if (!string.IsNullOrEmpty(stepType))
            {
                ScenarioStep?                 step = stepType switch
                {
                    "WaitCommand" => new WaitCommandStep
                    {
                        Order = _nextStepOrder++,
                        CommandName = "EXPECTED_CMD",
                        TimeoutMs = 5000
                    },
                    "WaitResponse" => new WaitResponseStep
                    {
                        Order = _nextStepOrder++,
                        ExpectedResponseContains = "completed",
                        TimeoutMs = 5000
                    },
                    "SendResponse" => new SendResponseStep
                    {
                        Order = _nextStepOrder++,
                        CommandName = "RESPONSE_CMD",
                        Response = "OK"
                    },
                    "SendCommand" => new SendCommandStep
                    {
                        Order = _nextStepOrder++,
                        CommandName = "SEND_CMD",
                        Payload = "Hello"
                    },
                    "Delay" => new DelayStep
                    {
                        Order = _nextStepOrder++,
                        DurationMs = 1000
                    },
                    "SetVariable" => new SetVariableStep
                    {
                        Order = _nextStepOrder++,
                        VariableName = "Counter",
                        Value = "1",
                        Operation = VariableOperation.Increment
                    },
                    "IfElse" => new IfElseStep
                    {
                        Order = _nextStepOrder++,
                        Condition = "${State} == ACTIVE"
                    },
                    "Label" => new LabelStep
                    {
                        Order = _nextStepOrder++,
                        LabelName = $"Label_{_nextStepOrder}"
                    },
                    "Goto" => new GotoStep
                    {
                        Order = _nextStepOrder++,
                        TargetLabel = ""
                    },
                    _ => null
                };

                if (step != null)
                {
                    Steps.Add(step);
                    SelectedStep = step;
                    RefreshAvailableLabels();
                    _logger.Info("Scenario", $"Added step: {step.GetType().Name}");
                }
            }
        }

        private void RemoveStep()
        {
            if (SelectedStep == null) return;

            Steps.Remove(SelectedStep);
            ReorderSteps();
            SelectedStep = Steps.LastOrDefault();
            RefreshAvailableLabels();
            _logger.Info("Scenario", "Removed step");
        }

        private void MoveUp()
        {
            if (SelectedStep == null) return;
            var index = Steps.IndexOf(SelectedStep);
            if (index > 0)
            {
                Steps.Move(index, index - 1);
                ReorderSteps();
            }
        }

        private void MoveDown()
        {
            if (SelectedStep == null) return;
            var index = Steps.IndexOf(SelectedStep);
            if (index < Steps.Count - 1)
            {
                Steps.Move(index, index + 1);
                ReorderSteps();
            }
        }

        private bool CanMoveUp()
        {
            if (SelectedStep == null) return false;
            return Steps.IndexOf(SelectedStep) > 0;
        }

        private bool CanMoveDown()
        {
            if (SelectedStep == null) return false;
            return Steps.IndexOf(SelectedStep) < Steps.Count - 1;
        }

        private void ReorderSteps()
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                Steps[i].Order = i + 1;
            }
        }

        public void NewScenario()
        {
            Steps.Clear();
            Scenario = new Scenario();
            _nextStepOrder = 1;
            _logger.Info("Scenario", "Created new scenario");
        }

        public void LoadScenario(Scenario scenario)
        {
            Scenario = scenario;
            Steps.Clear();
            foreach (var step in scenario.Steps)
            {
                Steps.Add(step);
            }
            _nextStepOrder = Steps.Count > 0 ? Steps.Max(s => s.Order) + 1 : 1;
        }

        public Scenario GetScenario()
        {
            Scenario.Steps = Steps.ToList();
            return Scenario;
        }
    }
}
