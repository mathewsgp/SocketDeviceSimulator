using System;
using System.Collections.Generic;
using System.IO;
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

        public ProtocolDefinition? CurrentProtocol => _currentProtocol;

        public event EventHandler? ProtocolChanged;

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

        public string SubstituteVariables(string template)
        {
            var result = template;
            var variableService = VariableService.Instance;

            // Replace ${VariableName} patterns
            var regex = new Regex(@"\$\{([^}]+)\}");
            result = regex.Replace(result, match =>
            {
                var variableName = match.Groups[1].Value;
                return variableService.GetVariableString(variableName);
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
    }
}
