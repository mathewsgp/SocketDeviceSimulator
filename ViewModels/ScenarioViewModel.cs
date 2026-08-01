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

        public Scenario Scenario
        {
            get => _scenario;
            set => SetProperty(ref _scenario, value);
        }

        public ScenarioStep? SelectedStep
        {
            get => _selectedStep;
            set => SetProperty(ref _selectedStep, value);
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

            RunScenarioCommand = new AsyncRelayCommand(RunScenarioAsync, () => !IsRunning && Steps.Count > 0);
            StopScenarioCommand = new RelayCommand(StopScenario, () => IsRunning);
            AddStepCommand = new RelayCommand(AddStep);
            RemoveStepCommand = new RelayCommand(RemoveStep, () => SelectedStep != null);
            MoveUpCommand = new RelayCommand(MoveUp, () => CanMoveUp());
            MoveDownCommand = new RelayCommand(MoveDown, () => CanMoveDown());
            NewScenarioCommand = new RelayCommand(NewScenario);
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
                ScenarioStep? step = stepType switch
                {
                    "WaitCommand" => new WaitCommandStep
                    {
                        Order = _nextStepOrder++,
                        CommandName = "EXPECTED_CMD",
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
                    _ => null
                };

                if (step != null)
                {
                    Steps.Add(step);
                    SelectedStep = step;
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
