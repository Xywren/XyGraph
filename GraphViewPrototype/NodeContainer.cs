using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class NodeContainer : Border
    {
        private StackPanel stackPanel;
        private Node node;

        public NodeContainer(Node node, Brush background, Orientation orientation = Orientation.Vertical, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
        {
            this.node = node;
            Background = background;
            stackPanel = new StackPanel { Orientation = orientation, HorizontalAlignment = horizontalAlignment };
            Child = stackPanel;
            Visibility = Visibility.Collapsed;
            MinHeight = Node.MIN_NODE_HEIGHT / 4;
            MinWidth = Node.MIN_NODE_WIDTH / 3;
            Padding = new Thickness(5);
        }

        public void Add(UIElement child)
        {
            stackPanel.Children.Add(child);
            Visibility = Visibility.Visible;
            if (child is Port port)
            {
                node.Ports.Add(port);
            }
        }
    }
}