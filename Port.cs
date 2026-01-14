using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json.Nodes;

namespace XyGraph
{
    public enum PortType { Input, Output }
    public enum ConnectionType { Single, Multi}

    public class Socket : Border
    {
        public Port port;
        public int size;

        public Socket(int size = 10)
        {
            this.size = size;
            Width = size;
            Height = size;
            Background = Brushes.Black;
            CornerRadius = new CornerRadius(size);
        }
    }

    public class Port : Border
    {
        public Guid guid;

        private const int DEFAULT_SOCKET_SIZE = 10;
        private const int BUTTON_WIDTH = 20;
        private const int BUTTON_HEIGHT = 20;

        public PortType type; // is this an input or output port?
        public ConnectionType connectionType = ConnectionType.Single; // does this port suport single or multiple edges?
        public string name;
        public Socket socket;
        private UIElement label;
        internal NodeContainer parentContainer;
        private Button button;

        public List<Edge> edges = new List<Edge>();

        public bool isEditable
        {
            get
            {
                // if label is a textbox then port is editable
                // if label is a textblock then port is not editable
                if (Child is Grid g)
                {
                    foreach (UIElement child in g.Children)
                    {
                        if (Grid.GetColumn(child) == 1)
                            return child is TextBox;
                    }
                }
                return false;
            }
            set
            {
                // Find the child occupying column 1 (the label column) and its index in the Grid.
                // If the current control type (TextBox vs TextBlock) doesn't match the requested state,
                // replace that element in-place so layout/order is preserved.
                if (Child is Grid g)
                {

                    // find the grid index of the label (label is always in column 1, but index may differ)
                    UIElement current = null;
                    int idx = -1;
                    for (int i = 0; i < g.Children.Count; i++)
                    {
                        if (Grid.GetColumn(g.Children[i]) == 1)
                        {
                            current = g.Children[i];
                            idx = i;
                            break;
                        }
                    }

                    if (idx == -1) return;

                    // Determine whether the current visual is editable
                    bool currentlyEditable = current is TextBox;

                    // Only perform the replacement the the value has changed (not setting IsEditable = true when its already true)
                    if (currentlyEditable != value)
                    {
                        // Remove the existing label
                        g.Children.RemoveAt(idx);

                        //create the new label
                        UpdateLabel(value);

                        // insert the new label in correct position
                        g.Children.Insert(idx, label);
                        Grid.SetColumn(label, 1);
                    }
                }
            }
        }



        public bool isRemovable
        {
            get;
            set
            {
                button.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateLabel(bool editable)
        {

            HorizontalAlignment alignment = type == PortType.Input ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            if (editable)
            {
                TextBox textBox = new TextBox { Text = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
                textBox.TextChanged += (object sender, TextChangedEventArgs e) => { name = textBox.Text; };
                textBox.HorizontalAlignment = alignment;
                label = textBox;
            }
            else
            {
                TextBlock textBlock = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
                textBlock.HorizontalAlignment = alignment;
                label = textBlock;
            }
        }

        public Port(string name, PortType type, Node node, int socketSize = DEFAULT_SOCKET_SIZE)
        {
            guid = Guid.NewGuid();

            this.name = name;
            this.type = type;
            Background = Brushes.Transparent;
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            socket = new Socket(socketSize);
            socket.port = this;
            button = new Button { Content = "X", Width = BUTTON_WIDTH, Height = BUTTON_HEIGHT };
            button.Click += (s, e) => {
                Delete();
            };
            button.Visibility = Visibility.Collapsed;
            UpdateLabel(isEditable);
            if (type == PortType.Input)
            {
                connectionType = ConnectionType.Multi;
                Grid.SetColumn(socket, 0);
                Grid.SetColumn(label, 1);
                Grid.SetColumn(button, 2);
                grid.ColumnDefinitions[1].Width = GridLength.Auto;
                grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                connectionType = ConnectionType.Single;
                Grid.SetColumn(button, 0);
                Grid.SetColumn(label, 1);
                Grid.SetColumn(socket, 2);
            }
            grid.Children.Add(button);
            grid.Children.Add(socket);
            grid.Children.Add(label);
            Child = grid;
        }

        internal void ConnectionMade(Edge connection)
        {
            if (connectionType == ConnectionType.Single)
            {
                foreach (Edge edge in edges.ToList())
                {
                    edge.Delete();
                }
            }

            edges.Add(connection);
        }

        public void Delete()
        {
            if (parentContainer != null)
            {
                foreach (Edge edge in edges.ToList())
                {
                    edge.Delete();
                }

                parentContainer.Remove(this);
            }
        }

        //
        public JsonObject Save()
        {
            var obj = new JsonObject
            {
                ["id"] = guid.ToString(),
                ["name"] = name ?? string.Empty,
                ["portType"] = type.ToString(),
                ["connectionType"] = connectionType.ToString()
            };

            return obj;
        }

        // used whem loading a port from Json. this will create a Port instance and return it when all data is loaded into it
        public static Port Load(JsonObject obj, Node node)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            string name = obj["name"]?.GetValue<string>() ?? string.Empty;
            PortType pType = Enum.Parse<PortType>(obj["portType"]?.GetValue<string>() ?? PortType.Input.ToString());

            // create port with name and type
            Port p = new Port(name, pType, node);

            // restore GUID and connection type
            p.guid = Guid.Parse(obj["id"]?.GetValue<string>() ?? p.guid.ToString());
            p.connectionType = Enum.Parse<ConnectionType>(obj["connectionType"]?.GetValue<string>() ?? p.connectionType.ToString());

            // refresh label to reflect loaded name
            p.UpdateLabel(p.isEditable);

            return p;
        }
    }
}