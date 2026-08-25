using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    /// <summary>
    /// A secondary entry point. Like Start/End it has no body — just a label and a single
    /// socket. When a matching event is delivered the graph begins executing here.
    ///
    /// The one socket is a fused output: wire it to a Node port and it drives execution;
    /// wire it to a data port and it delivers the event payload. Its look — red inner
    /// circle, black outer ring — signals both roles at once.
    /// </summary>
    public class EventEntryNode : Node
    {
        public Port outputPort { get; private set; }

        private TextBox _channelBox;

        public string channel
        {
            get => _channelBox.Text;
            set => _channelBox.Text = value ?? string.Empty;
        }

        public EventEntryNode(Graph graph) : base(graph)
        {
            title = "On Event";

            // No card — transparent like Start/End.
            titleContainer.Visibility  = Visibility.Collapsed;
            inputContainer.Visibility  = Visibility.Collapsed;
            Background               = Brushes.Transparent;
            innerBorder.Background   = Brushes.Transparent;
            mainContainer.Background = Brushes.Transparent;
            outputContainer.Background = Brushes.Transparent;
            topContainer.Background = Brushes.Transparent;

            // ⚡ + an editable name that reads as plain text, not a boxed field.
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock
            {
                Text = "⚡ ",
                FontSize = 14,
                Foreground = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center
            });
            _channelBox = new TextBox
            {
                Text            = string.Empty,
                MinWidth        = 70,
                BorderThickness = new Thickness(0),
                Background      = Brushes.Transparent,
                Foreground      = Brushes.Black,
                FontWeight      = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            head.Children.Add(_channelBox);
            topContainer.Add(head);

            BuildPort();
        }

        private void BuildPort()
        {
            outputPort = new Port("out", PortDirection.Output, typeof(object), socketSize: 14,
                                  color: Brushes.Red, drawSocketOuterRing: true);
            outputPort.connectionType = ConnectionType.Multi;
            StyleSocket(outputPort);
            if (outputPort.label is TextBlock tb) tb.Visibility = Visibility.Collapsed;
            if (outputPort.typeLabel != null)     outputPort.typeLabel.Visibility = Visibility.Collapsed;
            outputContainer.Add(outputPort);
        }

        // Red inner circle, black outer ring — set separately since SetColor paints both.
        private static void StyleSocket(Port p)
        {
            p.socket.SetColor(Brushes.Red);
            p.socket.BorderBrush = Brushes.Black;
        }

        /// <summary>
        /// Fired by the runtime when a matching event is delivered. Publishes the payload on
        /// the fused port so data targets can pull it, then runs every Node target — the port
        /// drives execution and data at once, per what each edge lands on.
        /// </summary>
        public void Activate(object payload)
        {
            base.Run();
            if (outputPort == null) return;

            outputPort.runtimeValue    = payload;
            outputPort.hasRuntimeValue = true;

            foreach (Edge e in outputPort.edges.ToList())
            {
                Port target = e.inputPort;
                if (target != null && typeof(Node).IsAssignableFrom(target.portType))
                    target.parentContainer?.node?.Run();
            }
        }

        public override void Run() { Activate(null); }
        public override void Completed() { base.Completed(); }

        public override JsonObject Save()
        {
            JsonObject obj = base.Save();
            obj["channel"] = channel;
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            // Drop the attribute-free placeholder before base re-creates ports from JSON.
            foreach (Port p in ports.ToList())
                if (p.direction == PortDirection.Output) p.Delete();

            base.Load(obj);

            channel = obj["channel"]?.GetValue<string>() ?? string.Empty;

            outputPort = ports.FirstOrDefault(p => p.direction == PortDirection.Output);
            if (outputPort != null)
            {
                StyleSocket(outputPort);
                if (outputPort.label is TextBlock tb) tb.Visibility = Visibility.Collapsed;
                if (outputPort.typeLabel != null)     outputPort.typeLabel.Visibility = Visibility.Collapsed;
            }
            else
            {
                BuildPort();
            }

            RestorePortGuid(obj, outputPort, PortDirection.Output);
        }
    }
}
