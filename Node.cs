using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using System.Text.Json.Nodes;

namespace XyGraph
{
    public class Node : Border
    {
        public Guid guid;

        public const double MIN_NODE_WIDTH = 150;
        public const double MIN_NODE_HEIGHT = 100;
        private const int CORNER_RADIUS = 10;
        private Grid grid;
        public List<Port> ports = new List<Port>();
        protected virtual string Type => GetType().Name;

        public NodeContainer titleContainer { get; private set; }
        public NodeContainer inputContainer { get; private set; }
        public NodeContainer outputContainer { get; private set; }
        public NodeContainer topContainer { get; private set; }
        public NodeContainer mainContainer { get; private set; }
        public NodeContainer bottomContainer { get; private set; }

        public Graph graph;


        public double SpawnOffsetX = 75;
        public double SpawnOffsetY = 50;

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
            guid = Guid.NewGuid();

            this.graph = graph;
            Background = Brushes.Magenta;
            CornerRadius = new CornerRadius(CORNER_RADIUS);

            Grid grid = new Grid();

            // Column definitions
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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

            Child = grid;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);

            titleTextBlock = new TextBlock { Text = title, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            titleContainer.Add(titleTextBlock);
            titleContainer.Visibility = Visibility.Visible;
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

        public List<Edge> GetAllEdges()
        {
            List<Edge> edges = new List<Edge>();
            foreach (Port port in ports)
            {
                foreach (Edge edge in port.edges)
                {
                    edges.Add(edge);
                }
            }
            return edges;

        }

        public void RedrawEdges()
        {
            foreach (Edge e in GetAllEdges())
            {
                e.UpdatePosition();
            }
        }


        public virtual JsonObject Save()
        {
            double x = Canvas.GetLeft(this);
            double y = Canvas.GetTop(this);
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;

            var obj = new JsonObject
            {
                ["type"] = Type,
                ["schemaVersion"] = 1,
                ["id"] = guid.ToString(),
                ["x"] = x,
                ["y"] = y
            };

            // loop through all ports that belong to this node and save them
            JsonArray portsArray = new JsonArray();
            foreach (Port port in ports)
            {
                portsArray.Add(port.Save());
            }
            obj["ports"] = portsArray;

            return obj;
        }

        // due to inheritence
        // (derived classes must override this funcion to load in their own custom properties)
        // we cannot use a static load Function like the other Load functions.
        // in this instance, you must create a dummy instance of the Node's type and then call Load() on it
        public virtual void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            guid = Guid.Parse(obj["id"]?.GetValue<string>() ?? guid.ToString());

            double x = obj["x"]?.GetValue<double>() ?? 0.0;
            Canvas.SetLeft(this, x);

            double y = obj["y"]?.GetValue<double>() ?? 0.0;
            Canvas.SetTop(this, y);

            // remove any existing ports to avoid duplicates when re-loading
            while (ports.Count > 0)
            {
                // Port.Delete will remove the visual from its parent container and remove it from this.ports
                ports[0].Delete();
            }

            // load ports that belong to this node
            JsonArray portsArray = obj["ports"] as JsonArray;
            if (portsArray != null)
            {
                foreach (JsonNode? item in portsArray)
                {
                    JsonObject portObj = item as JsonObject;
                    if (portObj == null) continue;

                    // create port via static loader
                    Port p = Port.Load(portObj, this);

                    if (p.type == PortType.Input)
                        inputContainer.Add(p);
                    else
                        outputContainer.Add(p);
                }
            }
        }
    }
}