using System;
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
        private UIElement label;
        internal NodeContainer parentContainer;

        // Edit-time properties
        public List<Edge> edges = new List<Edge>();


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

            TextBlock textBlock = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
            label = textBlock;

            // create a small type label to display underneath the socket (e.g. "<Node>")
            string typeName = (portType != null) ? portType.Name : "object";
            TextBlock typeLabel = new TextBlock { Text = $"<{typeName}>", FontSize = 6, Foreground = Brushes.LightGray, Margin = new Thickness(0, -2, 0, 0) };
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
            StackPanel verticalStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };

            if (direction == PortDirection.Input)
            {
                connectionType = ConnectionType.Multi;
                // horizontal stack: [ socket | label ] so label sits directly to the right of the socket
                StackPanel horiz = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                horiz.Children.Add(socket);
                horiz.Children.Add(label);

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

            // apply initial colour if provided
            if (color != null)
            {
                socket.SetColor(color);
            }
        }



        // =======================================================================
        //                            Serialization
        // =======================================================================


        // Serialize this port to JSON for saving in a node/graph.
        public JsonObject Save()
        {
            var obj = new JsonObject
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

            return obj;
        }

        // Load a port from its saved JSON representation. The caller will add the returned Port into the appropriate container.
        public static Port Load(JsonObject obj, Node node)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            string name = obj["name"]?.GetValue<string>() ?? string.Empty;
            PortDirection pType = Enum.Parse<PortDirection>(obj["direction"]?.GetValue<string>() ?? PortDirection.Input.ToString());

            // attempt to resolve the port's CLR type
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
                var conv = new BrushConverter();
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

            return p;
        }




        // =======================================================================
        //                            Edit-Time functions
        // =======================================================================
        public void Delete()
        {
            foreach (Edge edge in edges.ToList())
            {
                edge.Delete();
            }
            if (parentContainer != null)
            {
                parentContainer.Remove(this);
            }
        }

        internal void ConnectionMade(Edge connection)
        {
            if (connection == null) return;

            // If this port only supports a single connection, remove existing edges first
            if (connectionType == ConnectionType.Single)
            {
                foreach (Edge e in edges.ToList())
                {
                    e.Delete();
                }
            }

            edges.Add(connection);
        }
    }
}