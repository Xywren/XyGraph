using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class NodeContainer : Border
    {
        private StackPanel stackPanel { get;  set; }
        public Node node { get; private set; }

        public NodeContainer(Node node, Brush background, Orientation orientation = Orientation.Vertical, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
        {
            this.node = node;
            Background = background;
            stackPanel = new StackPanel { Orientation = orientation, HorizontalAlignment = horizontalAlignment };
            Child = stackPanel;
            Visibility = Visibility.Collapsed;
            MinHeight = 5;
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
                //node.PortsChanged();
                port.parentContainer = this;
            }
        }

        public void InsertAt(int index, UIElement child)
        {
            if (index < 0) index = 0;
            if (index > stackPanel.Children.Count) index = stackPanel.Children.Count;
            stackPanel.Children.Insert(index, child);
            Visibility = Visibility.Visible;
            if (child is Port port)
            {
                // insert into node ports list at corresponding position
                node.ports.Add(port);
                port.parentContainer = this;
            }
        }

        // Adds a port with an extra UIElement alongside it in a horizontal row.
        public void AddWithSideContent(Port port, UIElement sideContent)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(port);
            row.Children.Add(sideContent);
            stackPanel.Children.Add(row);
            Visibility = Visibility.Visible;
            node.ports.Add(port);
            port.parentContainer = this;
        }

        // Finds an already-registered port in the stack, removes its visual row,
        // and re-inserts it wrapped in a horizontal row with side content.
        public void WrapPortWithSideContent(Port port, UIElement sideContent)
        {
            // find the existing visual entry for this port
            int index = stackPanel.Children.IndexOf(port);
            if (index < 0) return;

            stackPanel.Children.RemoveAt(index);

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(port);
            row.Children.Add(sideContent);
            stackPanel.Children.Insert(index, row);
            // port stays in node.ports and parentContainer unchanged
        }

        public int IndexOf(UIElement child)
        {
            return stackPanel.Children.IndexOf(child);
        }

        public void Remove(UIElement child)
        {
            stackPanel.Children.Remove(child);

            if (child is Port port)
            {
                node.ports.Remove(port);
            }

            // if no more chldren, hide container
            if (stackPanel.Children.Count == 0)
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }
}