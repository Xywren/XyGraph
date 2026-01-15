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
    // 2 Co-Ordinate systems:
    //  - "Graph" Coordinates: With the origin in the center of the graph. (this is how nodes are saved)
    //  - "Canvas" Coordinates: With the origin in the top-left of the canvas. (this is how WPF Canvas works)

    public class Graph : Canvas
    {
        public double WorldSize { get; set; } = 10000.0;
        private enum GraphState { None, Panning, DraggingNode, CreatingEdge }

        public Point rightClickPos;
        public List<Edge> edges { get; internal set; } = new List<Edge>();
        public List<Node> nodes { get; internal set; } = new List<Node>();

        private const int GRID_SIZE = 20;

        public StartNode startNode { get; internal set; }
        public EndNode endNode { get; internal set; }
        internal MenuItem startItem { get; private set; }
        internal MenuItem endItem { get; private set; }

        public enum GraphStatus
        {
            Idle,
            Running,
            Completed,
            Error
        }
        public GraphStatus status { get; internal set; } = GraphStatus.Idle;
        public Node activeNode { get; internal set; }

        public Graph()
        {
            Background = Brushes.White;
            // Graph no longer owns mouse input handlers; GraphView manages mouse interactions (pan/zoom/drag/edge)

            ContextMenu = new ContextMenu();

            startItem = new MenuItem { Header = "Create Start Node" };
            startItem.Click += (object sender, RoutedEventArgs e) => AddStartNode();
            ContextMenu.Items.Add(startItem);

            endItem = new MenuItem { Header = "Create End Node" };
            endItem.Click += (object sender, RoutedEventArgs e) => AddEndNode();
            ContextMenu.Items.Add(endItem);
        }


        private void AddStartNode()
        {
            if (startNode == null)
            {
                startNode = new StartNode(this);
                Canvas.SetLeft(startNode, rightClickPos.X - StartNode.OffsetX);
                Canvas.SetTop(startNode, rightClickPos.Y - StartNode.OffsetY);
                Children.Add(startNode);
                nodes.Add(startNode);
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
                nodes.Add(endNode);
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


        internal UIElement GetNodeFromSource(object source)
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
        }

        // save graph into a JsonObject
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

            JsonArray nodesArray = obj["nodes"] as JsonArray;
            if (nodesArray != null)
            {
                foreach (JsonNode? item in nodesArray)
                {
                    JsonObject nodeObj = item as JsonObject;
                    if (nodeObj == null) continue;

                    string typeStr = nodeObj["type"]?.GetValue<string>() ?? "Node";

                    Node n = CreateNodeByType(typeStr);

                    n.Load(nodeObj);
                    AddNode(n, Canvas.GetLeft(n), Canvas.GetTop(n));
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

            // Set start and end nodes after loading
            startNode = nodes.FirstOrDefault(n => n is StartNode) as StartNode;
            if (startNode != null) startItem.IsEnabled = false;

            endNode = nodes.FirstOrDefault(n => n is EndNode) as EndNode;
            if (endNode != null) endItem.IsEnabled = false;
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

            // Guard against infinite or NaN sizes.
            if (double.IsInfinity(ActualWidth) || double.IsInfinity(ActualHeight) || double.IsNaN(ActualWidth) || double.IsNaN(ActualHeight))
            {
                return;
            }

            Pen pen = new Pen(Brushes.LightGray, 1);

            double maxX = Math.Max(0, ActualWidth);
            double maxY = Math.Max(0, ActualHeight);

            for (double x = 0; x < maxX; x += GRID_SIZE)
            {
                dc.DrawLine(pen, new Point(x, 0), new Point(x, maxY));
            }
            for (double y = 0; y < maxY; y += GRID_SIZE)
            {
                dc.DrawLine(pen, new Point(0, y), new Point(maxX, y));
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


        



        // =======================================================================
        //                            Runtime behaviour
        // =======================================================================

        public void Finished()
        {
            activeNode = null;
            status = GraphStatus.Completed;
        }
        public void OnError(Node node)
        {
            activeNode = null;
            status = GraphStatus.Error;
        }
    }
}