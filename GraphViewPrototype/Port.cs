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

        public Port(string name, NodeType type)
        {
            Name = name;
            Type = type;
            Background = Brushes.Transparent;
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Socket = new Socket();
            Socket.Port = this;
            TextBlock label = new TextBlock { Text = Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
            if (Type == NodeType.Output)
            {
                Grid.SetColumn(label, 0);
                Grid.SetColumn(Socket, 1);
            }
            else
            {
                Grid.SetColumn(Socket, 0);
                Grid.SetColumn(label, 1);
            }
            grid.Children.Add(Socket);
            grid.Children.Add(label);
            Child = grid;
        }
    }
}