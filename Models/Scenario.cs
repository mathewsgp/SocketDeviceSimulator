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
        public List<ScenarioStep> IfTrue { get; set; } = new();
        public List<ScenarioStep> IfFalse { get; set; } = new();
    }

    public class LoopUntilStep : ScenarioStep
    {
        // What to send in each iteration
        public string CommandToSend { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        
        // What to check in the response
        public string ExpectedResponseContains { get; set; } = string.Empty;
        
        // Loop control
        public int IntervalMs { get; set; } = 1000;
        public int MaxIterations { get; set; } = 60;
        
        // Actions to perform before sending command (e.g., increment counter)
        public List<ScenarioStep> PreActions { get; set; } = new();
        
        // What to do after condition is met
        public List<ScenarioStep> OnSuccess { get; set; } = new();
        
        // What to do on timeout
        public List<ScenarioStep> OnTimeout { get; set; } = new();
    }
}
