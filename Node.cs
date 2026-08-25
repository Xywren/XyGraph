using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class Node : Border
    {
        public event Action? NodeChanged;
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

        // Node status outline properties
        public Brush OutlineBrush = Brushes.Blue;
        public Brush SelectionBrush = Brushes.DodgerBlue;
        public double OutlineThickness = 3.0;
        // Active/waiting node → vivid green (same treatment as selection, different colour).
        public Brush OutlineRunningBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
        // Completed → muted slate so it reads as "done", leaving green to mean "active now".
        public Brush OutlineCompletedBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x6B, 0x7B));
        public Brush OutlineErrorBrush = Brushes.Red;
        public double OutlineGap = 2.0; // gap between outer border and inner content

        private TextBlock titleTextBlock;

        public Node(Graph graph)
        {
            guid = Guid.NewGuid();

            // main node border (you should never see this so bright pink shoudl stand out)
            this.graph = graph;
            CornerRadius = new CornerRadius(CORNER_RADIUS);

            // create grid structure of nodes
            grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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

            // the main panel of the node (you should never see this, so bright pink should stand out)
            innerBorder = new Border();
            innerBorder.Background = Brushes.Magenta;
            innerBorder.CornerRadius = new CornerRadius(Math.Max(0, CORNER_RADIUS - (int)OutlineThickness));
            innerBorder.Child = grid;

            // outer border (this) will act as the outline; make its background transparent so gap shows
            this.Background = Brushes.Transparent;
            this.Padding = new Thickness(OutlineGap);
            Child = innerBorder;

            // Every node offers the same two actions, in the same order as the port menu
            ContextMenu = new ContextMenu();

            // Grouping only makes sense for a multi-selection, and only when this node is
            // part of it — otherwise the menu would group nodes the user never clicked.
            MenuItem groupItem = new MenuItem { Header = "Group Selected" };
            groupItem.Click += (s, e) => graph.AddNodeGroup();
            ContextMenu.Items.Add(groupItem);

            Separator groupSeparator = new Separator();
            ContextMenu.Items.Add(groupSeparator);

            MenuItem disconnectAllItem = new MenuItem { Header = "Disconnect All" };
            disconnectAllItem.Click += (s, e) => this.DisconnectTargets();
            ContextMenu.Items.Add(disconnectAllItem);

            ContextMenu.Items.Add(new Separator());

            MenuItem deleteItem = new MenuItem { Header = "Delete Node" };
            deleteItem.Click += (s, e) => this.DeleteTargets();
            ContextMenu.Items.Add(deleteItem);

            // Retarget the labels each time the menu opens so it always states its scope
            ContextMenu.Opened += (s, e) =>
            {
                int count = ActionTargets().Count;
                bool many = count > 1;

                groupItem.Header          = $"Group Selected ({count})";
                groupItem.Visibility      = many ? Visibility.Visible : Visibility.Collapsed;
                groupSeparator.Visibility = groupItem.Visibility;

                disconnectAllItem.Header = many ? $"Disconnect All ({count} nodes)" : "Disconnect All";
                deleteItem.Header        = many ? $"Delete Selected ({count})"      : "Delete Node";
            };

            // add a textblock to show this node's title
            titleTextBlock = new TextBlock { Text = title, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            titleContainer.Add(titleTextBlock);
            titleContainer.Visibility = Visibility.Visible;

            UpdateOutlineForState();

            // nodes should have no outline by default
            this.BorderBrush = Brushes.Transparent;
            this.BorderThickness = new Thickness(OutlineThickness);
            this.Padding = new Thickness(OutlineGap);


            // automatically creates Input and Output ports based on sub-class atributes
            InitializePortsFromAttributes();
        }


        // Control-flow ports carry Node references. They are always black so the
        // execution path reads distinctly from typed data connections.
        internal static string DerivePortColour(Type portType)
        {
            if (portType != null && typeof(Node).IsAssignableFrom(portType))
                return "Black";

            return Common.HashColour(portType?.ToString() ?? "object");
        }

        protected void InitializePortsFromAttributes()
        {
            Type t = this.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            HandleNodeMultiPortAttributes(t, flags);
            HandleNodePortAttributes(t, flags);
        }

        //Handles the creation of ports for this node based on NodeInput and NodeOutput attributes
        private void HandleNodePortAttributes(Type t, BindingFlags flags)
        {
            // create a list of port metedata (this is all data required to construct ports)
            List<(MemberInfo member, Type memberType, string portName, PortDirection dir, string color, ConnectionType connType, int socketSize, bool drawOuterRing)> items =
                new List<(MemberInfo, Type, string, PortDirection, string, ConnectionType, int, bool)>();

            //loop through all members in this class type
            foreach (MemberInfo member in t.GetMembers(flags))
            {
                // we only care about fields and properties
                FieldInfo? asField = member as FieldInfo;
                PropertyInfo? asProp = member as PropertyInfo;
                if (asField == null && asProp == null) continue;

                //if this field has neither a NodeInput nor NodeOutput attribute, skip it
                NodeInputAttribute inAttr = member.GetCustomAttribute<NodeInputAttribute>();
                NodeOutputAttribute outAttr = member.GetCustomAttribute<NodeOutputAttribute>();
                if (inAttr == null && outAttr == null) continue;

                // Get the a name of this port:
                //  - if this attribute has specified a Name, use that
                //  - otherwise, use the name of the field itself
                string portName;
                if (inAttr != null) portName = inAttr.Name != null ? inAttr.Name : member.Name;
                else if (outAttr != null) portName = outAttr.Name != null ? outAttr.Name : member.Name;
                else portName = member.Name;

                // Record the System.Type type of this field
                Type memberType = (asField != null) ? (asField.FieldType ?? typeof(object)) : (asProp != null ? (asProp.PropertyType ?? typeof(object)) : typeof(object));

                // Get port Data from the Attribute, if none provided, use default values
                PortDirection dir = inAttr != null ? PortDirection.Input : PortDirection.Output;
                // do not default to Black here; if attribute is absent we will derive a color from the member type
                string colorName = inAttr?.Color ?? outAttr?.Color;
                // Determine connection type. If the attribute explicitly set ConnectionType, use it.
                // Otherwise, default outputs that are of type Node (or derived) to Single, else Multi.
                ConnectionType connType;
                if (inAttr != null)
                {
                    connType = inAttr.ConnectionType;
                }
                else
                {
                    // outAttr.ConnectionType is nullable to indicate unspecified. If specified, use it.
                    if (outAttr.ConnectionType.HasValue)
                    {
                        connType = outAttr.ConnectionType.Value;
                    }
                    else
                    {
                        // if member type is Node or derived, default to Single, else Multi
                        if (typeof(Node).IsAssignableFrom(memberType)) connType = ConnectionType.Single;
                        else connType = ConnectionType.Multi;
                    }
                }
                int socketSize = inAttr != null ? inAttr.SocketSize : outAttr.SocketSize;
                bool drawOuterRing = inAttr != null ? inAttr.DrawOuterRing : outAttr.DrawOuterRing;

                // add this ports metadata to the list
                items.Add((member, memberType, portName, dir, colorName, connType, socketSize, drawOuterRing));
            }

            // used to convert string hex codes ("#FF00FF") into Brushes
            BrushConverter brushConverter = new BrushConverter();

            // Loop over all the metadata in the list and create the Port UI elements and add them to the node
            foreach (var entry in items)
            {
                // if the attribute did not provide a color, derive one from the member type
                string derivedColor = DerivePortColour(entry.memberType);
                string finalColor = entry.color ?? derivedColor;
                Brush colorBrush = (Brush)brushConverter.ConvertFromString(finalColor);

                // Create the Port
                Port p = new Port(entry.portName, entry.dir, entry.memberType, socketSize: entry.socketSize, color: colorBrush, drawSocketOuterRing: entry.drawOuterRing);
                p.connectionType = entry.connType;
                p.ownerMember = entry.member;

                // add the ports to the appropriate nodeContainer
                if (entry.dir == PortDirection.Input)
                    inputContainer.Add(p);
                else
                    outputContainer.Add(p);
            }
        }

        public void ClearRuntimeCache()
        {
            foreach (Port p in ports)
            {
                p.runtimeValue = null;
                p.hasRuntimeValue = false;
                p.isEvaluating = false;
            }
        }

        private void HandleNodeMultiPortAttributes(Type t, BindingFlags flags)
        {
            // used to convert string hex codes ("#FF00FF") into Brushes
            BrushConverter brushConverter = new BrushConverter();

            //loop through all members in this class type
            foreach (MemberInfo member in t.GetMembers(flags))
            {
                //if this field has not got a NodeMultiOutput, skip it
                NodeMultiOutputAttribute multiAttr = member.GetCustomAttribute<NodeMultiOutputAttribute>();
                if (multiAttr == null) continue;

                // Create a single "Add Output" button for this multi-output member.
                Button addBtn = new Button { Content = "Add Output", FontSize = 8, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
                addBtn.Tag = member.Name; // mark the button so we can find it later

                // Expose unified accessors for the member so we don't duplicate field/property logic
                FieldInfo? fieldInfo = member as FieldInfo;
                PropertyInfo? propInfo = member as PropertyInfo;
                Type memberType = (fieldInfo != null) ? (fieldInfo.FieldType ?? typeof(object)) : (propInfo != null ? (propInfo.PropertyType ?? typeof(object)) : typeof(object));
                System.Func<object> getter = () => (fieldInfo != null) ? fieldInfo.GetValue(this) : (propInfo != null ? propInfo.GetValue(this) : null);
                System.Action<object> setter = (object v) => { if (fieldInfo != null) fieldInfo.SetValue(this, v); else if (propInfo != null) propInfo.SetValue(this, v); };

                // When the user clicks the button, create a new list slot (null) in the backing List<T>, then create a MultiPort UI
                addBtn.Click += (s, e) =>
                {
                    // figure out the element type of the list (T in List<T>)
                    Type elementType = typeof(object);
                    if (memberType.IsGenericType)
                    {
                        Type[] args = memberType.GetGenericArguments();
                        if (args != null && args.Length > 0) elementType = args[0];
                    }

                    // Ensure the backing list is instantiated, if not it will instantiate it for you
                    // TLDR: magically turns "[NodeMultiOutput] public List<int> myOutputs;" into "[NodeMultiOutput] public List<int> myOutputs = new List<int>();" if you didnt initialize it yourself
                    IList listRef = null;
                    try
                    {
                        object existing = getter();
                        if (existing is IList l) listRef = l;
                        else
                        {
                            Type listType = typeof(List<>).MakeGenericType(new Type[] { elementType });
                            listRef = (IList)Activator.CreateInstance(listType)!;
                            setter(listRef);
                        }
                    }
                    catch { }

                    // add a new empty slot to the output list
                    int newIndex = -1;
                    if (listRef != null)
                    {
                        listRef.Add(null);
                        newIndex = listRef.Count - 1;
                    }

                    // create the MultiPort UI from metadata
                    string derivedColor = DerivePortColour(elementType);
                    string finalColor = multiAttr.Color ?? derivedColor;
                    Brush colorBrush = (Brush)brushConverter.ConvertFromString(finalColor);
                    MultiPort mp = new MultiPort("New Output", PortDirection.Output, elementType, socketSize: multiAttr.SocketSize, color: colorBrush, drawSocketOuterRing: multiAttr.DrawOuterRing);

                    // keep track of which node, and which field in that node owns this list, and which element in this list this port belongs to
                    mp.ownerMember = member;
                    mp.ownerMemberName = member.Name;
                    mp.ownerIndex = newIndex;

                    // insert the new port visually immediately after the add button so it appears under the button
                    int btnIndex = outputContainer.IndexOf(addBtn);
                    int insertAt = btnIndex + 1 + Math.Max(0, newIndex);
                    outputContainer.InsertAt(insertAt, mp);
                };

                // Add the Button to the node
                outputContainer.Add(addBtn);

                // If the list already has elements (e.g., from initialization or load), create ports for each
                try
                {
                    // double check that this field/property is a List, if so loop through it
                    object existing = getter();
                    if (existing is System.Collections.IList existingList)
                    {
                        for (int i = 0; i < existingList.Count; i++)
                        {
                            // Get the element type T from a List<T> member (default to object if the member isn't generic)
                            Type elementType = typeof(object);
                            if (memberType.IsGenericType)
                            {
                                Type[] args = memberType.GetGenericArguments();
                                if (args != null && args.Length > 0) elementType = args[0];
                            }

                            // create a MultiPort for each existing list slot and insert after the add button
                            string derivedColorExisting = DerivePortColour(elementType);
                            string finalColorExisting = multiAttr.Color ?? derivedColorExisting;
                            Brush existingColorBrush = (Brush)brushConverter.ConvertFromString(finalColorExisting);
                            MultiPort mp = new MultiPort("New Output", PortDirection.Output, elementType, socketSize: multiAttr.SocketSize, color: existingColorBrush, drawSocketOuterRing: multiAttr.DrawOuterRing);
                            
                            // keep track of which node, and which member in that node owns this list, and which element in this list this port belongs to
                            mp.ownerMember = member;
                            mp.ownerMemberName = member.Name;
                            mp.ownerIndex = i;

                            // insert the new port visually immediately after the add button so it appears under the button
                            int btnIndex = outputContainer.IndexOf(addBtn);
                            int insertAt = btnIndex + 1 + i;
                            outputContainer.InsertAt(insertAt, mp);
                        }
                    }
                }
                catch { }
            }
        }

        private bool _isSelected;
        public bool isSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                UpdateOutlineForState();
            }
        }

        // set colour of the node outline based on this nodes state
        private void UpdateOutlineForState()
        {
            // selection outline takes precedence over the run-state outline
            if (isSelected)
            {
                this.BorderBrush = SelectionBrush;
                this.BorderThickness = new Thickness(OutlineThickness);
                return;
            }

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


        /// <summary>Drops every edge attached to any of this node's ports, leaving the node.</summary>
        public void DisconnectAll()
        {
            foreach (Port port in ports.ToList())
                port.DisconnectAll();
        }

        /// <summary>
        /// The nodes a context-menu action applies to: the whole selection when this node is
        /// part of a multi-selection, otherwise just this node. Returns a copy, since the
        /// callers mutate the selection while iterating.
        /// </summary>
        public List<Node> ActionTargets()
        {
            if (graph != null && graph.selectedNodes.Count > 1 && graph.selectedNodes.Contains(this))
                return graph.selectedNodes.ToList();

            return new List<Node> { this };
        }

        public void DisconnectTargets()
        {
            foreach (Node target in ActionTargets()) target.DisconnectAll();
        }

        public void DeleteTargets()
        {
            foreach (Node target in ActionTargets()) target.Delete();
        }

        // virtual so Start/End nodes get their bookkeeping run even when deleted through a
        // Node-typed reference, such as the context menu handlers
        public virtual void Delete()
        {
            List<Edge> edgesToRemove = graph.edges.Where(edge => this.ports.Contains(edge.outputPort) || this.ports.Contains(edge.inputPort)).ToList();
            foreach (Edge edge in edgesToRemove)
            {
                edge.Delete();
            }
            graph.Children.Remove(this);
            graph.nodes.Remove(this);
            graph.selectedNodes.Remove(this);   // else the selection keeps a dead reference
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


        public void OnNodeMoved()
        {
            NodeChanged?.Invoke();
        }

        protected void NotifyChanged()
        {
            NodeChanged?.Invoke();
        }

        public void RedrawEdges()
        {
            foreach (Edge e in GetAllEdges())
            {
                e.ReDraw();
            }
        }


        public virtual JsonObject Save()
        {
            double x = Canvas.GetLeft(this);
            double y = Canvas.GetTop(this);
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;
            // convert to centered world coordinates (world origin at center of graph)
            double worldSize = graph?.worldSize ?? 10000.0;
            double half = worldSize / 2.0;
            double centeredX = x - half;
            double centeredY = y - half;

            JsonObject obj = new JsonObject
            {
                ["type"] = Type,
                ["id"] = guid.ToString(),
                ["x"] = centeredX,
                ["y"] = centeredY,
                ["state"] = state.ToString()
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

        /// <summary>
        /// Restores a dynamically created port's saved identity. Load matches ports by name
        /// against the ones the constructor made from attributes, so a port built at runtime
        /// never matches and keeps its fresh guid — which silently drops every edge to it.
        /// Subclasses that build their own port must call this once they have rebuilt it.
        /// </summary>
        protected void RestorePortGuid(JsonObject obj, Port port, PortDirection direction)
        {
            if (obj == null || port == null) return;
            if (obj["ports"] is not JsonArray savedPorts) return;

            foreach (JsonNode? item in savedPorts)
            {
                if (item is not JsonObject portObj) continue;
                if (portObj["direction"]?.GetValue<string>() != direction.ToString()) continue;

                string savedId = portObj["id"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(savedId)) port.guid = Guid.Parse(savedId);
                return;
            }
        }

        public virtual void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            guid = Guid.Parse(obj["id"]?.GetValue<string>() ?? guid.ToString());

            // convert saved position to canvas coords and postition node
            double centeredX = obj["x"]?.GetValue<double>() ?? 0.0;
            double centeredY = obj["y"]?.GetValue<double>() ?? 0.0;
            Point point = new Point(centeredX, centeredY);
            point = ConvertWorldSpace(point);
            Canvas.SetLeft(this, point.X);
            Canvas.SetTop(this, point.Y);

            // Restore saved port state onto the constructor-created ports.
            // The constructor already created ports from the current [NodeInput]/[NodeOutput]
            // attributes, so the class definition is always the source of truth for what ports
            // exist. We just patch GUIDs and literals from saved data so existing edges still
            // connect. Saved ports that no longer match a class attribute are silently dropped;
            // edges pointing at them will be skipped by Edge.Load (line 551).
            JsonArray portsArray = obj["ports"] as JsonArray;
            if (portsArray != null)
            {
                List<Port> deferredMultiPorts = new List<Port>();

                foreach (JsonNode? item in portsArray)
                {
                    JsonObject portObj = item as JsonObject;
                    if (portObj == null) continue;

                    string savedName = portObj["name"]?.GetValue<string>() ?? string.Empty;
                    PortDirection savedDir = Enum.Parse<PortDirection>(portObj["direction"]?.GetValue<string>() ?? "Input");

                    // Multi-output ports ([NodeMultiOutput]) are dynamic and still need
                    // the deferred creation logic — parse them into a temporary Port.
                    string ownerMember = portObj["ownerMember"]?.GetValue<string>();
                    int ownerIdx = portObj["ownerIndex"]?.GetValue<int?>() ?? -1;
                    if (!string.IsNullOrEmpty(ownerMember) && ownerIdx >= 0 && savedDir == PortDirection.Output)
                    {
                        Port temp = Port.Load(portObj, this);
                        deferredMultiPorts.Add(temp);
                        continue;
                    }

                    // Find the matching constructor-created port by name + direction.
                    Port match = ports.FirstOrDefault(p =>
                        p.direction == savedDir &&
                        string.Equals(p.name, savedName, StringComparison.Ordinal));

                    if (match == null) continue;

                    // Patch GUID so existing edges connect to this port.
                    string savedId = portObj["id"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(savedId))
                        match.guid = Guid.Parse(savedId);

                    // Restore any saved literal value.
                    string literalStr = portObj["literal"]?.GetValue<string>();
                    if (literalStr != null)
                    {
                        match.SetLiteralFromEditor(match.portType == typeof(bool)
                            ? (object)(literalStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                            : literalStr);
                        match.PushLiteralToEditor();
                    }
                }

            #region Multi-port restoration
                Type nodeType = this.GetType();
                BindingFlags flagsLocal = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

                foreach (Port mp in deferredMultiPorts)
                {
                    MemberInfo ownerMemberInfo = nodeType.GetField(mp.ownerMemberName, flagsLocal) as MemberInfo
                                              ?? nodeType.GetProperty(mp.ownerMemberName, flagsLocal) as MemberInfo;
                    if (ownerMemberInfo == null) continue;

                    mp.ownerMember = ownerMemberInfo;

                    Type listOwnerType = null;
                    System.Collections.IList list = null;

                    if (ownerMemberInfo is FieldInfo fi && typeof(System.Collections.IList).IsAssignableFrom(fi.FieldType))
                    {
                        listOwnerType = fi.FieldType;
                        list = fi.GetValue(this) as System.Collections.IList;
                        if (list == null)
                        {
                            Type elemType = fi.FieldType.IsGenericType ? fi.FieldType.GetGenericArguments()[0] : typeof(object);
                            list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType));
                            fi.SetValue(this, list);
                        }
                    }
                    else if (ownerMemberInfo is PropertyInfo pi && typeof(System.Collections.IList).IsAssignableFrom(pi.PropertyType))
                    {
                        listOwnerType = pi.PropertyType;
                        list = pi.GetValue(this) as System.Collections.IList;
                        if (list == null)
                        {
                            Type elemType = pi.PropertyType.IsGenericType ? pi.PropertyType.GetGenericArguments()[0] : typeof(object);
                            list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType));
                            pi.SetValue(this, list);
                        }
                    }
                    else continue;

                    while (list.Count <= mp.ownerIndex) list.Add(null);

                    Type elemTypeForPort = listOwnerType.IsGenericType ? listOwnerType.GetGenericArguments()[0] : typeof(object);
                    NodeMultiOutputAttribute multiAttr = ownerMemberInfo is FieldInfo f2
                        ? f2.GetCustomAttribute<NodeMultiOutputAttribute>()
                        : ((PropertyInfo)ownerMemberInfo).GetCustomAttribute<NodeMultiOutputAttribute>();

                    int socketSize = multiAttr?.SocketSize ?? 10;
                    string derivedColor = DerivePortColour(elemTypeForPort);
                    Brush colorBrush;
                    try { colorBrush = (Brush)(new BrushConverter().ConvertFromString(multiAttr?.Color ?? derivedColor)); }
                    catch { colorBrush = Brushes.Black; }
                    bool drawOuter = multiAttr?.DrawOuterRing ?? true;

                    MultiPort newMp = new MultiPort(mp.name, PortDirection.Output, elemTypeForPort,
                        socketSize: socketSize, color: colorBrush, drawSocketOuterRing: drawOuter);
                    newMp.guid = mp.guid;
                    newMp.connectionType = mp.connectionType;
                    newMp.ownerIndex = mp.ownerIndex;
                    newMp.ownerMemberName = mp.ownerMemberName;
                    newMp.ownerMember = ownerMemberInfo;
                    try { newMp.colour = mp.colour; } catch { }

                    int addBtnIndex = -1;
                    if (outputContainer.Child is StackPanel sp)
                    {
                        for (int i = 0; i < sp.Children.Count; i++)
                        {
                            if (sp.Children[i] is Button b && (b.Tag as string) == mp.ownerMemberName)
                            { addBtnIndex = i; break; }
                        }
                    }

                    if (addBtnIndex >= 0)
                        outputContainer.InsertAt(addBtnIndex + 1 + Math.Max(0, mp.ownerIndex), newMp);
                    else
                        outputContainer.Add(newMp);
                }
            #endregion
            }

            // restore runtime state if present
            string stateStr = obj["state"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(stateStr))
            {
                if (System.Enum.TryParse<NodeState>(stateStr, out NodeState parsedState))
                {
                    // assign to property so UpdateOutlineForState() runs
                    this.state = parsedState;
                }
            }
        }


        // convert graph coordinates (0,0 at the center of the graph) to canvas coordinates (0,0 wt the top left of the graph)
        internal Point ConvertWorldSpace(Point p)
        {
            double worldSize = graph?.worldSize ?? 10000.0;
            return new Point(p.X + worldSize/2, p.Y + worldSize/2);
        }

        public List<String> GetOutputStrings(Type filterType)
        {
            List<string> outputNames = new List<string>();
            foreach (Port port in ports)
            {
                if (port.direction == PortDirection.Output)
                {
                    // if no filter provided, include all. Otherwise include only ports whose portType is assignable to the filter
                    if (filterType == null || (port.portType != null && filterType.IsAssignableFrom(port.portType)))
                    {
                        outputNames.Add(port.name);
                    }
                }
            }
            return outputNames;
        }








        // =======================================================================
        //                            Runtime behaviour
        // =======================================================================


        // Loop through all this Node's [NodeInput]s and get's their values
        public void PopulateInputs()
        {
            if (graph == null) return;

            // Get Member Data
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            List<MemberInfo> members = new List<MemberInfo>();
            Type nodeType = this.GetType();
            foreach (FieldInfo f in nodeType.GetFields(flags)) members.Add(f);
            foreach (PropertyInfo p in nodeType.GetProperties(flags)) members.Add(p);

            // loop through all members of this node
            foreach (MemberInfo member in members)
            {
                // if this member doesnt have the [NodeInput] attribute, skip it
                NodeInputAttribute inAttr = member.GetCustomAttribute<NodeInputAttribute>();
                if (inAttr == null) continue;

                // find input port for this member
                Port inputPort = this.ports.FirstOrDefault(p => p.ownerMember != null && p.ownerMember == member && p.direction == PortDirection.Input);
                if (inputPort == null) continue;

                // look for an incoming edge on this port
                Edge incoming = inputPort.edges.FirstOrDefault(e => e.inputPort == inputPort && e.outputPort != null);

                // a wire wins; otherwise fall back to an inline literal typed on the node
                object val;
                if (incoming != null && incoming.outputPort != null)
                    val = this.ResolvePortValue(incoming.outputPort);
                else if (inputPort.hasLiteral)
                    val = inputPort.literalValue;
                else
                    continue;

                // A record arrives as a reference to a row, so read the row rather than trust
                // whatever the value looked like when it was produced — which for a process
                // that has been waiting may be days out of date.
                val = RecordReference.Reload(val);

                try
                {
                    if (member is FieldInfo field) field.SetValue(this, val);
                    else if (member is PropertyInfo prop) prop.SetValue(this, val);
                }
                catch { }
            }

            // Ports created at runtime (multi-ports, dynamically added pairs) have no owning
            // member, so the loop above cannot reach them. Resolve them onto the port itself
            // so nodes that add their own ports can read their values.
            foreach (Port port in ports)
            {
                if (port.direction != PortDirection.Input) continue;
                if (port.ownerMember != null) continue;
                if (port.hasRuntimeValue) continue;

                port.runtimeValue = RecordReference.Reload(ResolvePortValue(port));
                port.hasRuntimeValue = true;
            }
        }

        // Resolve the value produced by an output Port. Lazily evaluates upstream data nodes.
        internal object ResolvePortValue(Port targetPort)
        {
            // if port is null, skip it
            if (targetPort == null) return null;

            // if port already has a runtime value cached, return it
            if (targetPort.hasRuntimeValue) return targetPort.runtimeValue;

            //if port does not already have a cached value, we need to evaluate upstream node

            // if the target port is somehow an input port (this should never happen)
            // get the output port on the other end of the edge
            Port fromPort = null;
            if (targetPort.direction == PortDirection.Input)
            {
                Edge incoming = targetPort.edges.FirstOrDefault(e => e.inputPort == targetPort && e.outputPort != null);
                if (incoming == null)
                {
                    // no wire — fall back to an inline literal typed on the node, if any
                    targetPort.runtimeValue = targetPort.hasLiteral ? targetPort.literalValue : null;
                    targetPort.hasRuntimeValue = true;
                    return targetPort.runtimeValue;
                }
                fromPort = incoming.outputPort;
            }
            else
                fromPort = targetPort;

            // these should never really happen, but just in case
            if (fromPort == null) return null;
            if (fromPort.hasRuntimeValue) return fromPort.runtimeValue;
            if (fromPort.isEvaluating) throw new InvalidOperationException("Cycle detected during evaluation.");

            // if we reached this point, the port does not have a cached value and we need to evaluate its parent node

            // get the parent Node of this port
            Node parentNode = fromPort.parentContainer?.node;
            if (parentNode == null) return null;

            try
            {
                fromPort.isEvaluating = true;

                // just incase the parent node has uncached inputs also (chained data nodes)
                // populate the inputs of the parent also
                parentNode.PopulateInputs();

                // evaluate the parent node
                parentNode.Evaluate();

                // cache these outputs into the ports
                parentNode.PublishOutputs();

                object result = fromPort.runtimeValue;
                fromPort.hasRuntimeValue = true;
                return result;
            }
            finally
            {
                fromPort.isEvaluating = false;
            }
        }

        // After Evaluate, write outputs into port values
        public void PublishOutputs()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            // Mget Member Data
            List<MemberInfo> members = new List<MemberInfo>();
            Type nodeType = this.GetType();
            foreach (FieldInfo f in nodeType.GetFields(flags)) members.Add(f);
            foreach (PropertyInfo p in nodeType.GetProperties(flags)) members.Add(p);

            // loop through all members of this node
            foreach (MemberInfo member in members)
            {
                // if this member doesnt have the [NodeOutput] attribute, skip it
                NodeOutputAttribute outAttr = member.GetCustomAttribute<NodeOutputAttribute>();
                if (outAttr == null) continue;

                // find output port for this member
                Port outPort = this.ports.FirstOrDefault(p => p.ownerMember != null && p.ownerMember == member && p.direction == PortDirection.Output);
                if (outPort == null) continue;

                // get the value of this member
                object val = null;
                try
                {
                    if (member is FieldInfo field) val = field.GetValue(this);
                    else if (member is PropertyInfo prop) val = prop.GetValue(this);
                }
                catch { val = null; }

                // set the ports value to this members value
                outPort.runtimeValue = val;
                outPort.hasRuntimeValue = true;
            }
        }



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
                // notify listeners that the node changed (no payload)
                NodeChanged?.Invoke();
            }
        }

        
        public virtual void Evaluate() { } // Nodes that never actually Run() but need to compute a value should override this and do input>output processing here 

        public virtual void Run()
        {
            // ensure inputs are populated for this node before running
            PopulateInputs();
            state = NodeState.Running;
        }
        public virtual void Completed()
        {
            state = NodeState.Completed;
        }
        protected void SetIdle()
        {
            state = NodeState.Idle;
        }
        public virtual void Error()
        {
            state = NodeState.Error;
            graph.OnError(this);
        }

        public virtual void Update()
        {
            // write your custom logic here that decides if this node is completed or not.
        }


        public List<string> GetOutputs()
        {
            List<string> outputNames = new List<string>();
            foreach (Port port in ports)
            {
                if (port.direction == PortDirection.Output)
                {
                    outputNames.Add(port.name);
                }
            }
            return outputNames;
        }
    }
}