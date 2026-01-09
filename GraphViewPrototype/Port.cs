using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public enum NodeType { Input, Output }

    public class Socket : Border
    {
        public Port Port { get; set; }

        public Socket()
        {
            Width = 10;
            Height = 10;
            Background = Brushes.Black;
            CornerRadius = new CornerRadius(5);
        }
    }

    public class Port : Border
    {
        public NodeType Type { get; set; }
        public string Name { get; set; }
        public Socket Socket { get; private set; }
        private UIElement label;
        private bool isEditable = false;

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
                        g.Children.Remove(label);
                        UpdateLabel();
                        g.Children.Add(label);
                    }
                }
            }
        }

        private void UpdateLabel()
        {
            if (isEditable)
            {
                TextBox textBox = new TextBox { Text = Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
                textBox.TextChanged += (object sender, TextChangedEventArgs e) => { Name = textBox.Text; };
                if (Type == NodeType.Input)
                {
                    textBox.HorizontalAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    textBox.HorizontalAlignment = HorizontalAlignment.Right;
                }
                label = textBox;
            }
            else
            {
                TextBlock textBlock = new TextBlock { Text = Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
                if (Type == NodeType.Input)
                {
                    textBlock.HorizontalAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    textBlock.HorizontalAlignment = HorizontalAlignment.Right;
                }
                label = textBlock;
            }
        }

        public Port(string name, NodeType type)
        {
            Name = name;
            Type = type;
            Background = Brushes.Transparent;
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Socket = new Socket();
            Socket.Port = this;
            UpdateLabel();
            if (Type == NodeType.Input)
            {
                Grid.SetColumn(Socket, 0);
                Grid.SetColumn(label, 1);
                grid.ColumnDefinitions[0].Width = GridLength.Auto;
                grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                Grid.SetColumn(label, 0);
                Grid.SetColumn(Socket, 1);
            }
            grid.Children.Add(Socket);
            grid.Children.Add(label);
            Child = grid;
        }
    }
}