using System;

namespace XyGraph
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NodeInputAttribute : Attribute
    {
        // Optional override for the port display name. Null means use member name; empty string means show no label.
        public string Name { get; set; } = null;
        // Optional color name or hex for the port socket (defaults to Black)
        public string Color { get; set; }
        // Connection type: default for inputs is Multi
        public ConnectionType ConnectionType { get; set; } = ConnectionType.Multi;
        // Optional socket size
        public int SocketSize { get; set; } = 10;
        // whether to draw outer ring around socket
        public bool DrawOuterRing { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NodeOutputAttribute : Attribute
    {
        // Optional override for the port display name. Null means use member name; empty string means show no label.
        public string Name { get; set; } = null;
        // Optional color name or hex for the port socket (defaults to Black)
        public string Color { get; set; }
        // Connection type: default for outputs is Single
        public ConnectionType? ConnectionType { get; set; } = null;
        // Optional socket size
        public int SocketSize { get; set; } = 10;
        // whether to draw outer ring around socket
        public bool DrawOuterRing { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NodeMultiOutputAttribute : Attribute
    {
        // Optional override for the port display name. Null means use member name; empty string means show no label.
        public string Name { get; set; } = null;
        // Optional color name or hex for the port socket (defaults to Black)
        public string Color { get; set; }
        // Connection type: default for multi-outputs is Multi
        public ConnectionType ConnectionType { get; set; } = ConnectionType.Multi;
        // Optional socket size
        public int SocketSize { get; set; } = 10;
        // whether to draw outer ring around socket
        public bool DrawOuterRing { get; set; } = true;
    }
}
