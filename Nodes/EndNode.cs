using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class EndNode : Border
    {
        public static readonly double OffsetX = 14;
        public static readonly double OffsetY = 14;

        public Port port { get; private set; }
        private Graph graph;

        public EndNode(Graph graph)
        {
            this.graph = graph;
            Port port = new Port("", PortType.Input, null, 20);
            port.isEditable = false;
            port.isRemovable = false;
            Border outline = new Border
            {
                Width = 28,
                Height = 28,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12)
            };

            port.HorizontalAlignment = HorizontalAlignment.Center;

            Grid g = port.Child as Grid;
            g.Children.Add(outline);

            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = "END", HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(port);
            Child = sp;

            this.port = port;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete End Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);
        }

        public void Delete()
        {
            var edgesToRemove = graph.edges.Where(edge => port == edge.fromPort || port == edge.toPort).ToList();
            foreach (var edge in edgesToRemove)
            {
                edge.Delete();
            }
            graph.Children.Remove(this);
            graph.endNode = null;
            graph.endItem.IsEnabled = true;
        }
    }
}