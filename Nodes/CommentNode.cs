using System;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    /// <summary>
    /// A sticky note. Carries no ports and never runs — it exists purely to annotate
    /// the graph and explain groupings of nodes.
    /// </summary>
    public class CommentNode : Node
    {
        private const double DEFAULT_WIDTH  = 220;
        private const double DEFAULT_HEIGHT = 120;

        private readonly TextBox _textBox;

        public string text
        {
            get => _textBox.Text;
            set => _textBox.Text = value ?? string.Empty;
        }

        public CommentNode(Graph graph) : base(graph)
        {
            title = "Comment";

            titleContainer.Background = (Brush)new BrushConverter().ConvertFromString("#FFD9A5");
            inputContainer.Visibility  = Visibility.Collapsed;
            outputContainer.Visibility = Visibility.Collapsed;
            topContainer.Visibility    = Visibility.Collapsed;
            bottomContainer.Visibility = Visibility.Collapsed;

            _textBox = new TextBox
            {
                Text                    = string.Empty,
                AcceptsReturn           = true,
                TextWrapping            = TextWrapping.Wrap,
                BorderThickness         = new Thickness(0),
                Background              = (Brush)new BrushConverter().ConvertFromString("#FFF6D8"),
                Width                   = DEFAULT_WIDTH,
                Height                  = DEFAULT_HEIGHT,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding                 = new Thickness(6)
            };
            _textBox.LostFocus += (s, e) => NotifyChanged();

            mainContainer.Visibility = Visibility.Visible;
            mainContainer.Child      = _textBox;
        }

        // Never participates in execution.
        public override void Run() { }

        public override JsonObject Save()
        {
            JsonObject obj = base.Save();
            obj["text"]   = text;
            obj["width"]  = _textBox.Width;
            obj["height"] = _textBox.Height;
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            base.Load(obj);

            text = obj["text"]?.GetValue<string>() ?? string.Empty;

            double? width  = obj["width"]?.GetValue<double>();
            double? height = obj["height"]?.GetValue<double>();
            if (width  is > 0) _textBox.Width  = width.Value;
            if (height is > 0) _textBox.Height = height.Value;
        }
    }
}
