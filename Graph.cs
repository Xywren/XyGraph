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

        // Must match GraphView's WORLD_SIZE. Nodes serialise relative to worldSize/2 while
        // groups serialise absolute, so a graph constructed without a GraphView has to agree
        // on this value or its groups load displaced from the nodes they contain.
        public double worldSize = 100000.0;
        private enum GraphState { None, Panning, DraggingNode, CreatingEdge }

        public Point rightClickPos;
        public List<Edge> edges { get; internal set; } = new List<Edge>();
        public List<Node> nodes { get; internal set; } = new List<Node>();
        public List<NodeGroup> groups { get; internal set; } = new List<NodeGroup>();

        // groups are drawn behind nodes so they read as a backdrop
        private const int GROUP_Z_INDEX = -1;
        // graph-level input previews (also act as serialisable data containers)
        public List<GraphInputDefinition> inputDefinitions { get; internal set; } = new List<GraphInputDefinition>();
        public Dictionary<string, GraphInput> inputValues  = new Dictionary<string, GraphInput>();

        // Graph-scoped variables: definitions are saved with the graph; values are runtime-only.
        public List<GraphVariable> variableDefinitions { get; internal set; } = new List<GraphVariable>();
        public Dictionary<Guid, object> variableValues = new Dictionary<Guid, object>();

        private const int GRID_SIZE = 20;

        public StartNode startNode { get; internal set; }
        // The first end node, kept for callers that only ever expect one.
        public EndNode endNode { get; internal set; }
        public List<EndNode> endNodes { get; } = new List<EndNode>();
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

        public List<Node> GetActiveNodes()
        {
            return nodes.Where(n => n.state == Node.NodeState.Running).ToList();
        }

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

            MenuItem branchItem = new MenuItem { Header = "Branch" };
            branchItem.Click += (object sender, RoutedEventArgs e) => AddNode(new BranchNode(this), rightClickPos.X, rightClickPos.Y);
            createMenu.Items.Add(branchItem);

            MenuItem commentItem = new MenuItem { Header = "Comment" };
            commentItem.Click += (object sender, RoutedEventArgs e) => AddCommentNode();
            createMenu.Items.Add(commentItem);

            MenuItem groupItem = new MenuItem { Header = "Group" };
            groupItem.Click += (object sender, RoutedEventArgs e) => AddNodeGroup();
            createMenu.Items.Add(groupItem);

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

        /// <summary>
        /// Adds an end node. A graph may have several — terminating a branch where it
        /// finishes keeps the wiring local instead of dragging every branch across the
        /// canvas to one shared terminator.
        /// </summary>
        public EndNode AddEndNode()
        {
            EndNode node = new EndNode(this);
            Canvas.SetLeft(node, rightClickPos.X - EndNode.OffsetX);
            Canvas.SetTop(node, rightClickPos.Y - EndNode.OffsetY);
            Children.Add(node);
            nodes.Add(node);
            node.NodeChanged -= OnNodeChanged;
            node.NodeChanged += OnNodeChanged;

            endNodes.Add(node);
            // endNode stays pointed at the first one so existing callers keep working
            if (endNode == null) endNode = node;

            return node;
        }
        public void AddCommentNode()
        {
            AddNode(new CommentNode(this), rightClickPos.X, rightClickPos.Y);
        }

        /// <summary>
        /// Creates a group around the current selection, or an empty group at the right-click
        /// position when nothing is selected.
        /// </summary>
        public void AddNodeGroup()
        {
            NodeGroup group = new NodeGroup(this);

            if (selectedNodes.Count > 0) group.EncloseNodes(selectedNodes);
            else                        group.PlaceAt(rightClickPos);

            groups.Add(group);
            Children.Add(group);
            SetZIndex(group, GROUP_Z_INDEX);
        }

        public void DeleteGroup(NodeGroup group)
        {
            if (group == null) return;
            groups.Remove(group);
            Children.Remove(group);
            GraphChanged?.Invoke();
        }

        /// <summary>
        /// Copies a group and the nodes inside it. Only edges with both ends inside the
        /// group are reproduced — connections leaving the group are dropped.
        /// </summary>
        public NodeGroup DuplicateGroup(NodeGroup source)
        {
            if (source == null) return null;

            const double PASTE_OFFSET = 40.0;
            Vector offset = new Vector(PASTE_OFFSET, PASTE_OFFSET);

            List<Node> originals = source.ContainedNodes();
            Dictionary<Guid, Port> portMap = new Dictionary<Guid, Port>();
            List<Node> copies = new List<Node>();

            foreach (Node original in originals)
            {
                JsonObject json = original.Save();
                Node copy = CreateNodeByType(json["type"]?.GetValue<string>() ?? "Node");
                copy.Load(json);

                // re-key the clone so it does not collide with the node it came from
                copy.guid = Guid.NewGuid();
                for (int i = 0; i < copy.ports.Count && i < original.ports.Count; i++)
                {
                    portMap[original.ports[i].guid] = copy.ports[i];
                    copy.ports[i].guid = Guid.NewGuid();
                }

                AddNode(copy, Canvas.GetLeft(original) + offset.X, Canvas.GetTop(original) + offset.Y);
                copies.Add(copy);
            }

            foreach (Edge e in edges.ToList())
            {
                if (!portMap.TryGetValue(e.outputPort.guid, out Port from)) continue;
                if (!portMap.TryGetValue(e.inputPort.guid,  out Port to))   continue;
                CreateEdge(from, to);
            }

            NodeGroup copyGroup = new NodeGroup(this) { name = source.name };
            copyGroup.Width  = source.Width;
            copyGroup.Height = source.Height;
            copyGroup.PlaceAt(new Point(source.Bounds.X + offset.X, source.Bounds.Y + offset.Y));

            groups.Add(copyGroup);
            Children.Add(copyGroup);
            SetZIndex(copyGroup, GROUP_Z_INDEX);

            ClearSelection();
            foreach (Node copy in copies) SelectNode(copy, additive: true);

            GraphChanged?.Invoke();
            return copyGroup;
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

        // ── Selection ─────────────────────────────────────────────────────────

        public List<Node> selectedNodes { get; internal set; } = new List<Node>();

        public void ClearSelection()
        {
            foreach (Node n in selectedNodes) n.isSelected = false;
            selectedNodes.Clear();
        }

        public void SelectNode(Node n, bool additive)
        {
            if (n == null) return;
            if (!additive) ClearSelection();
            if (selectedNodes.Contains(n)) return;
            selectedNodes.Add(n);
            n.isSelected = true;
        }

        public void DeselectNode(Node n)
        {
            if (n == null || !selectedNodes.Remove(n)) return;
            n.isSelected = false;
        }

        /// <summary>Deletes every selected node. Returns how many went.</summary>
        public int DeleteSelectedNodes()
        {
            // Copy first — Node.Delete() removes itself from the selection as it goes
            List<Node> doomed = selectedNodes.ToList();
            foreach (Node n in doomed) n.Delete();
            return doomed.Count;
        }

        /// <summary>
        /// Forces a layout pass, then redraws the given nodes' edges. Edge.ReDraw() reads
        /// socket positions through TranslatePoint, which reports where a socket was last
        /// laid out — and Canvas.SetLeft/SetTop only invalidate arrange. Without the pass
        /// the edges are drawn to the node's previous position, so a snap leaves them
        /// offset from their ports by the snap distance.
        /// </summary>
        public void RedrawEdgesAfterMove(IEnumerable<Node> moved)
        {
            if (moved == null) return;
            UpdateLayout();
            foreach (Node n in moved) n?.RedrawEdges();
        }

        public void ToggleNodeSelection(Node n)
        {
            if (n == null) return;
            if (selectedNodes.Contains(n)) DeselectNode(n);
            else SelectNode(n, additive: true);
        }

        /// <summary>Selects every node whose bounds intersect the given canvas-space rectangle.</summary>
        public void SelectNodesInRect(Rect area, bool additive)
        {
            if (!additive) ClearSelection();

            foreach (Node n in nodes)
            {
                double left = Canvas.GetLeft(n);
                double top  = Canvas.GetTop(n);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top))  top  = 0;

                Rect bounds = new Rect(left, top, n.ActualWidth, n.ActualHeight);
                if (area.IntersectsWith(bounds)) SelectNode(n, additive: true);
            }
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

            variableDefinitions.Clear();
            variableValues.Clear();
            selectedNodes.Clear();

            foreach (NodeGroup g in groups.ToList()) Children.Remove(g);
            groups.Clear();

            if (startItem != null)
                startItem.IsEnabled = true;

            startNode = null;
            endNode = null;
            endNodes.Clear();
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
            // persist runtime graph status
            obj["status"] = status.ToString();


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


            JsonArray variablesArray = new JsonArray();
            foreach (GraphVariable v in variableDefinitions)
                variablesArray.Add(v.Save());
            obj["variableDefinitions"] = variablesArray;

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

            JsonArray groupsArray = new JsonArray();
            foreach (NodeGroup g in groups)
                groupsArray.Add(g.Save());
            obj["groups"] = groupsArray;

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

            // load variable definitions
            JsonArray variablesArray2 = obj["variableDefinitions"] as JsonArray;
            if (variablesArray2 != null)
            {
                foreach (JsonNode item in variablesArray2)
                {
                    if (item is JsonObject vo)
                        variableDefinitions.Add(GraphVariable.Load(vo));
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

            if (obj["groups"] is JsonArray groupsArray)
            {
                foreach (JsonNode? item in groupsArray)
                {
                    if (item is not JsonObject groupObj) continue;

                    NodeGroup g = new NodeGroup(this);
                    g.Load(groupObj);
                    groups.Add(g);
                    Children.Add(g);
                    SetZIndex(g, GROUP_Z_INDEX);
                }
            }



            // Set start and end nodes after loading
            startNode = nodes.FirstOrDefault(n => n is StartNode) as StartNode;
            if (startNode != null) startItem.IsEnabled = false;

            endNodes.Clear();
            foreach (Node n in nodes)
                if (n is EndNode loaded) endNodes.Add(loaded);
            endNode = endNodes.FirstOrDefault();

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
            status = GraphStatus.Completed;
        }
        public void OnError(Node node)
        {
            status = GraphStatus.Error;
        }

        // Start running the graph from the start node (if present)
        public void Run()
        {
            if (startNode == null) return;
            InputValidation();
            ClearRuntimeCache();
            variableValues.Clear();   // fresh variable state each run
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