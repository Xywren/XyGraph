using System.Windows;
using System.Windows.Shapes;

namespace GraphViewPrototype
{
    public class Edge
    {
        public Port FromPort { get; set; }
        public Port ToPort { get; set; }
        public Line Line { get; set; }

        public void UpdatePosition(Graph canvas)
        {
            Point start = FromPort.TranslatePoint(new Point(5, 5), canvas);
            Point end = ToPort.TranslatePoint(new Point(5, 5), canvas);
            Line.X1 = start.X;
            Line.Y1 = start.Y;
            Line.X2 = end.X;
            Line.Y2 = end.Y;
        }
    }
}