using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XyGraph
{
    /// <summary>
    /// A graph value that lives in a database row. Implement it on domain objects so the graph
    /// stores what the value *is* rather than what it looked like when it was saved.
    /// </summary>
    public interface IGraphRecord
    {
        int RecordId { get; }
    }

    /// <summary>
    /// Reads and writes the values a graph carries between runs. A record is stored as its type
    /// and id, so a process that waits weeks reloads current data on the next tick instead of
    /// running against a snapshot taken when it was spawned. Everything else is stored as-is.
    /// </summary>
    public static class RecordReference
    {
        private const string TYPE_KEY = "recordType";
        private const string ID_KEY   = "recordId";

        public static JsonNode Write(object value)
        {
            if (value is IGraphRecord record)
            {
                return new JsonObject
                {
                    [TYPE_KEY] = value.GetType().AssemblyQualifiedName,
                    [ID_KEY]   = record.RecordId
                };
            }

            return JsonSerializer.SerializeToNode(value);
        }

        /// <summary>
        /// Returns the record as the database has it now. A port carries a reference to a row,
        /// not a copy of it, so a node reading one days after it was produced still sees current
        /// data. Anything that is not a saved record is passed straight back.
        /// </summary>
        public static object Reload(object value)
        {
            if (value is not IGraphRecord record) return value;
            if (record.RecordId <= 0) return value;

            try { return Activator.CreateInstance(value.GetType(), record.RecordId) ?? value; }
            catch { return value; }
        }

        public static object Read(JsonNode node, Type expectedType)
        {
            if (node == null) return null;

            if (node is JsonObject stored && stored[TYPE_KEY] != null)
            {
                Type recordType = Type.GetType(stored[TYPE_KEY].GetValue<string>() ?? string.Empty);
                if (recordType == null) return null;

                // A record deleted since the process started loads as null, so the node that
                // needs it fails its own null check and routes the process to Error.
                try { return Activator.CreateInstance(recordType, stored[ID_KEY].GetValue<int>()); }
                catch { return null; }
            }

            if (expectedType == null) return null;

            try { return JsonSerializer.Deserialize(node.ToJsonString(), expectedType); }
            catch { return null; }
        }
    }
}
