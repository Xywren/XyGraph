using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class StartNode : Border
    {
        public static readonly double OffsetX = 10;
        public static readonly double OffsetY = 10;

        public Port port { get; private set; }
        private Graph graph;

        public StartNode(Graph graph)
        {
            this.graph = graph;
            Port port = new Port("", PortType.Output, null, 20);
            port.isEditable = false;
            port.isRemovable = false;
            port.socket.Background = Brushes.Black;

            StackPanel sp = new StackPanel();
            TextBlock tb = new TextBlock { Text = "START", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            sp.Children.Add(tb);
            sp.Children.Add(port);
            Child = sp;

            this.port = port;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Start Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);
        }

        public void Delete()
        {
            List<Edge> edgesToRemove = graph.edges.Where(edge => port == edge.fromPort || port == edge.toPort).ToList();
            foreach (Edge edge in edgesToRemove)
            {
                edge.Delete();
            }
            graph.Children.Remove(this);
            graph.startNode = null;
            graph.startItem.IsEnabled = true;
        }
    }
}