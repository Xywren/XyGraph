using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public enum NodeType { Input, Output }

    public class Port : Border
    {
        public NodeType Type { get; set; }
        public string Name { get; set; }

        public Port(string name, NodeType type)
        {
            Name = name;
            Type = type;
            Width = 10;
            Height = 10;
            Background = Brushes.Black;
            CornerRadius = new CornerRadius(5);
        }
    }
}