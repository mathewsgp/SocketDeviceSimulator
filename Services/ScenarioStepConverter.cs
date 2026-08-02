using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocketSimulator.Models;

namespace SocketSimulator.Services
{
    public class ScenarioStepConverter : JsonConverter<ScenarioStep>
    {
        public override ScenarioStep? ReadJson(JsonReader reader, Type objectType, ScenarioStep? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jsonObject = JObject.Load(reader);
            var typeString = jsonObject["type"]?.ToString() ?? jsonObject["Type"]?.ToString();
            
            ScenarioStep? step = typeString switch
            {
                "WaitCommand" or "WaitCommandStep" => new WaitCommandStep(),
                "WaitResponse" or "WaitResponseStep" => new WaitResponseStep(),
                "SendResponse" or "SendResponseStep" => new SendResponseStep(),
                "SendCommand" or "SendCommandStep" => new SendCommandStep(),
                "Delay" or "DelayStep" => new DelayStep(),
                "SetVariable" or "SetVariableStep" => new SetVariableStep(),
                "IfElse" or "IfElseStep" => new IfElseStep(),
                "Label" or "LabelStep" => new LabelStep(),
                "Goto" or "GotoStep" => new GotoStep(),
                "AutoReply" or "AutoReplyStep" => new AutoReplyStep(),
                _ => null
            };

            if (step == null)
            {
                // Try to infer type from properties
                step = InferStepType(jsonObject);
            }

            if (step != null)
            {
                serializer.Populate(jsonObject.CreateReader(), step);
            }

            return step;
        }

        private ScenarioStep InferStepType(JObject jsonObject)
        {
            // Try to infer type based on properties
            if (jsonObject.ContainsKey("CommandName") && jsonObject.ContainsKey("Payload"))
                return new SendCommandStep();
            if (jsonObject.ContainsKey("CommandName") && jsonObject.ContainsKey("ExpectedResponseContains"))
                return new WaitResponseStep();
            if (jsonObject.ContainsKey("CommandName") && jsonObject.ContainsKey("Response"))
                return new SendResponseStep();
            if (jsonObject.ContainsKey("CommandName") && jsonObject.ContainsKey("TimeoutMs") && !jsonObject.ContainsKey("Payload"))
                return new WaitCommandStep();
            if (jsonObject.ContainsKey("DurationMs"))
                return new DelayStep();
            if (jsonObject.ContainsKey("VariableName"))
                return new SetVariableStep();
            if (jsonObject.ContainsKey("Condition"))
                return new IfElseStep();
            if (jsonObject.ContainsKey("LabelName"))
                return new LabelStep();
            if (jsonObject.ContainsKey("TargetLabel"))
                return new GotoStep();
            if (jsonObject.ContainsKey("CommandPattern"))
                return new AutoReplyStep();
            
            return new DelayStep(); // Default fallback
        }

        public override void WriteJson(JsonWriter writer, ScenarioStep? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var typeName = value switch
            {
                WaitCommandStep => "WaitCommand",
                WaitResponseStep => "WaitResponse",
                SendResponseStep => "SendResponse",
                SendCommandStep => "SendCommand",
                DelayStep => "Delay",
                SetVariableStep => "SetVariable",
                IfElseStep => "IfElse",
                LabelStep => "Label",
                GotoStep => "Goto",
                AutoReplyStep => "AutoReply",
                _ => "Unknown"
            };

            var jsonObject = JObject.FromObject(value, serializer);
            jsonObject.AddFirst(new JProperty("Type", typeName));
            jsonObject.WriteTo(writer);
        }
    }
}
