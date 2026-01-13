using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class NodeContainer : Border
    {
        public StackPanel stackPanel { get; private set; }
        public Node node { get; private set; }

        public NodeContainer(Node node, Brush background, Orientation orientation = Orientation.Vertical, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
        {
            this.node = node;
            Background = background;
            stackPanel = new StackPanel { Orientation = orientation, HorizontalAlignment = horizontalAlignment };
            Child = stackPanel;
            Visibility = Visibility.Collapsed;
            MinHeight = Node.MIN_NODE_HEIGHT / 4;
            MinWidth = Node.MIN_NODE_WIDTH / 3;
            Padding = new Thickness(5); // stops content from touching edges
            Margin = new Thickness(-1); // stops 1 pixel pink gaps between containers

            this.SizeChanged += (s, e) => OnResize();
        }

        public void OnResize()
        {
            // if the container is resized, the ports may have moved so we need to re-draw edges
            node.RedrawEdges();
        }

        public void Add(UIElement child)
        {
            stackPanel.Children.Add(child);
            Visibility = Visibility.Visible;
            if (child is Port port)
            {
                node.ports.Add(port);
                node.PortsChanged();
                port.parentContainer = this;
            }
        }
    }
}