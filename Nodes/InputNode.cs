using System;
using System.Text.Json.Nodes;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace XyGraph
{
    public class InputNode : Node
    {
        public Guid inputId;
        public Port outputPort;
        public InputNode(Graph graph, Guid inputId, string name, Type portType) : base(graph)
        {
            Initialize(inputId, name, portType);
        }

        // Minimal constructor used by the Graph loader so it can create the concrete type
        public InputNode(Graph graph) : base(graph)
        {
            // Configure visuals similar to the full constructor but do not create the output port here.
            titleContainer.Visibility = Visibility.Collapsed;
            mainContainer.Background = Brushes.Transparent;
            inputContainer.Visibility = Visibility.Collapsed;
            topContainer.Visibility = Visibility.Collapsed;

            // make the output container have rounded corners for this input node
            outputContainer.CornerRadius = new CornerRadius(6);
        }

        // Initialize performs the original constructor work so callers can create fully-initialized InputNodes
        public void Initialize(Guid inputId, string name, Type portType)
            {
            this.inputId = inputId;

            // Visuals
            this.title = name;
            titleContainer.Visibility = Visibility.Collapsed;
            mainContainer.Background = Brushes.Transparent;
            inputContainer.Visibility = Visibility.Collapsed;
            topContainer.Visibility = Visibility.Collapsed;

            // make the output container have rounded corners for this input node
            outputContainer.CornerRadius = new CornerRadius(6);

            // create a single output port for this input node
            Type resolvedType = portType ?? typeof(object);
            string hex = Common.HashColour(resolvedType.ToString());
            BrushConverter conv = new BrushConverter();
            Brush colorBrush = (Brush)conv.ConvertFromString(hex);
            Port p = new Port(name, PortDirection.Output, resolvedType, socketSize: 10, color: colorBrush, drawSocketOuterRing: true);

            if (p.label is TextBlock tb)
            {
                tb.Text = name ?? string.Empty;
                tb.FontWeight = FontWeights.Bold;
                tb.Foreground = Brushes.White;
            }

            p.connectionType = ConnectionType.Multi;

            outputContainer.Add(p);
            ports.Add(p);
            outputPort = p;
        }

        public override void Evaluate()
        {
            if (graph == null || outputPort == null) return;

            string key = inputId.ToString();
            if (graph.inputValues.ContainsKey(key))
            {
                GraphInput graphInput = graph.inputValues[key];
                outputPort.runtimeValue = graphInput.Value;
                outputPort.hasRuntimeValue = true;
            }
        }

        // Update this InputNode from a master definition (name and type)
        public void HandleGraphInputChange(string name, Type portType)
        {
            // update title via the public property so the UI is refreshed
            this.title = name ?? string.Empty;

            // update port
            if (outputPort != null)
            {
                outputPort.name = name ?? string.Empty;
                outputPort.portType = portType ?? typeof(object);
                // update label visual
                try { if (outputPort.label is TextBlock tb) { tb.Text = name ?? string.Empty; } } catch { }
                
                if (outputPort.typeLabel != null)
                {
                    string typeName = (outputPort.portType != null) ? outputPort.portType.Name : "object";
                    outputPort.typeLabel.Text = $"<{typeName}>";
                    // align type label consistent with port direction
                    outputPort.typeLabel.HorizontalAlignment = outputPort.direction == PortDirection.Input ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                    outputPort.typeLabel.TextAlignment = outputPort.direction == PortDirection.Input ? TextAlignment.Left : TextAlignment.Right;
                }

                // update socket colour
                string hex = Common.HashColour((outputPort.portType ?? typeof(object)).ToString());
                BrushConverter conv = new BrushConverter();
                Brush colorBrush = (Brush)conv.ConvertFromString(hex);
                outputPort.colour = colorBrush;

                // redraw edges connected to this port
                foreach (Edge e in outputPort.edges)
                {
                    e.ReDraw();
                }
            }
        }

        public override JsonObject Save()
        {
            JsonObject obj = base.Save();
            obj["inputId"] = inputId.ToString();
            obj["inputType"] = outputPort?.portType?.AssemblyQualifiedName ?? string.Empty;
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            // Remove any output ports that may have been created by the constructor to avoid duplication
            try
            {
                foreach (Port existing in ports.ToList())
                {
                    if (existing.direction == PortDirection.Output)
                    {
                        existing.Delete();
                    }
                }
            }
            catch { }

            // Let base.Load reconstruct ports and basic state
            base.Load(obj);

            // restore inputId
            string idStr = obj["inputId"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(idStr) && Guid.TryParse(idStr, out Guid parsed))
            {
                inputId = parsed;
            }

            // resolve saved type
            string typeName = obj["inputType"]?.GetValue<string>() ?? string.Empty;
            Type resolvedType = typeof(object);
            if (!string.IsNullOrEmpty(typeName))
            {
                try { resolvedType = System.Type.GetType(typeName) ?? typeof(object); } catch { resolvedType = typeof(object); }
            }

            // collect output ports created by base.Load
            List<Port> outputPorts = ports.Where(p => p.direction == PortDirection.Output).ToList();

            if (outputPorts.Count > 0)
            {
                // keep the first output port, remove any extras
                outputPort = outputPorts[0];
                for (int i = 1; i < outputPorts.Count; i++)
                {
                    try { outputPorts[i].Delete(); } catch { }
                }

                // ensure label visual matches InputNode style
                try
                {
                    if (outputPort.label is TextBlock tb)
                    {
                        tb.Text = outputPort.name ?? (obj["type"]?.GetValue<string>() ?? "Input");
                        tb.FontWeight = FontWeights.Bold;
                        tb.Foreground = Brushes.White;
                    }
                    if (outputPort.typeLabel != null)
                    {
                        string typeLabelName = (outputPort.portType != null) ? outputPort.portType.Name : "object";
                        outputPort.typeLabel.Text = $"<{typeLabelName}>";
                    }
                }
                catch { }
            }
            else
            {
                // no output port reconstructed; create one using saved metadata
                string nameForPort = obj["type"]?.GetValue<string>() ?? "Input";
                Initialize(inputId, nameForPort, resolvedType);
            }
        }
    }
}
