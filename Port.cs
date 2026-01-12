using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public enum NodeType { Input, Output }

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
        private const int DEFAULT_SOCKET_SIZE = 10;
        private const int BUTTON_WIDTH = 40;
        private const int BUTTON_HEIGHT = 20;

        public NodeType type;
        public string name;
        public Socket socket;
        private UIElement label;
        internal NodeContainer parentContainer;
        private Button button;

        public bool isEditable
        {
            get;
            set
            {
                if (Child is Grid g)
                {
                    int idx = g.Children.IndexOf(label);
                    g.Children.RemoveAt(idx);
                    UpdateLabel();
                    g.Children.Insert(idx, label);
                    Grid.SetColumn(label, 1);
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

        private void UpdateLabel()
        {

            HorizontalAlignment alignment = type == NodeType.Input ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            if (isEditable)
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

        public Port(string name, NodeType type, Node node, int socketSize = DEFAULT_SOCKET_SIZE)
        {
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
                if (parentContainer != null)
                {
                    parentContainer.stackPanel.Children.Remove(this);
                    parentContainer.node.ports.Remove(this);
                    parentContainer.node.PortsChanged();
                }
            };
            button.Visibility = Visibility.Collapsed;
            UpdateLabel();
            if (type == NodeType.Input)
            {
                Grid.SetColumn(socket, 0);
                Grid.SetColumn(label, 1);
                Grid.SetColumn(button, 2);
                grid.ColumnDefinitions[1].Width = GridLength.Auto;
                grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                Grid.SetColumn(button, 0);
                Grid.SetColumn(label, 1);
                Grid.SetColumn(socket, 2);
            }
            grid.Children.Add(button);
            grid.Children.Add(socket);
            grid.Children.Add(label);
            Child = grid;
        }
    }
}