using System;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    /// <summary>
    /// Fires a named event at one target instance, then continues. The target is a domain id
    /// (e.g. an account or case id); the engine resolves it to the live graph instance via the
    /// Kraken object and delivers the event to its matching EventEntry. Not a broadcast.
    /// </summary>
    public class EmitEventNode : Node
    {
        [NodeInput(Color = "#FF000000")] public Node execute;
        [NodeInput] public Process target;    // the in-flight process to deliver to
        [NodeInput] public object  payload;   // typed; engine transports DB objects by (type,id)

        [NodeOutput(Name = "Next", Color = "#FF000000")] public Node next;

        private TextBox _channelBox;

        public string channel
        {
            get => _channelBox.Text;
            set => _channelBox.Text = value ?? string.Empty;
        }

        public EmitEventNode(Graph graph) : base(graph)
        {
            title = "Emit Event";
            titleContainer.Background = (Brush)new BrushConverter().ConvertFromString("#FF1565C0");

            _channelBox = new TextBox { Text = string.Empty, MinWidth = 120, Margin = new Thickness(2) };
            topContainer.Add(_channelBox);
        }

        // Delivery is handled by the engine; node just routes execution onward for now.
        public override void Run() { base.Run(); Completed(); }

        public override void Completed()
        {
            base.Completed();
            next?.Run();
        }

        public override JsonObject Save()
        {
            JsonObject obj = base.Save();
            obj["channel"] = channel;
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            base.Load(obj);
            channel = obj["channel"]?.GetValue<string>() ?? string.Empty;
        }
    }
}
