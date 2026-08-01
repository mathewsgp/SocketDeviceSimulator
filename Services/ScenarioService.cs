using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SocketSimulator.Models;

namespace SocketSimulator.Services
{
    public class ScenarioService
    {
        private static ScenarioService? _instance;
        public static ScenarioService Instance => _instance ??= new ScenarioService();

        private readonly LoggingService _logger = LoggingService.Instance;
        private readonly VariableService _variableService = VariableService.Instance;
        private readonly SocketService _socketService = SocketService.Instance;
        private readonly ProtocolService _protocolService = ProtocolService.Instance;

        private CancellationTokenSource? _executionTokenSource;
        private bool _isRunning;
        private string _lastReceivedCommand = string.Empty;
        private Scenario? _currentScenario;

        public event EventHandler<StepExecutedEventArgs>? StepExecuted;
        public event EventHandler? ScenarioStarted;
        public event EventHandler? ScenarioCompleted;
        public event EventHandler<string>? ScenarioError;

        public bool IsRunning => _isRunning;
        public Scenario? CurrentScenario => _currentScenario;

        private ScenarioService()
        {
            _socketService.DataReceived += OnDataReceived;
        }

        private void OnDataReceived(object? sender, SocketService.DataReceivedEventArgs e)
        {
            _lastReceivedCommand = e.Data;
        }

        public async Task RunScenarioAsync(Scenario scenario)
        {
            if (_isRunning)
            {
                _logger.Warning("Scenario", "Scenario already running");
                return;
            }

            _currentScenario = scenario;
            _isRunning = true;
            _executionTokenSource = new CancellationTokenSource();
            ScenarioStarted?.Invoke(this, EventArgs.Empty);
            _logger.Info("Scenario", $"Started scenario: {scenario.Name}");

            try
            {
                await ExecuteStepsAsync(scenario.Steps, _executionTokenSource.Token);
                _logger.Info("Scenario", $"Completed scenario: {scenario.Name}");
                ScenarioCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Scenario", "Scenario cancelled");
            }
            catch (Exception ex)
            {
                _logger.Error("Scenario", $"Scenario error: {ex.Message}");
                ScenarioError?.Invoke(this, ex.Message);
            }
            finally
            {
                _isRunning = false;
                _executionTokenSource?.Dispose();
                _executionTokenSource = null;
            }
        }

        public void StopScenario()
        {
            _executionTokenSource?.Cancel();
            _logger.Info("Scenario", "Stopping scenario...");
        }

        private async Task ExecuteStepsAsync(List<ScenarioStep> steps, CancellationToken cancellationToken)
        {
            foreach (var step in steps.OrderBy(s => s.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteStepAsync(step, cancellationToken);
            }
        }

        private async Task ExecuteStepAsync(ScenarioStep step, CancellationToken cancellationToken)
        {
            _logger.Debug("Scenario", $"Executing step: {step.GetType().Name}");
            StepExecuted?.Invoke(this, new StepExecutedEventArgs(step));

            switch (step)
            {
                case WaitCommandStep waitStep:
                    await ExecuteWaitCommandAsync(waitStep, cancellationToken);
                    break;
                case SendResponseStep sendResponseStep:
                    await ExecuteSendResponseAsync(sendResponseStep, cancellationToken);
                    break;
                case SendCommandStep sendCommandStep:
                    await ExecuteSendCommandAsync(sendCommandStep, cancellationToken);
                    break;
                case DelayStep delayStep:
                    await Task.Delay(delayStep.DurationMs, cancellationToken);
                    break;
                case SetVariableStep setVariableStep:
                    ExecuteSetVariable(setVariableStep);
                    break;
                case IfElseStep ifElseStep:
                    await ExecuteIfElseAsync(ifElseStep, cancellationToken);
                    break;
            }
        }

        private async Task ExecuteWaitCommandAsync(WaitCommandStep step, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(step.TimeoutMs);

            while (DateTime.Now - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(_lastReceivedCommand))
                {
                    var receivedCommand = ParseCommandName(_lastReceivedCommand);
                    if (receivedCommand.Equals(step.CommandName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Info("Scenario", $"Received expected command: {step.CommandName}");
                        _lastReceivedCommand = string.Empty;
                        return;
                    }
                }

                await Task.Delay(50, cancellationToken);
            }

            _logger.Warning("Scenario", $"Timeout waiting for command: {step.CommandName}");
        }

        private string ParseCommandName(string message)
        {
            // Simple command parsing - extract first word
            var parts = message.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : message;
        }

        private async Task ExecuteSendResponseAsync(SendResponseStep step, CancellationToken cancellationToken)
        {
            var response = _protocolService.SubstituteVariables(step.Response);
            await _socketService.SendAsync(response);
            _logger.Info("Scenario", $"Sent response: {response}");
        }

        private async Task ExecuteSendCommandAsync(SendCommandStep step, CancellationToken cancellationToken)
        {
            var payload = _protocolService.SubstituteVariables(step.Payload);
            await _socketService.SendAsync(payload);
            _logger.Info("Scenario", $"Sent command: {payload}");
        }

        private void ExecuteSetVariable(SetVariableStep step)
        {
            var value = _protocolService.SubstituteVariables(step.Value);

            switch (step.Operation)
            {
                case VariableOperation.Set:
                    _variableService.SetVariable(step.VariableName, value);
                    break;
                case VariableOperation.Increment:
                    if (int.TryParse(value, out var incrementAmount))
                        _variableService.IncrementVariable(step.VariableName, incrementAmount);
                    else
                        _variableService.IncrementVariable(step.VariableName, 1);
                    break;
                case VariableOperation.Decrement:
                    if (int.TryParse(value, out var decrementAmount))
                        _variableService.DecrementVariable(step.VariableName, decrementAmount);
                    else
                        _variableService.DecrementVariable(step.VariableName, 1);
                    break;
                case VariableOperation.Toggle:
                    _variableService.ToggleVariable(step.VariableName);
                    break;
            }
        }

        private async Task ExecuteIfElseAsync(IfElseStep step, CancellationToken cancellationToken)
        {
            var conditionMet = EvaluateCondition(step.Condition);

            if (conditionMet)
            {
                _logger.Debug("Scenario", $"Condition true: {step.Condition}");
                await ExecuteStepsAsync(step.IfTrue, cancellationToken);
            }
            else
            {
                _logger.Debug("Scenario", $"Condition false: {step.Condition}");
                await ExecuteStepsAsync(step.IfFalse, cancellationToken);
            }
        }

        private bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return false;

            // Simple condition evaluation
            // Supports: VariableName == value, VariableName != value, VariableName > value, etc.
            
            var variableService = VariableService.Instance;
            
            // Equality check
            var eqMatch = Regex.Match(condition, @"\$\{([^}]+)\}\s*==\s*(.+)");
            if (eqMatch.Success)
            {
                var varName = eqMatch.Groups[1].Value;
                var expectedValue = eqMatch.Groups[2].Value.Trim();
                var actualValue = variableService.GetVariableString(varName);
                return actualValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);
            }

            // Not equal check
            var neMatch = Regex.Match(condition, @"\$\{([^}]+)\}\s*!=\s*(.+)");
            if (neMatch.Success)
            {
                var varName = neMatch.Groups[1].Value;
                var expectedValue = neMatch.Groups[2].Value.Trim();
                var actualValue = variableService.GetVariableString(varName);
                return !actualValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);
            }

            // Greater than
            var gtMatch = Regex.Match(condition, @"\$\{([^}]+)\}\s*>\s*(.+)");
            if (gtMatch.Success)
            {
                var varName = gtMatch.Groups[1].Value;
                if (double.TryParse(variableService.GetVariableString(varName), out var actual) &&
                    double.TryParse(gtMatch.Groups[2].Value.Trim(), out var expected))
                {
                    return actual > expected;
                }
            }

            // Less than
            var ltMatch = Regex.Match(condition, @"\$\{([^}]+)\}\s*<\s*(.+)");
            if (ltMatch.Success)
            {
                var varName = ltMatch.Groups[1].Value;
                if (double.TryParse(variableService.GetVariableString(varName), out var actual) &&
                    double.TryParse(ltMatch.Groups[2].Value.Trim(), out var expected))
                {
                    return actual < expected;
                }
            }

            return false;
        }

        public class StepExecutedEventArgs : EventArgs
        {
            public ScenarioStep Step { get; }
            public StepExecutedEventArgs(ScenarioStep step) => Step = step;
        }
    }
}
