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
        
        // Active auto-reply rules: key is command pattern, value is AutoReplyStep
        private Dictionary<string, AutoReplyStep> _activeAutoReplyRules = new();
        
        // Lock for thread safety when accessing shared data
        private readonly object _dataLock = new();

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
            _logger.Debug("Scenario", $"OnDataReceived: data='{e.Data}'");
            lock (_dataLock)
            {
                _lastReceivedCommand = e.Data;
                // Parse and store the command using Protocol
                if (_protocolService != null)
                {
                    _protocolService.ParseCommand(e.Data);
                    var allKeys = string.Join(", ", _protocolService.ParsedData.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    _logger.Debug("Scenario", $"OnDataReceived: parsed data after ParseCommand: [{allKeys}]");
                }
            }
            
            // Check for matching auto-reply rules (outside lock to avoid deadlock)
            CheckAndExecuteAutoReply(e.Data);
        }
        
        private void CheckAndExecuteAutoReply(string receivedData)
        {
            if (_activeAutoReplyRules.Count == 0)
            {
                _logger.Debug("Scenario", $"AutoReply: No rules registered (count=0), ignoring '{receivedData}'");
                return;
            }
            
            var parsedCommand = _protocolService.ParseCommand(receivedData);
            
            foreach (var rule in _activeAutoReplyRules.Values)
            {
                var patternToMatch = !string.IsNullOrEmpty(rule.CommandName) ? rule.CommandName : rule.CommandPattern;
                
                if (MatchesPattern(parsedCommand.CommandName, patternToMatch))
                {
                    _logger.Info("Scenario", $"AutoReply: Matched command '{parsedCommand.CommandName}', sending auto-response");
                    
                    // Build and send response
                    var response = rule.Response;
                    
                    // If using Protocol response template, build it
                    if (string.IsNullOrEmpty(response) && !string.IsNullOrEmpty(rule.CommandName))
                    {
                        response = _protocolService.BuildResponse(rule.CommandName);
                    }
                    
                    // Substitute variables in response
                    response = _protocolService.SubstituteVariables(response);
                    
                    if (!string.IsNullOrEmpty(response))
                    {
                        _ = _socketService.SendAsync(response);
                        _logger.Info("Scenario", $"AutoReply: Sent response: {response}");
                    }
                }
            }
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
            _activeAutoReplyRules.Clear(); // Clear any previous auto-reply rules
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
                _activeAutoReplyRules.Clear(); // Clear auto-reply rules when scenario ends
                _executionTokenSource?.Dispose();
                _executionTokenSource = null;
            }
        }

        public void StopScenario()
        {
            _executionTokenSource?.Cancel();
            _logger.Info("Scenario", "Stopping scenario...");
        }

        private async Task ExecuteStepsAsync(List<ScenarioStep> allSteps, CancellationToken cancellationToken)
        {
            var orderedSteps = allSteps.OrderBy(s => s.Order).ToList();
            var labelIndexMap = BuildLabelIndexMap(allSteps);
            int currentIndex = 0;

            while (currentIndex < orderedSteps.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = orderedSteps[currentIndex];

                // Handle Goto step
                if (step is GotoStep gotoStep)
                {
                    if (!string.IsNullOrEmpty(gotoStep.TargetLabel) && labelIndexMap.TryGetValue(gotoStep.TargetLabel, out var targetIndex))
                    {
                        _logger.Debug("Scenario", $"Goto: Jumping to '{gotoStep.TargetLabel}' (index {targetIndex})");
                        currentIndex = targetIndex;
                        continue;
                    }
                    else if (!string.IsNullOrEmpty(gotoStep.TargetLabel))
                    {
                        _logger.Warning("Scenario", $"Goto: Label '{gotoStep.TargetLabel}' not found");
                    }
                    currentIndex++;
                    continue;
                }

                // Handle Label step
                if (step is LabelStep labelStep)
                {
                    _logger.Debug("Scenario", $"Label: {labelStep.LabelName}");
                    currentIndex++;
                    continue;
                }

                // Handle AutoReply with goto support
                if (step is AutoReplyStep autoReplyStep)
                {
                    currentIndex = await ExecuteAutoReplyWithGotoAsync(autoReplyStep, labelIndexMap, currentIndex, cancellationToken);
                    continue;
                }

                // Handle WaitCommand with goto labels
                if (step is WaitCommandStep waitCmdStep)
                {
                    var (gotoExecuted, newIndex) = await ExecuteWaitCommandWithGotoAsync(waitCmdStep, labelIndexMap, cancellationToken);
                    if (gotoExecuted)
                    {
                        currentIndex = newIndex;
                        continue;
                    }
                    currentIndex++;
                    continue;
                }

                // Handle WaitResponse with goto labels
                if (step is WaitResponseStep waitRespStep)
                {
                    var (gotoExecuted, newIndex) = await ExecuteWaitResponseWithGotoAsync(waitRespStep, labelIndexMap, cancellationToken);
                    if (gotoExecuted)
                    {
                        currentIndex = newIndex;
                        continue;
                    }
                    currentIndex++;
                    continue;
                }

                // Handle IfElse with goto
                if (step is IfElseStep ifElseStep)
                {
                    var conditionMet = EvaluateCondition(ifElseStep.Condition);
                    
                    if (conditionMet && !string.IsNullOrEmpty(ifElseStep.GotoLabelIfTrue))
                    {
                        _logger.Debug("Scenario", $"IfElse: Condition true, goto '{ifElseStep.GotoLabelIfTrue}'");
                        if (labelIndexMap.TryGetValue(ifElseStep.GotoLabelIfTrue, out var targetIdx))
                        {
                            currentIndex = targetIdx;
                            continue;
                        }
                    }
                    else if (!conditionMet && !string.IsNullOrEmpty(ifElseStep.GotoLabelIfFalse))
                    {
                        _logger.Debug("Scenario", $"IfElse: Condition false, goto '{ifElseStep.GotoLabelIfFalse}'");
                        if (labelIndexMap.TryGetValue(ifElseStep.GotoLabelIfFalse, out var targetIdx))
                        {
                            currentIndex = targetIdx;
                            continue;
                        }
                    }
                    else
                    {
                        // Execute inline steps
                        if (conditionMet)
                        {
                            _logger.Debug("Scenario", $"IfElse: Condition true: {ifElseStep.Condition}");
                            await ExecuteStepsAsync(ifElseStep.IfTrue, cancellationToken);
                        }
                        else
                        {
                            _logger.Debug("Scenario", $"IfElse: Condition false: {ifElseStep.Condition}");
                            await ExecuteStepsAsync(ifElseStep.IfFalse, cancellationToken);
                        }
                    }
                    currentIndex++;
                    continue;
                }

                // Execute regular step
                await ExecuteStepAsync(step, cancellationToken);
                currentIndex++;
            }
        }

        private async Task<(bool gotoExecuted, int newIndex)> ExecuteWaitCommandWithGotoAsync(WaitCommandStep step, Dictionary<string, int> labelMap, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(step.TimeoutMs);
            bool? success = null;

            while (DateTime.Now - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(_lastReceivedCommand))
                {
                    var receivedCommand = ParseCommandName(_lastReceivedCommand);
                    if (receivedCommand.Equals(step.CommandName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Info("Scenario", $"WaitCommand: Received '{step.CommandName}'");
                        success = true;
                        _lastReceivedCommand = string.Empty;
                        break;
                    }
                }

                await Task.Delay(50, cancellationToken);
            }

            if (success == null)
            {
                _logger.Warning("Scenario", $"WaitCommand: Timeout waiting for '{step.CommandName}'");
                success = false;
            }

            // Handle goto based on result
            var targetLabel = success == true ? step.GotoLabelOnSuccess : step.GotoLabelOnTimeout;
            if (!string.IsNullOrEmpty(targetLabel) && labelMap.TryGetValue(targetLabel, out var targetIndex))
            {
                _logger.Debug("Scenario", $"WaitCommand: {(success == true ? "Success" : "Timeout")}, goto '{targetLabel}'");
                return (true, targetIndex);
            }

            return (false, 0);
        }

        private async Task<(bool gotoExecuted, int newIndex)> ExecuteWaitResponseWithGotoAsync(WaitResponseStep step, Dictionary<string, int> labelMap, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(step.TimeoutMs);
            bool? success = null;
            string receivedData = string.Empty;

            while (DateTime.Now - startTime < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_dataLock)
                {
                    if (!string.IsNullOrEmpty(_lastReceivedCommand) &&
                        _lastReceivedCommand.Contains(step.ExpectedResponseContains, StringComparison.OrdinalIgnoreCase))
                    {
                        receivedData = _lastReceivedCommand;
                        _logger.Info("Scenario", $"WaitResponse: Found '{step.ExpectedResponseContains}'");
                        success = true;
                        _lastReceivedCommand = string.Empty;
                        break;
                    }
                }

                await Task.Delay(50, cancellationToken);
            }

            if (success == null)
            {
                _logger.Warning("Scenario", $"WaitResponse: Timeout waiting for '{step.ExpectedResponseContains}'");
                success = false;
            }

            // Handle goto based on result
            var targetLabel = success == true ? step.GotoLabelOnSuccess : step.GotoLabelOnTimeout;
            if (!string.IsNullOrEmpty(targetLabel) && labelMap.TryGetValue(targetLabel, out var targetIndex))
            {
                _logger.Debug("Scenario", $"WaitResponse: {(success == true ? "Success" : "Timeout")}, goto '{targetLabel}'");
                return (true, targetIndex);
            }

            return (false, 0);
        }

        private Dictionary<string, int> BuildLabelIndexMap(List<ScenarioStep> steps)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var ordered = steps.OrderBy(s => s.Order).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i] is LabelStep label)
                {
                    map[label.LabelName] = i;
                }
            }
            return map;
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
                    // IfElse is handled in ExecuteStepsAsync with goto support
                    // This case handles nested execution via ExecuteStepsAsync
                    break;
                case LabelStep labelStep:
                    // Label is just a marker, nothing to execute
                    _logger.Debug("Scenario", $"Label: {labelStep.LabelName}");
                    break;
                case GotoStep gotoStep:
                    // Goto is handled in RunScenarioAsync by finding the target label
                    _logger.Debug("Scenario", $"Goto: {gotoStep.TargetLabel}");
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
            string payload;
            
            // If command name is specified, use BuildCommand with protocol separators
            if (!string.IsNullOrEmpty(step.CommandName))
            {
                payload = _protocolService.BuildCommand(step.CommandName);
                // Substitute any remaining variables in the payload
                payload = _protocolService.SubstituteVariables(payload);
            }
            else
            {
                // Use raw payload
                payload = _protocolService.SubstituteVariables(step.Payload);
            }
            
            await _socketService.SendAsync(payload);
            _logger.Info("Scenario", $"Sent command: {payload}");
        }

        private bool MatchesPattern(string commandName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
                return true;
            
            // Simple wildcard matching
            if (pattern.Contains("*"))
            {
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(commandName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            // Exact match (case insensitive)
            return commandName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private Task<int> ExecuteAutoReplyWithGotoAsync(AutoReplyStep step, Dictionary<string, int> labelMap, int currentIndex, CancellationToken cancellationToken)
        {
            // Get the command pattern to match
            var commandPattern = !string.IsNullOrEmpty(step.CommandName) ? step.CommandName : step.CommandPattern;
            
            // If pattern is empty, don't register the rule (effectively disabling this auto-reply)
            if (string.IsNullOrEmpty(commandPattern))
            {
                _logger.Info("Scenario", $"AutoReply: Empty pattern, skipping");
                return Task.FromResult(currentIndex + 1);
            }
            
            // Register the auto-reply rule
            _activeAutoReplyRules[commandPattern] = step;
            _logger.Info("Scenario", $"AutoReply: Enabled for pattern '{commandPattern}'");
            
            // Continue to next step immediately (non-blocking)
            return Task.FromResult(currentIndex + 1);
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

        private bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return false;

            var variableService = VariableService.Instance;

            // Extract variable name from ${varName}
            var varMatch = Regex.Match(condition, @"\$\{([^}]+)\}");
            if (!varMatch.Success)
                return false;

            var varName = varMatch.Groups[1].Value;
            
            // Get variable value - first from VariableService, then check parsed data
            var varValueStr = variableService.GetVariableString(varName);
            _logger.Debug("Scenario", $"EvaluateCondition START: condition='{condition}', varName='{varName}', variableService='{varValueStr}'");
            
            // Also check parsed data (from received STATUS responses) - use lock for thread safety
            lock (_dataLock)
            {
                var lastCmd = _protocolService.GetParsedValue("LastCommand");
                _logger.Debug("Scenario", $"EvaluateCondition: lastCmd='{lastCmd}'");
                
                if (!string.IsNullOrEmpty(lastCmd))
                {
                    // Try to get the specific parameter from the last command (case-insensitive)
                    var paramValue = _protocolService.GetParsedValue(lastCmd, varName);
                    _logger.Debug("Scenario", $"EvaluateCondition: checking '{lastCmd}.{varName}' = '{paramValue}'");
                    if (!string.IsNullOrEmpty(paramValue))
                    {
                        varValueStr = paramValue;
                    }
                }
                
                // Also check direct parsed data key (e.g., state) - case-insensitive
                if (varValueStr == variableService.GetVariableString(varName))
                {
                    var directParsed = _protocolService.GetParsedValue(varName);
                    _logger.Debug("Scenario", $"EvaluateCondition: checking direct '{varName}' = '{directParsed}'");
                    if (!string.IsNullOrEmpty(directParsed))
                    {
                        varValueStr = directParsed;
                    }
                }
                
                // Debug: dump all parsed data
                var allKeys = string.Join(", ", _protocolService.ParsedData.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                _logger.Debug("Scenario", $"EvaluateCondition: all parsed data: [{allKeys}]");
            }
            
            _logger.Debug("Scenario", $"EvaluateCondition: final varValueStr='{varValueStr}'");

            // Find operator and comparison value - check multi-char operators first
            string op = null;
            int opIndex = -1;

            // Check for >= first (before > to avoid partial match)
            if ((opIndex = condition.IndexOf(">=")) >= 0)
                op = ">=";
            // Check for <= (before <)
            else if ((opIndex = condition.IndexOf("<=")) >= 0)
                op = "<=";
            // Check for != (before ==)
            else if ((opIndex = condition.IndexOf("!=")) >= 0)
                op = "!=";
            // Check for == (after != to avoid matching !=)
            else if ((opIndex = condition.IndexOf("==")) >= 0)
                op = "==";
            // Check for >
            else if ((opIndex = condition.IndexOf(">")) >= 0)
                op = ">";
            // Check for <
            else if ((opIndex = condition.IndexOf("<")) >= 0)
                op = "<";

            if (op == null)
                return false;

            // Extract comparison value after operator
            var compareValueStr = condition.Substring(opIndex + op.Length).Trim();
            _logger.Debug("Scenario", $"EvaluateCondition: compareValueStr='{compareValueStr}'");

            // Try numeric comparison first
            if (double.TryParse(varValueStr, out var varNum) && double.TryParse(compareValueStr, out var compareNum))
            {
                _logger.Debug("Scenario", $"EvaluateCondition: numeric comparison: {varNum} {op} {compareNum}");
                return op switch
                {
                    ">=" => varNum >= compareNum,
                    ">" => varNum > compareNum,
                    "<=" => varNum <= compareNum,
                    "<" => varNum < compareNum,
                    "==" => Math.Abs(varNum - compareNum) < 0.0001, // Use tolerance for float comparison
                    "!=" => Math.Abs(varNum - compareNum) >= 0.0001,
                    _ => false
                };
            }

            // Fall back to string comparison for non-numeric values
            var result = op switch
            {
                "==" => varValueStr.Equals(compareValueStr, StringComparison.OrdinalIgnoreCase),
                "!=" => !varValueStr.Equals(compareValueStr, StringComparison.OrdinalIgnoreCase),
                _ => false // String comparison not supported for >, <, >=, <=
            };
            _logger.Debug("Scenario", $"EvaluateCondition: string result: '{varValueStr}' {op} '{compareValueStr}' = {result}");
            return result;
        }

        public class StepExecutedEventArgs : EventArgs
        {
            public ScenarioStep Step { get; }
            public StepExecutedEventArgs(ScenarioStep step) => Step = step;
        }
    }
}
