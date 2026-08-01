using System.Collections.Generic;

namespace SocketSimulator.Models
{
    public class Scenario
    {
        public string Name { get; set; } = "Untitled Scenario";
        public string Description { get; set; } = string.Empty;
        public List<ScenarioStep> Steps { get; set; } = new();
    }

    public abstract class ScenarioStep
    {
        public int Order { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class WaitCommandStep : ScenarioStep
    {
        public string CommandName { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 5000;
    }

    public class SendResponseStep : ScenarioStep
    {
        public string CommandName { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
    }

    public class SendCommandStep : ScenarioStep
    {
        public string CommandName { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    public class DelayStep : ScenarioStep
    {
        public int DurationMs { get; set; } = 1000;
    }

    public class SetVariableStep : ScenarioStep
    {
        public string VariableName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public VariableOperation Operation { get; set; } = VariableOperation.Set;
    }

    public enum VariableOperation
    {
        Set,
        Increment,
        Decrement,
        Toggle
    }

    public class IfElseStep : ScenarioStep
    {
        public string Condition { get; set; } = string.Empty;
        public List<ScenarioStep> IfTrue { get; set; } = new();
        public List<ScenarioStep> IfFalse { get; set; } = new();
    }
}
