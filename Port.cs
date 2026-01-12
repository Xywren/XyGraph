using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public enum NodeType { Input, Output }

    public class Socket : Border
    {
        public Port Port { get; set; }
        public int Size;

        public Socket(int size = 10)
        {
            Size = size;
            Width = Size;
            Height = Size;
            Background = Brushes.Black;
            CornerRadius = new CornerRadius(Size);
        }
    }

    public class Port : Border
    {
        public NodeType Type { get; set; }
        public string Name { get; set; }
        public Socket Socket { get; private set; }
        private UIElement label;
        private bool isEditable = false;
        private Node parentNode;
        internal NodeContainer parentContainer;
        private Button button;
        private bool isRemovable = false;

        public bool IsEditable
        {
            get { return isEditable; }
            set
            {
                if (isEditable != value)
                {
                    isEditable = value;
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
        }

        public bool IsRemovable
        {
            get { return isRemovable; }
            set
            {
                if (isRemovable != value)
                {
                    isRemovable = value;
                    button.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void UpdateLabel()
        {

            HorizontalAlignment alignment = Type == NodeType.Input ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            if (isEditable)
            {
                TextBox textBox = new TextBox { Text = Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
                textBox.TextChanged += (object sender, TextChangedEventArgs e) => { Name = textBox.Text; };
                textBox.HorizontalAlignment = alignment;
                label = textBox;
            }
            else
            {
                TextBlock textBlock = new TextBlock { Text = Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
                textBlock.HorizontalAlignment = alignment;
                label = textBlock;
            }
        }

        public Port(string name, NodeType type, Node node, int socketSize = 10)
        {
            Name = name;
            Type = type;
            Background = Brushes.Transparent;
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); 
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); 
            Socket = new Socket(socketSize);
            Socket.Port = this;
            button = new Button { Content = "X", Width = 40, Height = 20 };
            button.Click += (s, e) => {
                if (parentContainer != null)
                {
                    parentContainer.stackPanel.Children.Remove(this);
                    parentContainer.node.Ports.Remove(this);
                    node.PortsChanged();
                }
            };
            button.Visibility = isRemovable ? Visibility.Visible : Visibility.Collapsed;
            UpdateLabel();
            if (Type == NodeType.Input)
            {
                Grid.SetColumn(Socket, 0);
                Grid.SetColumn(label, 1);
                Grid.SetColumn(button, 2);
                grid.ColumnDefinitions[1].Width = GridLength.Auto;
                grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                Grid.SetColumn(button, 0);
                Grid.SetColumn(label, 1);
                Grid.SetColumn(Socket, 2);
            }
            grid.Children.Add(button);
            grid.Children.Add(Socket);
            grid.Children.Add(label);
            Child = grid;
        }
    }
}