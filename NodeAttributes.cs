using System;

namespace XyGraph
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NodeInputAttribute : Attribute
    {
        // Optional override for the port display name
        public string Name { get; set; } = string.Empty;
        // Optional color name or hex for the port socket (defaults to Black)
        public string Color { get; set; } = "Black";
        // Connection type: default for inputs is Multi
        public ConnectionType ConnectionType { get; set; } = ConnectionType.Multi;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NodeOutputAttribute : Attribute
    {
        // Optional override for the port display name
        public string Name { get; set; } = string.Empty;
        // Optional color name or hex for the port socket (defaults to Black)
        public string Color { get; set; } = "Black";
        // Connection type: default for outputs is Single
        public ConnectionType ConnectionType { get; set; } = ConnectionType.Single;
    }
}
