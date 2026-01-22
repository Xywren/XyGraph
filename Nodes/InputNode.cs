using System;
using System.Text.Json.Nodes;
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

            p.connectionType = ConnectionType.Single;

            outputContainer.Add(p);
            ports.Add(p);
            outputPort = p;
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
    }
}
