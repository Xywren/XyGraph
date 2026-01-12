using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class Node : Border
    {
        public const double MIN_NODE_WIDTH = 150;
        public const double MIN_NODE_HEIGHT = 100;
        private const int CORNER_RADIUS = 10;
        private Grid grid;
        public List<Port> ports = new List<Port>();

        public NodeContainer titleContainer { get; private set; }
        public NodeContainer inputContainer { get; private set; }
        public NodeContainer outputContainer { get; private set; }
        public NodeContainer topContainer { get; private set; }
        public NodeContainer mainContainer { get; private set; }
        public NodeContainer bottomContainer { get; private set; }

        public Graph graph;

        public string title 
        { 
            get;
            set 
            { 
                if (titleTextBlock != null) titleTextBlock.Text = value; 
            } 
        } = "Title";

        private TextBlock titleTextBlock;

        public Node(Graph graph)
        {
            this.graph = graph;
            Background = Brushes.DarkGray;
            CornerRadius = new CornerRadius(CORNER_RADIUS);
            grid = CreateContent();
            Child = grid;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);

            // Add default title
            titleTextBlock = new TextBlock { Text = title, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            titleContainer.Add(titleTextBlock);
            titleContainer.Visibility = Visibility.Visible;
        }

        private Grid CreateContent()
        {
            Grid grid = new Grid();

            // Column definitions
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Row definitions
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Middle
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom

            // Title container
            titleContainer = new NodeContainer(this, Brushes.DarkSlateGray);
            Grid.SetRow(titleContainer, 0);
            Grid.SetColumn(titleContainer, 0);
            Grid.SetColumnSpan(titleContainer, 3);
            grid.Children.Add(titleContainer);
            titleContainer.CornerRadius = new CornerRadius(CORNER_RADIUS, CORNER_RADIUS, 0, 0);

            // Top container
            topContainer = new NodeContainer(this, Brushes.DimGray);
            Grid.SetRow(topContainer, 1);
            Grid.SetColumn(topContainer, 0);
            Grid.SetColumnSpan(topContainer, 3);
            grid.Children.Add(topContainer);

            // Middle row containers
            inputContainer = new NodeContainer(this, Brushes.Gray, Orientation.Vertical, HorizontalAlignment.Left);
            Grid.SetRow(inputContainer, 2);
            Grid.SetColumn(inputContainer, 0);
            grid.Children.Add(inputContainer);

            mainContainer = new NodeContainer(this, Brushes.DarkGray);
            Grid.SetRow(mainContainer, 2);
            Grid.SetColumn(mainContainer, 1);
            grid.Children.Add(mainContainer);

            outputContainer = new NodeContainer(this, Brushes.Gray, Orientation.Vertical, HorizontalAlignment.Right);
            Grid.SetRow(outputContainer, 2);
            Grid.SetColumn(outputContainer, 2);
            grid.Children.Add(outputContainer);

            // Bottom container
            bottomContainer = new NodeContainer(this, Brushes.DimGray);
            Grid.SetRow(bottomContainer, 3);
            Grid.SetColumn(bottomContainer, 0);
            Grid.SetColumnSpan(bottomContainer, 3);
            grid.Children.Add(bottomContainer);
            bottomContainer.CornerRadius = new CornerRadius(0, 0, CORNER_RADIUS, CORNER_RADIUS);

            return grid;
        }

        public void PortsChanged()
        {
            // if more than 1 output port, make all output port labels editable
            if (ports.Where(p => p.type == PortType.Output).Count() >= 2)
            {
                foreach (Port p in ports)
                {
                    if (p.type == PortType.Output)
                    {
                        p.isEditable = true;
                        p.isRemovable = true;
                    }
                }
            }

            // if only 1 output port, make all output port labels uneditable
            else if (ports.Where(p => p.type == PortType.Output).Count() == 1)
            {
                foreach (Port p in ports)
                {
                    if (p.type == PortType.Output)
                    {
                        p.isEditable = false;
                        p.isRemovable = false;
                    }
                }
            }
        }

        public void Delete()
        {
            List<Edge> edgesToRemove = graph.edges.Where(edge => this.ports.Contains(edge.fromPort) || this.ports.Contains(edge.toPort)).ToList();
            foreach (Edge edge in edgesToRemove)
            {
                edge.Delete();
            }
            graph.Children.Remove(this);
            graph.nodes.Remove(this);
        }
    }
}