using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class EndNode : Border
    {
        public Port Port { get; private set; }
        private Graph graph;

        public EndNode(Graph graph)
        {
            this.graph = graph;
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
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete End Node" };
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
            graph.endNode = null;
            graph.endItem.IsEnabled = true;
        }
    }
}