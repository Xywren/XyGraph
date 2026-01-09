using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class EndNode : Border
    {
        public Port Port { get; private set; }

        public EndNode()
        {
            Port = new Port("", NodeType.Input, null, 20);
            Port.IsEditable = false;
            Port.IsRemovable = false;
            Border outline = new Border
            {
                Width = 28,
                Height = 28,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12)
            };
            Grid g = Port.Child as Grid;
            g.Children.Add(outline);
            Child = Port;
        }
    }
}