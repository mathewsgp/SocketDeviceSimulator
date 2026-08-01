using System.Collections.Generic;

namespace SocketSimulator.Models
{
    public class ProtocolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        
        // Command format separators (can be overridden per command)
        public string StartPrefix { get; set; } = "*";      // Prefix before command (e.g., "*")
        public string EndPrefix { get; set; } = "#";          // Suffix after command (e.g., "#")
        public string CommandParameterSeparator { get; set; } = "=";  // Between command and first param (e.g., "=")
        public string ParameterSeparator { get; set; } = ",";  // Between parameters (e.g., ",")
        public string? ParameterValueSeparator { get; set; }   // Between param name and value (e.g., ":")
        
        public List<CommandDefinition> Commands { get; set; } = new();
    }

    public class CommandDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ParameterDefinition> Parameters { get; set; } = new();
        public string? ResponseTemplate { get; set; }
        
        // Per-command format overrides (optional)
        public string? StartPrefix { get; set; }
        public string? EndPrefix { get; set; }
        public string? CommandParameterSeparator { get; set; }
        public string? ParameterSeparator { get; set; }
        public string? ParameterValueSeparator { get; set; }
        
        // Computed command string (e.g., "*LOGIN=value1,param2:value2#")
        public string? Payload { get; set; }
    }

    public class ParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public List<string> PossibleValues { get; set; } = new();
        public bool Required { get; set; } = true;
        // Default value index (0 = first value from PossibleValues)
        public int DefaultValueIndex { get; set; } = 0;
    }
}
