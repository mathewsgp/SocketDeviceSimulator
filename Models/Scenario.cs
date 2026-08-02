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
        // Goto on success (command received)
        public string? GotoLabelOnSuccess { get; set; }
        // Goto on timeout
        public string? GotoLabelOnTimeout { get; set; }
    }

    // Wait for a response pattern from client
    public class WaitResponseStep : ScenarioStep
    {
        // Command from Protocol to get response template
        public string CommandName { get; set; } = string.Empty;
        // Custom pattern to wait for (overrides protocol template if empty)
        public string ExpectedResponseContains { get; set; } = string.Empty;
        // Optional parameter values to match in response
        public string ExpectedParameters { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 5000;
        // Goto on success (pattern found)
        public string? GotoLabelOnSuccess { get; set; }
        // Goto on timeout
        public string? GotoLabelOnTimeout { get; set; }
    }

    public class SendResponseStep : ScenarioStep
    {
        // Command from Protocol to use as response
        public string CommandName { get; set; } = string.Empty;
        // Response template from Protocol or custom
        public string Response { get; set; } = string.Empty;
        // Reference to last received command payload: ${ReceivedPayload}
        // Reference to parsed parameters: ${Param.ParameterName}
        public string PayloadTemplate { get; set; } = string.Empty;
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
        // Actions to execute if condition is true
        public List<ScenarioStep> IfTrue { get; set; } = new();
        // Actions to execute if condition is false
        public List<ScenarioStep> IfFalse { get; set; } = new();
        // OR: Goto a label instead of executing actions
        public string? GotoLabelIfTrue { get; set; }
        public string? GotoLabelIfFalse { get; set; }
    }

    // Label - marks a position in the scenario for Goto
    public class LabelStep : ScenarioStep
    {
        public string LabelName { get; set; } = string.Empty;
    }

    // Goto - jumps to a labeled position
    public class GotoStep : ScenarioStep
    {
        public string TargetLabel { get; set; } = string.Empty;
    }

    // Auto Reply - automatically respond to specific commands
    public class AutoReplyStep : ScenarioStep
    {
        // Command pattern to match (supports wildcards like "*")
        public string CommandPattern { get; set; } = string.Empty;
        // Response to send when command is received
        public string Response { get; set; } = string.Empty;
        // Optional: specific command name from Protocol
        public string? CommandName { get; set; }
    }
}
