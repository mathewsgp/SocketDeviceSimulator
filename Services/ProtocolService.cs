using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SocketSimulator.Models;

namespace SocketSimulator.Services
{
    public class ProtocolService
    {
        private static ProtocolService? _instance;
        public static ProtocolService Instance => _instance ??= new ProtocolService();

        private ProtocolDefinition? _currentProtocol;
        private readonly LoggingService _logger = LoggingService.Instance;
        
        // Parsed data from received commands/responses
        private readonly Dictionary<string, string> _parsedData = new();

        public ProtocolDefinition? CurrentProtocol => _currentProtocol;
        public Dictionary<string, string> ParsedData => _parsedData;

        public event EventHandler? ProtocolChanged;
        public event EventHandler? ParsedDataChanged;

        private ProtocolService() { }

        public ProtocolDefinition? LoadProtocol(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var protocol = JsonConvert.DeserializeObject<ProtocolDefinition>(json);
                
                if (protocol != null)
                {
                    _currentProtocol = protocol;
                    _logger.Info("Protocol", $"Loaded protocol: {protocol.Name} v{protocol.Version}");
                    _logger.Info("Protocol", $"Format: {protocol.StartPrefix}CMD{protocol.CommandParameterSeparator}param{protocol.ParameterSeparator}...{protocol.EndPrefix}");
                    ProtocolChanged?.Invoke(this, EventArgs.Empty);
                }
                
                return protocol;
            }
            catch (Exception ex)
            {
                _logger.Error("Protocol", $"Failed to load protocol: {ex.Message}");
                return null;
            }
        }

        public void SaveProtocol(string filePath, ProtocolDefinition protocol)
        {
            try
            {
                var json = JsonConvert.SerializeObject(protocol, Formatting.Indented);
                File.WriteAllText(filePath, json);
                _logger.Info("Protocol", $"Saved protocol to: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error("Protocol", $"Failed to save protocol: {ex.Message}");
            }
        }

        /// <summary>
        /// Build a command string using the protocol separators
        /// Format: {StartPrefix}{Command}{CommandParameterSeparator}{Param1}{ValueSep}{Value1}{ParamSep}{Param2}{ValueSep}{Value2}{EndPrefix}
        /// If no ParameterValueSeparator: {StartPrefix}{Command}{CommandParameterSeparator}{Value1}{ParamSep}{Value2}{EndPrefix}
        /// </summary>
        public string BuildCommand(string commandName, Dictionary<string, string>? parameterValues = null)
        {
            if (_currentProtocol == null)
                return commandName;

            var command = _currentProtocol.Commands.Find(c => c.Name == commandName);
            if (command == null)
                return commandName;

            // Get separators (command overrides or protocol defaults)
            var startPrefix = command.StartPrefix ?? _currentProtocol.StartPrefix;
            var endPrefix = command.EndPrefix ?? _currentProtocol.EndPrefix;
            var cmdParamSep = command.CommandParameterSeparator ?? _currentProtocol.CommandParameterSeparator;
            var paramSep = command.ParameterSeparator ?? _currentProtocol.ParameterSeparator;
            var valueSep = command.ParameterValueSeparator ?? _currentProtocol.ParameterValueSeparator;

            var sb = new StringBuilder();
            sb.Append(startPrefix);
            sb.Append(commandName);

            // Build parameter string
            var paramParts = new List<string>();
            var paramIndex = 0;
            
            foreach (var param in command.Parameters)
            {
                string? value = null;
                
                // Try to get value from provided dictionary
                if (parameterValues != null && parameterValues.TryGetValue(param.Name, out var providedValue))
                {
                    value = providedValue;
                }
                // Try to get from parsed data
                else if (_parsedData.TryGetValue($"{commandName}.{param.Name}", out var parsedValue))
                {
                    value = parsedValue;
                }
                // Use default value from PossibleValues
                else if (param.PossibleValues.Count > 0 && param.DefaultValueIndex < param.PossibleValues.Count)
                {
                    value = param.PossibleValues[param.DefaultValueIndex];
                }
                
                if (value != null)
                {
                    if (!string.IsNullOrEmpty(valueSep))
                    {
                        // Include parameter name with value separator
                        paramParts.Add($"{param.Name}{valueSep}{value}");
                    }
                    else
                    {
                        // Value only (no parameter name)
                        paramParts.Add(value);
                    }
                }
                paramIndex++;
            }

            if (paramParts.Count > 0)
            {
                sb.Append(cmdParamSep);
                sb.Append(string.Join(paramSep, paramParts));
            }

            sb.Append(endPrefix);
            return sb.ToString();
        }

        /// <summary>
        /// Parse a received command string and store parsed values
        /// Returns the command name and extracted parameters
        /// </summary>
        public ParsedCommand ParseCommand(string rawMessage)
        {
            var result = new ParsedCommand { RawMessage = rawMessage };
            
            if (_currentProtocol == null)
                return result;

            // Try to extract command using protocol separators
            var startPrefix = _currentProtocol.StartPrefix;
            var endPrefix = _currentProtocol.EndPrefix;
            var cmdParamSep = _currentProtocol.CommandParameterSeparator;
            var paramSep = _currentProtocol.ParameterSeparator;
            var valueSep = _currentProtocol.ParameterValueSeparator;

            // Remove start and end prefixes
            var content = rawMessage;
            
            if (!string.IsNullOrEmpty(startPrefix) && content.StartsWith(startPrefix))
            {
                content = content.Substring(startPrefix.Length);
            }
            
            if (!string.IsNullOrEmpty(endPrefix) && content.EndsWith(endPrefix))
            {
                content = content.Substring(0, content.Length - endPrefix.Length);
            }

            // Split command from parameters
            string commandName;
            string? paramString = null;

            if (!string.IsNullOrEmpty(cmdParamSep) && content.Contains(cmdParamSep))
            {
                var parts = content.Split(new[] { cmdParamSep }, 2, StringSplitOptions.None);
                commandName = parts[0];
                paramString = parts.Length > 1 ? parts[1] : null;
            }
            else
            {
                commandName = content;
            }

            result.CommandName = commandName.Trim();

            // Find command definition for parameter parsing
            var commandDef = _currentProtocol.Commands.Find(c => c.Name.Equals(result.CommandName, StringComparison.OrdinalIgnoreCase));
            
            // Parse parameters
            if (!string.IsNullOrEmpty(paramString))
            {
                var paramParts = paramString.Split(new[] { paramSep }, StringSplitOptions.None);
                
                for (int i = 0; i < paramParts.Length; i++)
                {
                    var paramPart = paramParts[i];
                    
                    if (!string.IsNullOrEmpty(valueSep) && paramPart.Contains(valueSep))
                    {
                        // Has parameter name and value
                        var valueParts = paramPart.Split(new[] { valueSep }, 2, StringSplitOptions.None);
                        var paramName = valueParts[0];
                        var paramValue = valueParts.Length > 1 ? valueParts[1] : string.Empty;
                        
                        result.Parameters[paramName] = paramValue;
                        _parsedData[$"{result.CommandName}.{paramName}"] = paramValue;
                    }
                    else if (commandDef != null && i < commandDef.Parameters.Count)
                    {
                        // Value only - use parameter definition order
                        var paramDef = commandDef.Parameters[i];
                        result.Parameters[paramDef.Name] = paramPart;
                        _parsedData[$"{result.CommandName}.{paramDef.Name}"] = paramPart;
                    }
                    else
                    {
                        // Unknown parameter, use index
                        result.Parameters[$"param{i}"] = paramPart;
                        _parsedData[$"{result.CommandName}.param{i}"] = paramPart;
                    }
                }
            }

            // Store command itself
            _parsedData[$"{result.CommandName}"] = rawMessage;
            _parsedData["LastCommand"] = result.CommandName;
            
            _logger.Debug("Protocol", $"Parsed command: {result.CommandName}, Params: {string.Join(", ", result.Parameters.Select(p => $"{p.Key}={p.Value}"))}");
            
            // Notify UI that parsed data changed
            ParsedDataChanged?.Invoke(this, EventArgs.Empty);
            
            return result;
        }

        /// <summary>
        /// Parse a response and store for later use
        /// </summary>
        public void ParseAndStore(string rawResponse)
        {
            var parsed = ParseCommand(rawResponse);
            if (!string.IsNullOrEmpty(parsed.CommandName))
            {
                _parsedData["LastResponse"] = rawResponse;
                _logger.Debug("Protocol", $"Stored response: {parsed.CommandName}");
            }
            // Notify UI that parsed data changed
            ParsedDataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Get a stored value by key (supports ${Parsed.Key} syntax)
        /// </summary>
        public string GetParsedValue(string key)
        {
            if (_parsedData.TryGetValue(key, out var value))
                return value;
            
            // Try without prefix
            if (_parsedData.TryGetValue(key.Replace("Parsed.", ""), out value))
                return value;
                
            return string.Empty;
        }

        /// <summary>
        /// Get a stored value using dot notation: CommandName.ParameterName
        /// </summary>
        public string GetParsedValue(string commandName, string parameterName)
        {
            var key = $"{commandName}.{parameterName}";
            if (_parsedData.TryGetValue(key, out var value))
                return value;
            return string.Empty;
        }

        /// <summary>
        /// Clear all parsed data
        /// </summary>
        public void ClearParsedData()
        {
            _parsedData.Clear();
            _logger.Debug("Protocol", "Cleared parsed data");
        }

        /// <summary>
        /// Substitute variables including parsed data
        /// </summary>
        public string SubstituteVariables(string template)
        {
            var result = template;
            var variableService = VariableService.Instance;

            // Replace ${VariableName} patterns (variables)
            var regex = new Regex(@"\$\{([^}]+)\}");
            result = regex.Replace(result, match =>
            {
                var key = match.Groups[1].Value;
                
                // Check parsed data first
                if (_parsedData.TryGetValue(key, out var parsedValue))
                    return parsedValue;
                    
                // Then check variables
                return variableService.GetVariableString(key);
            });

            return result;
        }

        public string BuildResponse(string commandName, Dictionary<string, string>? parameters = null)
        {
            if (_currentProtocol == null)
                return string.Empty;

            var command = _currentProtocol.Commands.Find(c => c.Name == commandName);
            if (command == null || string.IsNullOrEmpty(command.ResponseTemplate))
                return string.Empty;

            var response = command.ResponseTemplate;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    response = response.Replace($"${{{param.Key}}}", param.Value);
                }
            }

            return SubstituteVariables(response);
        }

        public CommandDefinition? GetCommand(string name)
        {
            return _currentProtocol?.Commands.Find(c => c.Name == name);
        }

        public List<string> GetCommandNames()
        {
            var names = new List<string>();
            if (_currentProtocol != null)
            {
                foreach (var cmd in _currentProtocol.Commands)
                {
                    names.Add(cmd.Name);
                }
            }
            return names;
        }

        public List<string> GetResponseTemplates()
        {
            var templates = new List<string>();
            if (_currentProtocol != null)
            {
                foreach (var cmd in _currentProtocol.Commands)
                {
                    if (!string.IsNullOrEmpty(cmd.ResponseTemplate))
                    {
                        templates.Add(cmd.ResponseTemplate);
                    }
                }
            }
            return templates;
        }
    }

    /// <summary>
    /// Result of parsing a command
    /// </summary>
    public class ParsedCommand
    {
        public string RawMessage { get; set; } = string.Empty;
        public string CommandName { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
