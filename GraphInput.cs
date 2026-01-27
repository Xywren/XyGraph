using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XyGraph
{
    public class GraphInput
    {
        public Guid ID { get; set; }
        public string name { get; set; }
        public object Value { get; set; }

        public GraphInput()
        {
            ID = Guid.NewGuid();
            name = string.Empty;
            Value = null;
        }

        public JsonObject Save()
        {
            JsonObject obj = new JsonObject();
            obj["id"] = ID.ToString();
            obj["name"] = name ?? string.Empty;
            if (Value != null)
            {
                JsonNode node = JsonSerializer.SerializeToNode(Value);
                obj["value"] = node;
            }
            return obj;
        }

        public void Load(JsonObject obj, Type expectedType = null)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            string idStr = obj["id"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrEmpty(idStr) && Guid.TryParse(idStr, out Guid parsed))
            {
                ID = parsed;
            }

            name = obj["name"]?.GetValue<string>() ?? string.Empty;

            JsonNode node = obj["value"];
            if (node == null)
            {
                Value = null;
                return;
            }

            if (expectedType != null)
            {
                string json = node.ToJsonString();
                object deserialized = JsonSerializer.Deserialize(json, expectedType);
                Value = deserialized;
                return;
            }

            // fallback: keep as JsonNode if no expected CLR type provided
            Value = node;
        }
    }
}
