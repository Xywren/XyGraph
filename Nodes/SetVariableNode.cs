using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    /// <summary>
    /// Execution node. Reads its 'value' input port and stores it in the graph's
    /// variable store under variableId, then continues execution via Next.
    /// </summary>
    public class SetVariableNode : Node
    {
        [NodeInput(Color  = "#FF000000")] public Node execute;
        [NodeOutput(Name  = "Next", Color = "#FF000000")] public Node next;
        [NodeInput(Name   = "value")] public object value;   // type updated dynamically in Initialize

        public Guid variableId;
        private Port valuePort;

        // ── Full constructor ───────────────────────────────────────────────────────

        public SetVariableNode(Graph graph, Guid varId, string varName, Type portType) : base(graph)
        {
            Initialize(varId, varName, portType);
        }

        // ── Loader constructor ────────────────────────────────────────────────────

        public SetVariableNode(Graph graph) : base(graph) { }

        public void Initialize(Guid varId, string varName, Type portType)
        {
            variableId = varId;
            title      = $"Set: {varName}";

            // Colour the title bar with the variable's type colour
            string titleHex = Common.HashColour((portType ?? typeof(object)).ToString());
            titleContainer.Background = (Brush)new BrushConverter().ConvertFromString(titleHex);

            // Update the 'value' port that was auto-created from the [NodeInput] attribute
            valuePort = ports.FirstOrDefault(p => p.name == "value" && p.direction == PortDirection.Input);
            if (valuePort != null)
            {
                valuePort.portType = portType ?? typeof(object);

                if (valuePort.label is TextBlock tb)
                {
                    tb.Text       = varName ?? "value";
                    tb.FontWeight = FontWeights.Bold;
                    tb.Foreground = Brushes.White;
                }

                if (valuePort.typeLabel != null)
                    valuePort.typeLabel.Text = $"<{(portType?.Name ?? "object")}>";

                string hex = DerivePortColour(portType ?? typeof(object));
                valuePort.colour = (Brush)new BrushConverter().ConvertFromString(hex);

                foreach (Edge e in valuePort.edges) e.ReDraw();
            }
        }

        // ── Execution ─────────────────────────────────────────────────────────────

        public override void Run()
        {
            base.Run();                        // PopulateInputs fills this.value from connected port
            if (graph != null && variableId != Guid.Empty)
                graph.variableValues[variableId] = value;
            Completed();
        }

        public override void Completed()
        {
            base.Completed();
            next?.Run();
        }

        public override void Error() { base.Error(); }

        // ── Serialisation ─────────────────────────────────────────────────────────

        public override JsonObject Save()
        {
            JsonObject obj = base.Save();
            obj["variableId"]   = variableId.ToString();
            obj["variableType"] = (valuePort?.portType ?? typeof(object)).AssemblyQualifiedName;
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            base.Load(obj);

            string idStr = obj["variableId"]?.GetValue<string>() ?? string.Empty;
            if (Guid.TryParse(idStr, out Guid id)) variableId = id;

            // the definition is the authority on name and type; the saved port type is a fallback
            GraphVariable definition = graph?.variableDefinitions.FirstOrDefault(v => v.Id == variableId);
            Type resolved = definition?.ResolvedType
                            ?? GraphVariable.ResolveType(obj["variableType"]?.GetValue<string>() ?? string.Empty);

            Initialize(variableId, definition?.Name ?? "variable", resolved);
        }
    }
}
