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

        public override JsonObject Save()
        {
            JsonObject obj = base.Save();
            obj["inputId"] = inputId.ToString();
            obj["inputType"] = outputPort?.portType?.AssemblyQualifiedName ?? string.Empty;
            return obj;
        }
    }
}
