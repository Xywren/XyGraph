using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class StartNode : Border
    {
        public Port Port { get; private set; }

        public StartNode()
        {
            Port = new Port("", NodeType.Output, null, 20);
            Port.IsEditable = false;
            Port.IsRemovable = false;
            Child = Port;
        }
    }
}