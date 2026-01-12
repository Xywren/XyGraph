using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class StartNode : Border
    {
        public Port Port { get; private set; }
        private Graph graph;

        public StartNode(Graph graph)
        {
            this.graph = graph;
            Port = new Port("", NodeType.Output, null, 20);
            Port.IsEditable = false;
            Port.IsRemovable = false;
            Port.Socket.Background = Brushes.Black;
            Child = Port;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Start Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);
        }

        public void Delete()
        {
            var edgesToRemove = graph.edges.Where(edge => Port == edge.FromPort || Port == edge.ToPort).ToList();
            foreach (var edge in edgesToRemove)
            {
                edge.Delete();
            }
            graph.Children.Remove(this);
            graph.startNode = null;
            graph.startItem.IsEnabled = true;
        }
    }
}