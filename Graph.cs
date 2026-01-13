using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace XyGraph
{
    public class Graph : Canvas
    {
        private enum GraphState { None, Panning, DraggingNode, CreatingEdge }

        private GraphState currentState = GraphState.None;
        private Point lastMousePos;
        public Point rightClickPos;
        private UIElement draggedNode;
        private Point dragStartPos;
        private Port edgeStartPort;
        private Line tempConnectionLine;
        public List<Edge> edges { get; internal set; } = new List<Edge>();
        public List<Node> nodes { get; internal set; } = new List<Node>();
        private Port targetPort;

        private const double ZOOM_FACTOR = 1.1;
        private const double ZOOM_REDUCE = 0.9;
        private const int GRID_SIZE = 20;
        private const double SNAP_DISTANCE = 25;

        public StartNode startNode { get; internal set; }
        public EndNode endNode { get; internal set; }
        internal MenuItem startItem { get; private set; }
        internal MenuItem endItem { get; private set; }

        public Graph()
        {
            Background = Brushes.White;
            MouseWheel += OnMouseWheel;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonDown += OnMouseRightButtonDown;

            ContextMenu = new ContextMenu();

            startItem = new MenuItem { Header = "Create Start Node" };
            startItem.Click += (object sender, RoutedEventArgs e) => AddStartNode();
            ContextMenu.Items.Add(startItem);

            endItem = new MenuItem { Header = "Create End Node" };
            endItem.Click += (object sender, RoutedEventArgs e) => AddEndNode();
            ContextMenu.Items.Add(endItem);
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            rightClickPos = e.GetPosition(this);
        }

        private void AddStartNode()
        {
            if (startNode == null)
            {
                startNode = new StartNode(this);
                Canvas.SetLeft(startNode, rightClickPos.X - StartNode.OffsetX);
                Canvas.SetTop(startNode, rightClickPos.Y - StartNode.OffsetY);
                Children.Add(startNode);
                startItem.IsEnabled = false;
            }
        }


        private void AddEndNode()
        {
            if (endNode == null)
            {
                endNode = new EndNode(this);
                Canvas.SetLeft(endNode, rightClickPos.X - EndNode.OffsetX);
                Canvas.SetTop(endNode, rightClickPos.Y - EndNode.OffsetY);
                Children.Add(endNode);
                endItem.IsEnabled = false;
            }
        }
        public void AddNode(Node n, double posX = 0, double posY = 0)
        {

            Canvas.SetLeft(n, posX);
            Canvas.SetTop(n, posY);
            nodes.Add(n);
            Children.Add(n);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double scale = e.Delta > 0 ? ZOOM_FACTOR : ZOOM_REDUCE;
            ScaleTransform matrix = LayoutTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            matrix.ScaleX *= scale;
            matrix.ScaleY *= scale;
            LayoutTransform = matrix;
        }

        private UIElement GetNodeFromSource(object source)
        {
            DependencyObject element = source as DependencyObject;
            while (element != null && element != this)
            {
                if (Children.Contains(element as UIElement))
                {
                    return element as UIElement;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }


        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(e.Source is Socket && currentState == GraphState.None)
            {
                Port port = (e.Source as Socket).port;
                edgeStartPort = port;
                currentState = GraphState.CreatingEdge;
                tempConnectionLine = new Line { Stroke = Brushes.Black, StrokeThickness = 2, IsHitTestVisible = false };
                Children.Add(tempConnectionLine);
                CaptureMouse();
            }
            else if (e.Source != this && currentState == GraphState.None)
            {
                UIElement node = GetNodeFromSource(e.Source);
                if (node != null)
                {
                    currentState = GraphState.DraggingNode;
                    draggedNode = node;
                    dragStartPos = e.GetPosition(this);
                    CaptureMouse();
                }
            }
            else if (e.Source == this && currentState == GraphState.None)
            {
                currentState = GraphState.Panning;
                lastMousePos = e.GetPosition(Window.GetWindow(this));
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (currentState == GraphState.CreatingEdge)
            {
                Point mousePos = e.GetPosition(this);
                int startOffset = edgeStartPort.socket.size / 2;
                Point startPos = edgeStartPort.socket.TranslatePoint(new Point(startOffset, startOffset), this);
                tempConnectionLine.X1 = startPos.X;
                tempConnectionLine.Y1 = startPos.Y;
                Point snapPos = mousePos;
                targetPort = null;
                double minDist = SNAP_DISTANCE;
                foreach (Node n in nodes)
                {
                    foreach (Port p in n.ports)
                    {
                        if (p != edgeStartPort && p.type != edgeStartPort.type)
                        {
                            int endOffset = p.socket.size / 2;
                            Point portPos = p.socket.TranslatePoint(new Point(endOffset, endOffset), this);
                            double dist = (mousePos - portPos).Length;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                snapPos = portPos;
                                targetPort = p;
                            }
                        }
                    }
                }
                if (startNode != null && startNode.port != edgeStartPort)
                {
                    Point portPos = startNode.port.socket.TranslatePoint(new Point(10, 10), this);
                    double dist = (mousePos - portPos).Length;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        snapPos = portPos;
                        targetPort = startNode.port;
                    }
                }
                if (endNode != null && endNode.port != edgeStartPort)
                {
                    Point portPos = endNode.port.socket.TranslatePoint(new Point(10, 10), this);
                    double dist = (mousePos - portPos).Length;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        snapPos = portPos;
                        targetPort = endNode.port;
                    }
                }
                tempConnectionLine.X2 = snapPos.X;
                tempConnectionLine.Y2 = snapPos.Y;
            }
            else if (currentState == GraphState.DraggingNode)
            {
                Point pos = e.GetPosition(this);
                Vector delta = pos - dragStartPos;
                double currentLeft = Canvas.GetLeft(draggedNode);
                double currentTop = Canvas.GetTop(draggedNode);
                Canvas.SetLeft(draggedNode, currentLeft + delta.X);
                Canvas.SetTop(draggedNode, currentTop + delta.Y);
                dragStartPos = pos;
                // Update edges
                if(draggedNode is Node node)
                    node.RedrawEdges();
                else if (draggedNode is StartNode start)
                {
                    foreach (Edge edge in start.port.edges)
                        edge.UpdatePosition();
                }
                else if (draggedNode is EndNode end)
                {
                    foreach (Edge edge in end.port.edges)
                        edge.UpdatePosition();
                }
            }
            else if (currentState == GraphState.Panning)
            {
                Point pos = e.GetPosition(Window.GetWindow(this));
                Vector delta = pos - lastMousePos;
                TranslateTransform transform = RenderTransform as TranslateTransform ?? new TranslateTransform();
                transform.X += delta.X;
                transform.Y += delta.Y;
                RenderTransform = transform;
                lastMousePos = pos;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (currentState == GraphState.CreatingEdge)
            {
                currentState = GraphState.None;
                Children.Remove(tempConnectionLine);
                tempConnectionLine = null;
                if (targetPort != null)
                {
                    CreateEdge(edgeStartPort, targetPort);
                }
                edgeStartPort = null;
                targetPort = null;
                ReleaseMouseCapture();
            }
            else if (currentState == GraphState.DraggingNode)
            {
                currentState = GraphState.None;
                draggedNode = null;
                ReleaseMouseCapture();
            }
            else if (currentState == GraphState.Panning)
            {
                currentState = GraphState.None;
                ReleaseMouseCapture();
            }
        }

        public Edge CreateEdge(Port from, Port to)
        {
            // Check if an edge already exists between these ports (bi-directional)
            if (edges.Any(edge => (edge.fromPort == from && edge.toPort == to) || (edge.fromPort == to && edge.toPort == from)))
            {
                return null;
            }

            Edge conn = new Edge(this, from, to);
            conn.UpdatePosition();
            Children.Add(conn.visual);
            edges.Add(conn);

            return conn;
        }

        // performs a search for a port by its GUID
        // this can be expensive so get a port by it's node when known
        public Port GetPortById(Guid id)
        {
            // start and end nodes dont actually count as nodes
            // so they wont get caught by the below for loop
            // check these first to speed up search on smaller graphs
            if (startNode != null && startNode.port.guid == id) return startNode.port;
            if (endNode != null && endNode.port.guid == id) return endNode.port;

            // loop through all nodes in graph
            foreach (Node node in nodes)
            {
                foreach (Port p in node.ports)
                {
                    if (p.guid == id) return p;
                }
            }

            return null;
        }

        // clears graph of nodes and edges
        public void Clear()
        {
            while(nodes.Count >0)
                nodes[0].Delete();

            // we shouldne need to delete edges here,
            // deleting the nodes should also delete any edges they were connected to

            // delete start/end nodes
            if (startNode != null)
            {
                startNode.Delete();
                startNode = null;
            }
            if (endNode != null)
            {
                endNode.Delete();
                endNode = null;
            }


            // we shouldnt need to clear the lists here, they should already be cleared by this point
            // i prefer to manage these elements properly instead doing of "just in case" code
        }

        // save graph into a JsonObject (nodes include their ports)
        public JsonObject Save()
        {
            JsonObject obj = new JsonObject
            {
                ["schemaVersion"] = 1
            };

            JsonArray nodesArray = new JsonArray();
            foreach (Node n in nodes)
            {
                nodesArray.Add(n.Save());
            }

            obj["nodes"] = nodesArray;

            JsonArray edgesArray = new JsonArray();
            foreach (Edge e in edges)
            {
                edgesArray.Add(e.Save());
            }
            obj["edges"] = edgesArray;

            return obj;
        }

        // load graph from JsonObject into this graph (non-static)
        public void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            // clear existing graph
            Clear();

            // load nodes first
            JsonArray nodesArray = obj["nodes"] as JsonArray;
            if (nodesArray != null)
            {
                foreach (JsonNode? item in nodesArray)
                {
                    JsonObject nodeObj = item as JsonObject;
                    if (nodeObj == null) continue;

                    string typeStr = nodeObj["type"]?.GetValue<string>() ?? "Node";

                    Node n = CreateNodeByType(typeStr);

                    // ensure node is part of this graph if ctor didn't add
                    if (!nodes.Contains(n))
                    {
                        nodes.Add(n);
                        Children.Add(n);
                    }

                    n.Load(nodeObj);
                    AddNode(n, Canvas.GetLeft(n), Canvas.GetBottom(n));
                }
            }

            // then load edges
            JsonArray edgesArray = obj["edges"] as JsonArray;
            if (edgesArray != null)
            {
                foreach (JsonNode? item in edgesArray)
                {
                    JsonObject edgeObj = item as JsonObject;
                    if (edgeObj == null) continue;

                    Edge.Load(edgeObj, this);
                }
            }
        }

        // will return an instance of the type, casted to a Node
        private Node CreateNodeByType(string typeName)
        {
            // Note: this will only find and use constructors that accept a single Graph parameter.
            if (string.IsNullOrEmpty(typeName))
                return new Node(this);

            // get all currently loaded derivatives of type Node
            List<Type> nodeDerivatives = GetDerivatives(typeof(Node));

            // find a type with the matching simple name (e.g. "ThreadNode")
            Type matched = nodeDerivatives.FirstOrDefault(t => t.Name == typeName);

            if (matched != null)
            {
                var ctor = matched.GetConstructor(new System.Type[] { typeof(Graph) });
                if (ctor != null)
                {
                    return (Node)ctor.Invoke(new object[] { this });
                }
            }

            // fallback to base Node
            return new Node(this);
        }

        // draws the grid background
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Pen pen = new Pen(Brushes.LightGray, 1);
            for (double x = 0; x < ActualWidth; x += GRID_SIZE)
            {
                dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
            }
            for (double y = 0; y < ActualHeight; y += GRID_SIZE)
            {
                dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
            }
        }

        public static List<Type> GetDerivatives(Type baseType)
        {
            if (baseType == null)
                throw new ArgumentNullException(nameof(baseType));

            var result = new List<Type>();

            // Look through all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in types)
                {
                    if (t == null)
                        continue;

                    // Skip the base type itself and abstract types if you only want concrete
                    if (t == baseType)
                        continue;

                    if (baseType.IsAssignableFrom(t))
                        result.Add(t);
                }
            }

            return result;
        }
    }
}