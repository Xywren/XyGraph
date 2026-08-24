using System;
using System.Reflection;
using System.CodeDom;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace XyGraph
{
    public enum PortDirection { Input, Output }
    public enum ConnectionType { Single, Multi}

    public class Socket : Border
    {
        // serialised elements
        public Port port;
        public int size;
        private bool hasOuterRing;

        public Socket(int size = 10, bool drawOuterRing = true)
        {
            this.size = size;
            this.hasOuterRing = drawOuterRing;

            // outer ring will be the Border (this). inner circle will be a child Border.
            if (drawOuterRing)
            {
                int outerSize = size + 8; // provide padding for the ring
                Width = outerSize;
                Height = outerSize;
                Background = Brushes.Transparent;
                CornerRadius = new CornerRadius(outerSize / 2.0);
                BorderThickness = new Thickness(2);
            }
            else
            {
                // no outer ring: size to inner circle and no border thickness
                Width = size;
                Height = size;
                Background = Brushes.Transparent;
                CornerRadius = new CornerRadius(size / 2.0);
                BorderThickness = new Thickness(0);
            }

            Border inner = new Border();
            inner.Width = size;
            inner.Height = size;
            inner.Background = Brushes.Black;
            inner.CornerRadius = new CornerRadius(size / 2.0);
            inner.HorizontalAlignment = HorizontalAlignment.Center;
            inner.VerticalAlignment = VerticalAlignment.Center;

            this.Child = inner;
        }

        public void SetColor(Brush b)
        {
            if (this.Child is Border inner)
            {
                inner.Background = b;
            }
            this.BorderBrush = b;
        }

        public Brush GetColor()
        {
            if (this.Child is Border inner)
            {
                return inner.Background as Brush ?? Brushes.Black;
            }
            return Brushes.Black;
        }
    }

    public class Port : Border
    {
        private const int DEFAULT_SOCKET_SIZE = 10;

        // serialised elements
        public Guid guid;
        public PortDirection direction; // is this an input or output port?
        public Socket socket;
        public ConnectionType connectionType = ConnectionType.Single; // does this port suport single or multiple edges?
        public string name;
        public Type portType;
        public Brush colour
        {
            get { return socket?.GetColor(); }
            set { if (socket != null) socket.SetColor(value); }
        }

        // non-serialised elements
        public UIElement label;
        public TextBlock typeLabel;
        public NodeContainer parentContainer;

        public MemberInfo ownerMember; // magic code that lets us set Inputs and Outputs on subclasses of Node
        public int ownerIndex = -1; // optional owner metadata for multi-output grouping
        public string ownerMemberName = null;

        // Edit-time properties
        public List<Edge> edges = new List<Edge>();

        // Inline literal: for a primitive input port (string/int/float/bool) the user can
        // type a value directly on the node. It is used at run time only when the port has
        // no incoming edge — a wire, variable or graph constant always takes precedence.
        public object literalValue;
        public bool hasLiteral;
        private FrameworkElement literalEditor;

        // Transient runtime state (cleared at start of a graph run)
        // Stores the computed value for this output port during a run.
        public object runtimeValue;
        // Whether runtimeValue contains a valid computed value.
        public bool hasRuntimeValue = false;
        // Used for cycle detection while evaluating this port/node.
        internal bool isEvaluating = false;


        public Port(string name, PortDirection direction, Type type, int socketSize = DEFAULT_SOCKET_SIZE, Brush color = null, bool drawSocketOuterRing = true)
        {
            guid = Guid.NewGuid();
            this.name = name;
            this.portType = type;
            this.direction = direction;

            socket = new Socket(socketSize, drawSocketOuterRing);
            socket.port = this;

            // build simple UI: socket and label. For input ports socket is left, for outputs socket is right.
            Background = Brushes.Transparent;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Align the grid to the side of the node depending on port direction so ports sit flush
            // against the left (inputs) or right (outputs) edges.
            if (direction == PortDirection.Input)
                grid.HorizontalAlignment = HorizontalAlignment.Left;
            else
                grid.HorizontalAlignment = HorizontalAlignment.Right;

            TextBlock textBlock = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
            label = textBlock;

            // create a small type label to display underneath the socket (e.g. "<Node>")
            string typeName = (portType != null) ? portType.Name : "object";
            typeLabel = new TextBlock { Text = $"<{typeName}>", FontSize = 6, Foreground = Brushes.LightGray, Margin = new Thickness(0, -2, 0, 0) };
            // align type label: left for input ports, right for output ports
            if (direction == PortDirection.Input)
            {
                typeLabel.HorizontalAlignment = HorizontalAlignment.Left;
                typeLabel.TextAlignment = TextAlignment.Left;
            }
            else
            {
                typeLabel.HorizontalAlignment = HorizontalAlignment.Right;
                typeLabel.TextAlignment = TextAlignment.Right;
            }

            // create vertical stack which contains a horizontal row (socket + label) and the small type label underneath
            // align the stack to the left for inputs and right for outputs so sockets align on the node edges
            StackPanel verticalStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = (direction == PortDirection.Input) ? HorizontalAlignment.Left : HorizontalAlignment.Right };

            if (direction == PortDirection.Input)
            {
                connectionType = ConnectionType.Multi;
                // horizontal stack: [ socket | label ] so label sits directly to the right of the socket
                StackPanel horiz = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                horiz.Children.Add(socket);
                horiz.Children.Add(label);

                // Primitive inputs get an inline editor so a literal can be typed on the node
                literalEditor = BuildLiteralEditor();
                if (literalEditor != null) horiz.Children.Add(literalEditor);

                verticalStack.Children.Add(horiz);
                verticalStack.Children.Add(typeLabel);

                Grid.SetColumn(verticalStack, 0);
                Grid.SetColumnSpan(verticalStack, 2);
                grid.Children.Add(verticalStack);
            }
            else
            {
                connectionType = ConnectionType.Single;
                // horizontal stack: [ label | socket ] so label sits directly to the left of the socket
                StackPanel horiz = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                horiz.Children.Add(label);
                horiz.Children.Add(socket);

                verticalStack.Children.Add(horiz);
                verticalStack.Children.Add(typeLabel);

                Grid.SetColumn(verticalStack, 1);
                Grid.SetColumnSpan(verticalStack, 2);
                grid.Children.Add(verticalStack);
            }

            Child = grid;

            ContextMenu = new ContextMenu();
            MenuItem disconnectItem = new MenuItem { Header = "Disconnect Port" };
            disconnectItem.Click += (object s, RoutedEventArgs e) => DisconnectAll();
            ContextMenu.Items.Add(disconnectItem);

            // A port sits on top of its node, so WPF shows this menu instead of the node's.
            // On nodes that are almost entirely port — input and variable Get nodes — that
            // left no way to reach the node's own actions, so they are offered here too and
            // follow the same selection scoping.
            ContextMenu.Items.Add(new Separator());

            MenuItem disconnectNodeItem = new MenuItem { Header = "Disconnect All" };
            disconnectNodeItem.Click += (object s, RoutedEventArgs e) => parentContainer?.node?.DisconnectTargets();
            ContextMenu.Items.Add(disconnectNodeItem);

            MenuItem deleteNodeItem = new MenuItem { Header = "Delete Node" };
            deleteNodeItem.Click += (object s, RoutedEventArgs e) => parentContainer?.node?.DeleteTargets();
            ContextMenu.Items.Add(deleteNodeItem);

            ContextMenu.Opened += (object s, RoutedEventArgs e) =>
            {
                int count = parentContainer?.node?.ActionTargets().Count ?? 1;
                bool many = count > 1;

                disconnectNodeItem.Header = many ? $"Disconnect All ({count} nodes)" : "Disconnect All";
                deleteNodeItem.Header     = many ? $"Delete Selected ({count})"      : "Delete Node";
            };

            // apply initial colour if provided
            if (color != null)
            {
                socket.SetColor(color);
            }
        }



        // =======================================================================
        //                            Inline literals
        // =======================================================================

        /// <summary>True for the primitive types that can be typed directly on a node.</summary>
        public static bool IsInlineEditable(Type t)
        {
            if (t == null) return false;
            return t == typeof(string) || t == typeof(bool)
                || t == typeof(int)   || t == typeof(long)  || t == typeof(short) || t == typeof(byte)
                || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
        }

        private bool IsNumeric(Type t) =>
            t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal);

        // Builds the on-node editor for a primitive input port: a checkbox for bool, a small
        // text field otherwise. Returns null for non-primitive ports (no editor).
        private FrameworkElement BuildLiteralEditor()
        {
            if (direction != PortDirection.Input || !IsInlineEditable(portType)) return null;

            if (portType == typeof(bool))
            {
                CheckBox box = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };
                box.Checked   += (s, e) => SetLiteralFromEditor(true);
                box.Unchecked += (s, e) => SetLiteralFromEditor(false);
                return box;
            }

            TextBox tb = new TextBox
            {
                Width             = IsNumeric(portType) ? 46 : 80,
                Height            = 18,
                FontSize          = 10,
                Padding           = new Thickness(2, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(4, 0, 4, 0)
            };
            tb.TextChanged += (s, e) => SetLiteralFromEditor(tb.Text);
            return tb;
        }

        // Parses the editor's raw value into the port's type and stores it as the literal.
        internal void SetLiteralFromEditor(object raw)
        {
            if (portType == typeof(bool))
            {
                literalValue = raw is bool b && b;
                hasLiteral   = true;
                return;
            }

            string text = raw as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) { hasLiteral = false; literalValue = null; return; }

            if (portType == typeof(string)) { literalValue = text; hasLiteral = true; return; }

            try { literalValue = Convert.ChangeType(text, portType); hasLiteral = true; }
            catch { hasLiteral = false; literalValue = null; }   // half-typed number — ignore until valid
        }

        // Reflects a loaded/!programmatic literal back into the editor UI.
        internal void PushLiteralToEditor()
        {
            if (literalEditor is CheckBox box) box.IsChecked = literalValue is bool b && b;
            else if (literalEditor is TextBox tb) tb.Text = literalValue?.ToString() ?? string.Empty;
        }

        // The editor is only meaningful when nothing is wired in; a connected port is driven
        // by its edge, so hide the field to make that obvious.
        public void UpdateLiteralEditorVisibility()
        {
            if (literalEditor == null) return;
            literalEditor.Visibility = edges.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // =======================================================================
        //                            Serialization
        // =======================================================================


        // Serialize this port to JSON for saving in a node/graph.
        public JsonObject Save()
        {
            JsonObject obj = new JsonObject
            {
                ["id"] = guid.ToString(),
                ["name"] = name ?? string.Empty,
                ["direction"] = direction.ToString(),
                ["portType"] = portType?.AssemblyQualifiedName ?? string.Empty,
                ["connectionType"] = connectionType.ToString(),
                ["socketSize"] = socket?.size ?? DEFAULT_SOCKET_SIZE
            };

            // save colour as a string if possible
            try
            {
                Brush b = socket?.GetColor() ?? Brushes.Black;
                var conv = new BrushConverter();
                string s = conv.ConvertToString(b) ?? "Black";
                obj["color"] = s;
            }
            catch
            {
                obj["color"] = "Black";
            }

            // persist optional owner metadata for multi-output grouping
            if (!string.IsNullOrEmpty(ownerMemberName) && ownerIndex >= 0)
            {
                obj["ownerMember"] = ownerMemberName;
                obj["ownerIndex"] = ownerIndex;
            }

            // persist an inline literal if one has been set
            if (hasLiteral && literalValue != null)
                obj["literal"] = literalValue.ToString();

            return obj;
        }

        // Load a port from its saved JSON representation. The caller will add the returned Port into the appropriate container.
        // Two ports may connect when the value produced by the output satisfies the
        // input's declared type. An input declared as object therefore accepts anything,
        // which is what lets generic nodes exist.
        public static bool CanConnect(Port from, Port to)
        {
            if (from == null || to == null) return false;
            if (from.direction == to.direction) return false;
            if (from.portType == null || to.portType == null) return false;

            Port output = from.direction == PortDirection.Output ? from : to;
            Port input = from.direction == PortDirection.Input ? from : to;

            // Normal case: the output's value already satisfies the input's type.
            if (input.portType.IsAssignableFrom(output.portType)) return true;

            // Cast connection: the input is a subtype of what the output produces
            // (e.g. object → Bill). The wire carries the cast; if the runtime value
            // isn't actually that type the assignment silently no-ops. Never applies to
            // execution (Node) wires — those are exact.
            if (typeof(Node).IsAssignableFrom(output.portType)) return false;
            return output.portType.IsAssignableFrom(input.portType);
        }

        public static Port Load(JsonObject obj, Node node)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            string name = obj["name"]?.GetValue<string>() ?? string.Empty;
            PortDirection pType = Enum.Parse<PortDirection>(obj["direction"]?.GetValue<string>() ?? PortDirection.Input.ToString());

            // attempt to resolve the port's system.Type
            string typeName = obj["portType"]?.GetValue<string>() ?? string.Empty;
            Type resolvedType = null;
            if (!string.IsNullOrEmpty(typeName))
            {
                resolvedType = Type.GetType(typeName);
            }

            int socketSize = obj["socketSize"]?.GetValue<int?>() ?? DEFAULT_SOCKET_SIZE;

            // parse color
            Brush colorBrush = Brushes.Black;
            string colorStr = obj["color"]?.GetValue<string>() ?? "Black";
            try
            {
                BrushConverter conv = new BrushConverter();
                colorBrush = (Brush)conv.ConvertFromString(colorStr);
            }
            catch
            {
                colorBrush = Brushes.Black;
            }

            // create port with name and resolved type (fall back to object if unresolved)
            Port p = new Port(name, pType, resolvedType ?? typeof(object), socketSize, colorBrush);

            // restore GUID and connection type
            p.guid = Guid.Parse(obj["id"]?.GetValue<string>() ?? p.guid.ToString());
            p.connectionType = Enum.Parse<ConnectionType>(obj["connectionType"]?.GetValue<string>() ?? p.connectionType.ToString());

            // restore optional owner metadata
            p.ownerMemberName = obj["ownerMember"]?.GetValue<string>();
            p.ownerIndex = obj["ownerIndex"]?.GetValue<int?>() ?? -1;

            // restore an inline literal
            string literalStr = obj["literal"]?.GetValue<string>();
            if (literalStr != null)
            {
                p.SetLiteralFromEditor(p.portType == typeof(bool)
                    ? (object)(literalStr.Equals("True", StringComparison.OrdinalIgnoreCase))
                    : literalStr);
                p.PushLiteralToEditor();
            }

            return p;
        }




        // =======================================================================
        //                            Edit-Time functions
        // =======================================================================
        public void DisconnectAll()
        {
            foreach (Edge edge in edges.ToList())
            {
                edge.Delete();
            }
        }

        public void Delete()
        {
            DisconnectAll();

            if (parentContainer != null)
            {
                parentContainer.Remove(this);
            }
        }

        // Fired whenever edges are added or removed on this port.
        public Action OnEdgesChanged;

        internal void ConnectionMade(Edge connection)
        {
            if (connection == null) return;

            if (connectionType == ConnectionType.Single)
            {
                foreach (Edge e in edges.ToList())
                    e.Delete();
            }

            edges.Add(connection);
            UpdateLiteralEditorVisibility();
            OnEdgesChanged?.Invoke();
        }

        internal void ConnectionRemoved(Edge connection)
        {
            edges.Remove(connection);
            UpdateLiteralEditorVisibility();
            OnEdgesChanged?.Invoke();
        }
    }
}