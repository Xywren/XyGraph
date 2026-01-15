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
        internal Border innerBorder;
        public List<Port> ports = new List<Port>();
        protected virtual string Type => GetType().Name;

        public NodeContainer titleContainer { get; private set; }
        public NodeContainer inputContainer { get; private set; }
        public NodeContainer outputContainer { get; private set; }
        public NodeContainer topContainer { get; private set; }
        public NodeContainer mainContainer { get; private set; }
        public NodeContainer bottomContainer { get; private set; }

        public Graph graph;
        public List<Node> inputs = new List<Node>();
        public List<Node> outputs = new List<Node>();


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

        // Outline properties: default blue outline, customizable via dependency properties
        public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.Register(
            nameof(OutlineBrush), typeof(Brush), typeof(Node), new PropertyMetadata(Brushes.Blue, OnOutlineBrushChanged));

        public Brush OutlineBrush
        {
            get => (Brush)GetValue(OutlineBrushProperty);
            set => SetValue(OutlineBrushProperty, value);
        }

        public static readonly DependencyProperty OutlineThicknessProperty = DependencyProperty.Register(
            nameof(OutlineThickness), typeof(double), typeof(Node), new PropertyMetadata(3.0, OnOutlineThicknessChanged));

        public double OutlineThickness
        {
            get => (double)GetValue(OutlineThicknessProperty);
            set => SetValue(OutlineThicknessProperty, value);
        }

        // Brushes per state
        public static readonly DependencyProperty OutlineRunningBrushProperty = DependencyProperty.Register(
            nameof(OutlineRunningBrush), typeof(Brush), typeof(Node), new PropertyMetadata(Brushes.Blue, OnOutlineBrushChanged));

        public Brush OutlineRunningBrush
        {
            get => (Brush)GetValue(OutlineRunningBrushProperty);
            set => SetValue(OutlineRunningBrushProperty, value);
        }

        public static readonly DependencyProperty OutlineCompletedBrushProperty = DependencyProperty.Register(
            nameof(OutlineCompletedBrush), typeof(Brush), typeof(Node), new PropertyMetadata(Brushes.Green, OnOutlineBrushChanged));

        public Brush OutlineCompletedBrush
        {
            get => (Brush)GetValue(OutlineCompletedBrushProperty);
            set => SetValue(OutlineCompletedBrushProperty, value);
        }

        public static readonly DependencyProperty OutlineErrorBrushProperty = DependencyProperty.Register(
            nameof(OutlineErrorBrush), typeof(Brush), typeof(Node), new PropertyMetadata(Brushes.Red, OnOutlineBrushChanged));

        public Brush OutlineErrorBrush
        {
            get => (Brush)GetValue(OutlineErrorBrushProperty);
            set => SetValue(OutlineErrorBrushProperty, value);
        }

        private static void OnOutlineBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Node n)
            {
                n.BorderBrush = e.NewValue as Brush;
            }
        }

        private static void OnOutlineThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Node n)
            {
                double v = 0.0;
                if (e.NewValue is double dv) v = dv;
                n.BorderThickness = new Thickness(v);
                if (n.innerBorder != null)
                {
                    n.innerBorder.CornerRadius = new CornerRadius(Math.Max(0, CORNER_RADIUS - (int)v));
                }
            }
        }

        public static readonly DependencyProperty OutlineGapProperty = DependencyProperty.Register(
            nameof(OutlineGap), typeof(double), typeof(Node), new PropertyMetadata(2.0, OnOutlineGapChanged));

        public double OutlineGap
        {
            get => (double)GetValue(OutlineGapProperty);
            set => SetValue(OutlineGapProperty, value);
        }

        private static void OnOutlineGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Node n)
            {
                double gap = 0.0;
                if (e.NewValue is double gd) gap = gd;
                // set outer padding to create a gap between node edge and inner content (outline separation)
                n.Padding = new Thickness(gap);
            }
        }

        private TextBlock titleTextBlock;

        public Node(Graph graph)
        {
            guid = Guid.NewGuid();

            this.graph = graph;
            Background = Brushes.Magenta;
            CornerRadius = new CornerRadius(CORNER_RADIUS);
            // initialize outline visuals from dependency properties
            BorderBrush = OutlineBrush;
            BorderThickness = new Thickness(OutlineThickness);

            // create inner grid and keep reference to it
            grid = new Grid();

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

            // create an inner border to render node background inset from the outer outline
            innerBorder = new Border();
            innerBorder.Background = this.Background;
            innerBorder.CornerRadius = new CornerRadius(Math.Max(0, CORNER_RADIUS - (int)OutlineThickness));
            innerBorder.Child = grid;

            // outer border (this) will act as the outline; make its background transparent so gap shows
            this.Background = Brushes.Transparent;
            this.Padding = new Thickness(OutlineGap);

            Child = innerBorder;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);

            titleTextBlock = new TextBlock { Text = title, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            titleContainer.Add(titleTextBlock);
            titleContainer.Visibility = Visibility.Visible;

            // set initial outline based on state
            UpdateOutlineForState();
        }

        private void UpdateOutlineForState()
        {
            switch (state)
            {
                case NodeState.Idle:
                    // hide outline
                    this.BorderBrush = Brushes.Transparent;
                    this.BorderThickness = new Thickness(0);
                    break;
                case NodeState.Running:
                    this.BorderBrush = OutlineRunningBrush ?? OutlineBrush;
                    this.BorderThickness = new Thickness(OutlineThickness);
                    break;
                case NodeState.Completed:
                    this.BorderBrush = OutlineCompletedBrush ?? OutlineBrush;
                    this.BorderThickness = new Thickness(OutlineThickness);
                    break;
                case NodeState.Error:
                    this.BorderBrush = OutlineErrorBrush ?? OutlineBrush;
                    this.BorderThickness = new Thickness(OutlineThickness);
                    break;
            }
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
            // convert to centered world coordinates (world origin at center of graph)
            double worldSize = graph?.WorldSize ?? 10000.0;
            double half = worldSize / 2.0;
            double centeredX = x - half;
            double centeredY = y - half;

            var obj = new JsonObject
            {
                ["type"] = Type,
                ["id"] = guid.ToString(),
                ["x"] = centeredX,
                ["y"] = centeredY
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

            // loaded coordinates are centered world coordinates; convert to canvas coords
            double centeredX = obj["x"]?.GetValue<double>() ?? 0.0;
            double centeredY = obj["y"]?.GetValue<double>() ?? 0.0;
            Point point = new Point(centeredX, centeredY);
            point =  ConvertWorldSpace(point);
            Canvas.SetLeft(this, point.X);
            Canvas.SetTop(this, point.Y);

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


        // convert graph coordinates (0,0 at the center of the graph) to canvas coordinates (0,0 wt the top left of the graph)
        internal Point ConvertWorldSpace(Point p)
        {
            double worldSize = graph?.WorldSize ?? 10000.0;
            return new Point(p.X + worldSize/2, p.Y + worldSize/2);
        }








        // =======================================================================
        //                            Runtime behaviour
        // =======================================================================

        public enum NodeState
        {
            Idle,
            Running,
            Completed,
            Error
        }
        private NodeState _state = NodeState.Idle;
        public NodeState state
        {
            get => _state;
            internal set
            {
                _state = value;
                UpdateOutlineForState();
            }
        }

        public virtual void Run()
        {
            state = NodeState.Running;
        }
        public virtual void Completed()
        {
            state = NodeState.Completed;
        }
        public virtual void Error()
        {
            state = NodeState.Error;
            graph.OnError(this);
        }

        public virtual void NextNode(int outputIndex = 0)
        {
            Node nextNode = outputs[outputIndex];
        }

        public List<string> GetOutputs()
        {
            List<string> outputNames = new List<string>();
            foreach (Port port in ports)
            {
                if (port.type == PortType.Output)
                {
                    outputNames.Add(port.name);
                }
            }
            return outputNames;
        }
    }
}