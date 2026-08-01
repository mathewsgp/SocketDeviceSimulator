using System.Collections.Generic;

namespace SocketSimulator.Models
{
    public class ProtocolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public List<CommandDefinition> Commands { get; set; } = new();
    }

    public class CommandDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ParameterDefinition> Parameters { get; set; } = new();
        public string? ResponseTemplate { get; set; }
        public string? Payload { get; set; }
    }

    public class ParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public List<string> PossibleValues { get; set; } = new();
        public bool Required { get; set; } = true;
    }
}
