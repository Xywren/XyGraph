using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    /// <summary>
    /// Pure data node. Reads a graph variable's current runtime value and exposes it
    /// as an output port. No execution ports — wire it like any data source.
    /// </summary>
    public class GetVariableNode : Node
    {
        public Guid variableId;
        public Port outputPort { get; private set; }

        // ── Full constructor (used when placing from sidebar or building workflows) ──

        public GetVariableNode(Graph graph, Guid varId, string varName, Type portType) : base(graph)
        {
            ApplyChrome();

            Initialize(varId, varName, portType);
        }

        // ── Loader constructor (Graph.CreateNodeByType needs a (Graph) ctor) ──

        public GetVariableNode(Graph graph) : base(graph)
        {
            ApplyChrome();
        }

        /// <summary>
        /// A Get node is just a labelled socket — no title bar, no inputs — carrying the
        /// shared variable colour so it reads differently from a graph input node.
        /// </summary>
        private void ApplyChrome()
        {
            titleContainer.Visibility    = Visibility.Collapsed;
            mainContainer.Background     = Brushes.Transparent;
            inputContainer.Visibility    = Visibility.Collapsed;
            topContainer.Visibility      = Visibility.Collapsed;
            outputContainer.CornerRadius = new CornerRadius(6);
            outputContainer.Background   = GraphVariable.ColourBrush;
        }

        public void Initialize(Guid varId, string varName, Type portType)
        {
            variableId = varId;

            string hex = DerivePortColour(portType ?? typeof(object));
            Brush colorBrush = (Brush)new BrushConverter().ConvertFromString(hex);

            Port p = new Port(varName ?? "variable", PortDirection.Output, portType ?? typeof(object),
                              socketSize: 10, color: colorBrush, drawSocketOuterRing: true);
            if (p.label is TextBlock tb)
            {
                tb.Text       = varName ?? "variable";
                tb.FontWeight = FontWeights.Bold;
                tb.Foreground = Brushes.White;
            }
            p.connectionType = ConnectionType.Multi;

            outputContainer.Add(p);   // NodeContainer.Add registers the port on the node
            outputPort = p;
        }

        public void HandleGraphVariableChanged(string varName, Type portType)
        {
            if (outputPort == null) return;
            outputPort.name     = varName ?? string.Empty;
            outputPort.portType = portType ?? typeof(object);

            if (outputPort.label is TextBlock tb)  tb.Text = varName ?? string.Empty;
            if (outputPort.typeLabel != null)
                outputPort.typeLabel.Text = $"<{(portType?.Name ?? "object")}>";

            string hex = DerivePortColour(portType ?? typeof(object));
            outputPort.colour = (Brush)new BrushConverter().ConvertFromString(hex);

            foreach (Edge e in outputPort.edges) e.ReDraw();
        }

        // ── Evaluation ────────────────────────────────────────────────────────────

        public override void Evaluate()
        {
            if (graph == null || outputPort == null) return;
            if (graph.variableValues.TryGetValue(variableId, out object val))
            {
                outputPort.runtimeValue    = val;
                outputPort.hasRuntimeValue = true;
            }
        }

        // ── Serialisation ─────────────────────────────────────────────────────────

        public override JsonObject Save()
        {
            JsonObject obj = base.Save();
            obj["variableId"]   = variableId.ToString();
            obj["variableType"] = outputPort?.portType?.AssemblyQualifiedName ?? string.Empty;
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            // Remove attribute-created output ports before base.Load() adds more
            foreach (Port p in ports.ToList())
                if (p.direction == PortDirection.Output) p.Delete();

            base.Load(obj);

            string idStr = obj["variableId"]?.GetValue<string>() ?? string.Empty;
            if (Guid.TryParse(idStr, out Guid id)) variableId = id;

            // the definition is the authority on name and type; the saved port type is a fallback
            GraphVariable definition = graph?.variableDefinitions.FirstOrDefault(v => v.Id == variableId);
            Type resolved = definition?.ResolvedType
                            ?? GraphVariable.ResolveType(obj["variableType"]?.GetValue<string>() ?? string.Empty);
            string varName = definition?.Name ?? "variable";

            List<Port> outPorts = ports.Where(p => p.direction == PortDirection.Output).ToList();
            if (outPorts.Count > 0)
            {
                outputPort = outPorts[0];
                if (outputPort.label is TextBlock tb) { tb.FontWeight = FontWeights.Bold; tb.Foreground = Brushes.White; }
                HandleGraphVariableChanged(varName, resolved);
            }
            else
            {
                Initialize(variableId, varName, resolved);
            }

            RestorePortGuid(obj, outputPort, PortDirection.Output);
        }
    }
}
