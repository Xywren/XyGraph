using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class Port : Border
    {
        public bool IsInput { get; set; }

        public Port()
        {
            Width = 10;
            Height = 10;
            Background = Brushes.Black;
            CornerRadius = new CornerRadius(5);
        }
    }
}