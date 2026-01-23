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
        public double OutlineThickness = 3.0;
        public Brush OutlineRunningBrush = Brushes.Blue;
        public Brush OutlineCompletedBrush = Brushes.Green;
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

            // create context menu to delete this node
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);

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


        protected void InitializePortsFromAttributes()
        {
            Type t = this.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            HandleNodePortAttributes(t, flags);
            HandleNodeMultiPortAttributes(t, flags);
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
                ConnectionType connType = inAttr != null ? inAttr.ConnectionType : outAttr.ConnectionType;
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
                string derivedColor = Common.HashColour(entry.memberType?.ToString() ?? "object");
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
                    string derivedColor = Common.HashColour(elementType?.ToString() ?? "object");
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
                            string derivedColorExisting = Common.HashColour(elementType?.ToString() ?? "object");
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

        // set colour of the node outline based on this nodes state
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


        public void Delete()
        {
            List<Edge> edgesToRemove = graph.edges.Where(edge => this.ports.Contains(edge.outputPort) || this.ports.Contains(edge.inputPort)).ToList();
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

            // remove any existing ports to avoid duplicates when re-loading
            while (ports.Count > 0)
                ports[0].Delete();




        #region Unholy Reflection Port Loading Ritual 
            // load ports that belong to this node
            JsonArray portsArray = obj["ports"] as JsonArray;
            if (portsArray != null)
            {
                // defer multi-ports that include owner metadata so we can resolve members and insert at the correct index
                List<Port> deferredMultiPorts = new List<Port>();

                // for each port (in JSON form) in this JSON array
                foreach (JsonNode? item in portsArray)
                {
                    JsonObject portObj = item as JsonObject;
                    if (portObj == null) continue;

                    // create port via static loader
                    Port p = Port.Load(portObj, this);

                    // If the saved port did not include explicit owner metadata (ownerMemberName)
                    if (string.IsNullOrEmpty(p.ownerMemberName))
                    {
                        BindingFlags resolveFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
                        MemberInfo resolved = null;

                        // Loop through all fields
                        foreach (FieldInfo field in this.GetType().GetFields(resolveFlags))
                        {
                            //if not at NodeInput or NodeOutput attribute, skip
                            NodeInputAttribute inAttr = field.GetCustomAttribute<NodeInputAttribute>();
                            NodeOutputAttribute outAttr = field.GetCustomAttribute<NodeOutputAttribute>();
                            if (inAttr == null && outAttr == null) continue;

                            // get the expected name of this port based on attribute or field name
                            string expectedName;
                            if (inAttr != null) expectedName = inAttr.Name != null ? inAttr.Name : field.Name;
                            else expectedName = outAttr.Name != null ? outAttr.Name : field.Name;

                            // assume all NodeInputs are Input ports, NodeOutputs are Output ports
                            PortDirection expectedDir = inAttr != null ? PortDirection.Input : PortDirection.Output;


                            // If Attribute direction and name match the loaded port, we've
                            // found the member that originally declared this port.
                            if (expectedDir == p.direction && string.Equals(expectedName ?? string.Empty, p.name ?? string.Empty, StringComparison.Ordinal))
                            {
                                // remember which field created this port for later binding
                                resolved = field;
                                break;
                            }
                        }

                        // if not found a field that created this port, search properties
                        if (resolved == null)
                        {
                            // Loop through all properties
                            foreach (PropertyInfo prop in this.GetType().GetProperties(resolveFlags))
                            {
                                //if not at NodeInput or NodeOutput attribute, skip
                                NodeInputAttribute inAttr = prop.GetCustomAttribute<NodeInputAttribute>();
                                NodeOutputAttribute outAttr = prop.GetCustomAttribute<NodeOutputAttribute>();
                                if (inAttr == null && outAttr == null) continue;

                                // get the expected name of this port based on attribute or field name
                                string expectedName;
                                if (inAttr != null) expectedName = inAttr.Name != null ? inAttr.Name : prop.Name;
                                else expectedName = outAttr.Name != null ? outAttr.Name : prop.Name;

                                // assume all NodeInputs are Input ports, NodeOutputs are Output ports
                                PortDirection expectedDir = inAttr != null ? PortDirection.Input : PortDirection.Output;

                                // If Attribute direction and name match the loaded port, we've
                                // found the member that originally declared this port.
                                if (expectedDir == p.direction && string.Equals(expectedName ?? string.Empty, p.name ?? string.Empty, StringComparison.Ordinal))
                                {
                                    // remember which field created this port for later binding
                                    resolved = prop;
                                    break;
                                }
                            }
                        }

                        // If we found which member created this port, set it as this ports Owner
                        if (resolved != null)
                        {
                            p.ownerMember = resolved;
                        }
                    }

                    // If the port contains owner metadata and an index, It is a [NodeMultiOutput] and not a [NodeInput] or [NodeOutput]
                    // defer it for later - these ports are more complex to recreate and use different logic
                    if (!string.IsNullOrEmpty(p.ownerMemberName) && p.ownerIndex >= 0 && p.direction == PortDirection.Output)
                    {
                        deferredMultiPorts.Add(p);
                    }
                    // if this is not a multi-output-port, add it now
                    else
                    {
                        // add the ports to the appropriate nodeContainer
                        if (p.direction == PortDirection.Input)
                            inputContainer.Add(p);
                        else
                            outputContainer.Add(p);
                    }
                }

                // Now process the more complicated MultiPorts that ew deferred for later

                Type nodeType = this.GetType();
                BindingFlags flagsLocal = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

                // for each deferred multi-port
                foreach (Port mp in deferredMultiPorts)
                {
                    // find the member (field or property) on this node that owns the list backing these ports
                    MemberInfo ownerMember = nodeType.GetField(mp.ownerMemberName, flagsLocal) as MemberInfo ?? nodeType.GetProperty(mp.ownerMemberName, flagsLocal) as MemberInfo;
                    if (ownerMember != null)
                    {
                        // tie the loaded metadata to the resolved member so later code can use it
                        mp.ownerMember = ownerMember;

                        if (ownerMember is FieldInfo fi && typeof(System.Collections.IList).IsAssignableFrom(fi.FieldType))
                        {
                            // attempts to parse the runtime object as a List<T> for the
                            // multi-output owner member (e.g. List<Node>)
                            object listObj = fi.GetValue(this);
                            System.Collections.IList list = listObj as System.Collections.IList;


                            // Ensure the backing list is instantiated, if not it will instantiate it for you
                            // TLDR: magically turns "[NodeMultiOutput] public List<int> myOutputs;" into "[NodeMultiOutput] public List<int> myOutputs = new List<int>();" if you didnt initialize it yourself
                            if (list == null)
                            {
                                Type elemType = fi.FieldType.IsGenericType ? fi.FieldType.GetGenericArguments()[0] : typeof(object);
                                Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(new Type[] { elemType });
                                list = (System.Collections.IList)Activator.CreateInstance(listType);
                                fi.SetValue(this, list);
                            }

                            // if this Port's index is 5, ensure the list has at least 6 slots so index [5] is valid
                            while (list.Count <= mp.ownerIndex) list.Add(null);

                            // Determine type T in List<T>
                            Type elemTypeForPort = fi.FieldType.IsGenericType ? fi.FieldType.GetGenericArguments()[0] : typeof(object);

                            // load [NodeMultiOutput] attribute metadata
                            NodeMultiOutputAttribute multiAttr = fi.GetCustomAttribute<NodeMultiOutputAttribute>();
                            int socketSize = multiAttr?.SocketSize ?? 10;
                            // derive a color for multi-ports if not specified on the attribute
                            string derived = Common.HashColour(elemTypeForPort?.ToString() ?? "object");
                            BrushConverter localBrushConverter = new BrushConverter();
                            Brush colorBrush = (Brush)localBrushConverter.ConvertFromString(multiAttr?.Color ?? derived);
                            bool drawOuter = multiAttr?.DrawOuterRing ?? true;

                            // create a MultiPort
                            MultiPort newMp = new MultiPort(mp.name, PortDirection.Output, elemTypeForPort, socketSize: socketSize, color: colorBrush, drawSocketOuterRing: drawOuter);

                            // carry over important serialized state (ids, connection mode, owner metadata)
                            newMp.guid = mp.guid;
                            newMp.connectionType = mp.connectionType;
                            newMp.ownerIndex = mp.ownerIndex;
                            newMp.ownerMemberName = mp.ownerMemberName;
                            newMp.ownerMember = ownerMember;

                            // try to preserve the saved colour if present
                            try { newMp.colour = mp.colour; } catch { }

                            // find the add-button for this [NodeMultiOutput]
                            int addBtnIndex = -1;
                            if (outputContainer.Child is StackPanel sp)
                            {
                                for (int i = 0; i < sp.Children.Count; i++)
                                {
                                    if (sp.Children[i] is Button b && (b.Tag as string) == mp.ownerMemberName)
                                    {
                                        addBtnIndex = i;
                                        break;
                                    }
                                }
                            }

                            // Add these MultiPorts underneath their "Add Output" button.
                            if (addBtnIndex >= 0)
                            {
                                int insertAt = addBtnIndex + 1 + Math.Max(0, mp.ownerIndex);
                                outputContainer.InsertAt(insertAt, newMp);
                            }
                            // if we cant find the button, just add them to the end of the container
                            else
                            {
                                outputContainer.Add(newMp);
                            }
                        }

                        // same code for properties
                        else if (ownerMember is PropertyInfo pi && typeof(System.Collections.IList).IsAssignableFrom(pi.PropertyType))
                        {
                            // attempts to parse the runtime object as a List<T> for the
                            // multi-output owner member (e.g. List<Node>)
                            object listObj = pi.GetValue(this);
                            System.Collections.IList list = listObj as System.Collections.IList;


                            // Ensure the backing list is instantiated, if not it will instantiate it for you
                            // TLDR: magically turns "[NodeMultiOutput] public List<int> myOutputs;" into "[NodeMultiOutput] public List<int> myOutputs = new List<int>();" if you didnt initialize it yourself
                            if (list == null)
                            {
                                Type elemType = pi.PropertyType.IsGenericType ? pi.PropertyType.GetGenericArguments()[0] : typeof(object);
                                Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(new Type[] { elemType });
                                list = (System.Collections.IList)Activator.CreateInstance(listType);
                                pi.SetValue(this, list);
                            }

                            // if this Port's index is 5, ensure the list has at least 6 slots so index [5] is valid
                            while (list.Count <= mp.ownerIndex) list.Add(null);

                            // Determine type T in List<T>
                            Type elemTypeForPort = pi.PropertyType.IsGenericType ? pi.PropertyType.GetGenericArguments()[0] : typeof(object);

                            // load [NodeMultiOutput] attribute metadata
                            NodeMultiOutputAttribute multiAttr = pi.GetCustomAttribute<NodeMultiOutputAttribute>();
                            int socketSize = multiAttr?.SocketSize ?? 10;
                            // derive a color for multi-ports if not specified on the attribute (match field-backed behavior)
                            string derivedProp = Common.HashColour(elemTypeForPort?.ToString() ?? "object");
                            string finalPropColor = multiAttr?.Color ?? derivedProp;
                            Brush colorBrush;
                            try { colorBrush = (Brush)(new BrushConverter().ConvertFromString(finalPropColor)); } catch { colorBrush = Brushes.Black; }
                            bool drawOuter = multiAttr?.DrawOuterRing ?? true;

                            // create a MultiPort
                            MultiPort newMp = new MultiPort(mp.name, PortDirection.Output, elemTypeForPort, socketSize: socketSize, color: colorBrush, drawSocketOuterRing: drawOuter);

                            // carry over important serialized state (ids, connection mode, owner metadata)
                            newMp.guid = mp.guid;
                            newMp.connectionType = mp.connectionType;
                            newMp.ownerIndex = mp.ownerIndex;
                            newMp.ownerMemberName = mp.ownerMemberName;
                            newMp.ownerMember = ownerMember;
                            try { newMp.colour = mp.colour; } catch { }

                            int addBtnIndex = -1;
                            if (outputContainer.Child is StackPanel sp2)
                            {
                                for (int i = 0; i < sp2.Children.Count; i++)
                                {
                                    if (sp2.Children[i] is Button b && (b.Tag as string) == mp.ownerMemberName)
                                    {
                                        addBtnIndex = i;
                                        break;
                                    }
                                }
                            }

                            // Add these MultiPorts underneath their "Add Output" button.
                            if (addBtnIndex >= 0)
                            {
                                int insertAt = addBtnIndex + 1 + Math.Max(0, mp.ownerIndex);
                                outputContainer.InsertAt(insertAt, newMp);
                            }
                            // if we cant find the button, just add them to the end of the container
                            else
                            {
                                outputContainer.Add(newMp);
                            }
                        }
                        else
                        {
                            // couldn't find a list-backed member for this saved multi-port; fail loudly
                            throw new InvalidOperationException($"Saved multi-port refers to member '{mp.ownerMemberName}' but that member is not a List on type '{this.GetType().FullName}'.");
                        }
                    }
                    else
                    {
                        // owner member name didn't resolve; fail loudly so caller can detect bad/legacy save data
                        throw new InvalidOperationException($"Could not resolve owner member '{mp.ownerMemberName}' for port '{mp.name}' on node type '{this.GetType().FullName}'.");
                    }
                }
            }
        #endregion

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

        public List<String> GetOutputStrings()
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
                if (incoming != null && incoming.outputPort != null)
                {
                    // Get the PortValue from the upstream node, assign it to this member
                    object val = this.ResolvePortValue(incoming.outputPort);
                    try
                    {
                        if (member is FieldInfo field) field.SetValue(this, val);
                        else if (member is PropertyInfo prop) prop.SetValue(this, val);
                    }
                    catch { }
                }
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
                    targetPort.runtimeValue = null;
                    targetPort.hasRuntimeValue = true;
                    return null;
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
        public virtual void Error()
        {
            state = NodeState.Error;
            graph.OnError(this);
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