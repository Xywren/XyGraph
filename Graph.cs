using System;
using System.CodeDom;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    // 2 Co-Ordinate systems:
    //  - "Graph" Coordinates: With the origin in the center of the graph. (this is how nodes are saved)
    //  - "Canvas" Coordinates: With the origin in the top-left of the canvas. (this is how WPF Canvas works)

    public class Graph : Canvas
    {
        public event Action GraphLoaded;
        public event Action? GraphChanged;

        public double worldSize = 10000.0;
        private enum GraphState { None, Panning, DraggingNode, CreatingEdge }

        public Point rightClickPos;
        public List<Edge> edges { get; internal set; } = new List<Edge>();
        public List<Node> nodes { get; internal set; } = new List<Node>();
        // graph-level input previews (also act as serialisable data containers)
        public List<GraphInputDefinition> inputDefinitions { get; internal set; } = new List<GraphInputDefinition>();
        public Dictionary<string, GraphInput> inputValues  = new Dictionary<string, GraphInput>();

        private const int GRID_SIZE = 20;

        public StartNode startNode { get; internal set; }
        public EndNode endNode { get; internal set; }
        internal MenuItem startItem { get; private set; }
        internal MenuItem endItem { get; private set; }
        public MenuItem createMenu { get; private set; }

        public Guid guid { get; set; }

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
            guid = System.Guid.NewGuid();

            ContextMenu = new ContextMenu();

            // Create a top-level Create menu and expose it so callers can add items
            createMenu = new MenuItem { Header = "Create" };

            startItem = new MenuItem { Header = "Start" };
            startItem.Click += (object sender, RoutedEventArgs e) => AddStartNode();
            createMenu.Items.Add(startItem);

            endItem = new MenuItem { Header = "End" };
            endItem.Click += (object sender, RoutedEventArgs e) => AddEndNode();
            createMenu.Items.Add(endItem);

            ContextMenu.Items.Add(createMenu);
            
        }


        public void AddStartNode()
        {
            if (startNode == null)
            {
                startNode = new StartNode(this);
                Canvas.SetLeft(startNode, rightClickPos.X - StartNode.OffsetX);
                Canvas.SetTop(startNode, rightClickPos.Y - StartNode.OffsetY);
                Children.Add(startNode);
                nodes.Add(startNode);
                startNode.NodeChanged -= OnNodeChanged;
                startNode.NodeChanged += OnNodeChanged;
                if (startItem != null) startItem.IsEnabled = false;
            }
        }

        public void AddEndNode()
        {
            if (endNode == null)
            {
                endNode = new EndNode(this);
                Canvas.SetLeft(endNode, rightClickPos.X - EndNode.OffsetX);
                Canvas.SetTop(endNode, rightClickPos.Y - EndNode.OffsetY);
                Children.Add(endNode);
                nodes.Add(endNode);
                endNode.NodeChanged -= OnNodeChanged;
                endNode.NodeChanged += OnNodeChanged;
                if (endItem != null) endItem.IsEnabled = false;
            }
        }
        public void AddNode(Node n, double posX = 0, double posY = 0)
        {

            Canvas.SetLeft(n, posX);
            Canvas.SetTop(n, posY);
            nodes.Add(n);
            Children.Add(n);
            // subscribe to node notifications so graph can bubble changes
            n.NodeChanged -= OnNodeChanged;
            n.NodeChanged += OnNodeChanged;
        }

        // bubbles node change events up as a graph-level notification
        private void OnNodeChanged()
        {
            GraphChanged?.Invoke();
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
            if (edges.Any(edge => (edge.outputPort == from && edge.inputPort == to) || (edge.outputPort == to && edge.inputPort == from)))
            {
                return null;
            }

            Edge conn = new Edge(this, from, to);
            conn.ReDraw();
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
            // unsubscribe node handlers to avoid leaks
            foreach (Node n in nodes.ToList())
            {
                n.NodeChanged -= OnNodeChanged;
            }

            while(nodes.Count >0)
                nodes[0].Delete();


            while (inputDefinitions.Count > 0)
                inputDefinitions[0].Delete();

            if (startItem != null)
                startItem.IsEnabled = true;
            if (endItem != null)
                endItem.IsEnabled = true;

            startNode = null;
            endNode = null;
        }

        // save graph into a JsonObject
        public JsonObject Save()
        {
            JsonObject obj = new JsonObject
            {
                ["schemaVersion"] = 1
            };

            // include graph GUID in the saved data
            obj["guid"] = guid.ToString();
            // persist runtime graph status and active node (if any)
            obj["status"] = status.ToString();
            obj["activeNode"] = activeNode != null ? activeNode.guid.ToString() : null;


            // the definitions of inputs, the "slots" that need ot be filled
            JsonArray inputsArray = new JsonArray();
            foreach (GraphInputDefinition gi in inputDefinitions)
            {
                inputsArray.Add(gi.Save());
            }
            obj["inputDefinitions"] = inputsArray;

            // the values of the inputs
            JsonObject inputValuesObj = new JsonObject();
            foreach (KeyValuePair<string, GraphInput> kvp in inputValues)
            {
                inputValuesObj[kvp.Key] = kvp.Value.Save();
            }
            obj["inputValues"] = inputValuesObj;


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
        public Graph Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            // restore graph GUID if present, otherwise generate a new one
            string guidStr = obj["guid"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(guidStr))
            {
                guid = System.Guid.Parse(guidStr);
            }
            else
            {
                guid = System.Guid.NewGuid();
            }


            // restore runtime status (if present)
            string statusStr = obj["status"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(statusStr))
            {
                if (System.Enum.TryParse<GraphStatus>(statusStr, out GraphStatus parsedStatus))
                {
                    status = parsedStatus;
                }
            }

            string activeGuidStr = obj["activeNode"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(activeGuidStr))
            {
                if (System.Guid.TryParse(activeGuidStr, out System.Guid activeGuid))
                {
                    Node found = nodes.FirstOrDefault(n => n.guid == activeGuid);
                    if (found != null)
                    {
                        activeNode = found;
                    }
                }
            }
            // clear existing graph
            Clear();


            // load graph-level inputs (if present)
            JsonArray inputsArray = obj["inputDefinitions"] as JsonArray;
            if (inputsArray != null)
            {
                foreach (JsonNode? item in inputsArray)
                {
                    JsonObject inputObj = item as JsonObject;
                    if (inputObj == null) throw new ArgumentException("Invalid input object in inputs array");
                    GraphInputDefinition gi = new GraphInputDefinition(this);
                    gi.Load(inputObj);
                    inputDefinitions.Add(gi);
                }
            }
            // restore runtime input values (if present)
            JsonObject inputValuesObj = obj["inputValues"] as JsonObject;
            if (inputValuesObj != null)
            {
                foreach (KeyValuePair<string, JsonNode> kv in inputValuesObj)
                {
                    try
                    {
                        JsonObject valObj = kv.Value as JsonObject;
                        if (valObj == null) continue;

                        GraphInput runtime = new GraphInput();
                        // find matching definition to determine expected type
                        GraphInputDefinition matched = inputDefinitions.FirstOrDefault(d => d.InputId.ToString() == kv.Key);
                        Type expected = null;
                        if (matched != null)
                        {
                            JsonObject defObj = matched.Save();
                            string typeName = defObj["type"]?.GetValue<string>() ?? string.Empty;
                            if (!string.IsNullOrEmpty(typeName))
                            {
                                try { expected = Type.GetType(typeName, false, true); } catch { expected = null; }
                            }
                        }

                        runtime.Load(valObj, expected);
                        inputValues[kv.Key] = runtime;
                    }
                    catch { }
                }
            }

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

            // notify listeners that the graph has finished loading
            GraphLoaded?.Invoke();

            return this;
        }


        public void ProvideInput(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Input name required", nameof(name));

            // try to find a matching definition by name (case-insensitive)
            GraphInputDefinition matchingDefinition = null;
            foreach (GraphInputDefinition def in inputDefinitions)
            {
                JsonObject defObj = def.Save();
                string defName = defObj["name"]?.GetValue<string>() ?? string.Empty;
                if (string.Equals(defName, name, StringComparison.OrdinalIgnoreCase))
                {
                    matchingDefinition = def;
                    break;
                }
            }
            if(matchingDefinition == null) throw new ArgumentException($"No matching input definition found for name '{name}'", nameof(name));


            GraphInput runtimeInput = new GraphInput();
            runtimeInput.name = matchingDefinition.Name;
            runtimeInput.ID = matchingDefinition.InputId;
            runtimeInput.Value = value;

            string key = runtimeInput.ID.ToString();
            runtimeInput.ID = matchingDefinition.InputId;

            inputValues[key] = runtimeInput;
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
                ConstructorInfo? ctor = matched.GetConstructor(new System.Type[] { typeof(Graph) });
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

            List<Type> result = new List<Type>();

            // Look through all loaded assemblies
            foreach (Assembly? assembly in AppDomain.CurrentDomain.GetAssemblies())
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

                foreach (Type? t in types)
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

        // Start running the graph from the start node (if present)
        public void Run()
        {
            if (startNode == null) return;
            InputValidation();
            // clear transient runtime state on ports so each run starts fresh
            ClearRuntimeCache();
            activeNode = startNode;
            status = GraphStatus.Running;
            startNode.Run();
        }

        private bool InputValidation()
        {
            foreach (GraphInputDefinition definition in inputDefinitions)
            {
                string key = definition.InputId.ToString();
                if (!inputValues.ContainsKey(key))
                {
                    JsonObject defObj = definition.Save();
                    string inputName = defObj["name"]?.GetValue<string>() ?? "Unnamed Input";
                    throw new InvalidOperationException($"Missing required input: '{inputName}'. All graph inputs must be provided before running.");
                }
            }
            return true;
        }

        private void ClearRuntimeCache()
        {
            foreach (Node node in nodes)
            {
                try { node.ClearRuntimeCache(); } catch { }
            }
        }
    }
}