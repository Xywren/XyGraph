using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XyGraph
{
    public enum EdgeStyle { Linear, Bezier }

    public class Edge
    {

        private const double BEZIER_STRENGTH = 100;

        public Port FromPort { get; set; }
        public Port ToPort { get; set; }
        public EdgeStyle Style { get; set; } = EdgeStyle.Bezier;
        public UIElement Visual { get; set; }
        private Graph graph;


        public Edge(Graph graph, Port fromPort, Port toPort)
        {
            this.graph = graph;
            FromPort = fromPort;
            ToPort = toPort;
        }

        public void UpdatePosition(Graph canvas)
        {
            Point start = FromPort.Socket.TranslatePoint(new Point(FromPort.Socket.Size / 2, FromPort.Socket.Size / 2), canvas);
            Point end = ToPort.Socket.TranslatePoint(new Point(ToPort.Socket.Size / 2, ToPort.Socket.Size / 2), canvas);

            if (Style == EdgeStyle.Linear)
            {
                if (Visual is Line line)
                {
                    line.X1 = start.X;
                    line.Y1 = start.Y;
                    line.X2 = end.X;
                    line.Y2 = end.Y;
                }
                else
                {
                    Visual = new Line { Stroke = Brushes.Black, StrokeThickness = 2, IsHitTestVisible = false, X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y };
                }
            }
            else // Bezier
            {
                PathGeometry geometry = new PathGeometry();
                PathFigure figure = new PathFigure { StartPoint = start };
                BezierSegment segment = new BezierSegment
                {
                    Point1 = start + new Vector(BEZIER_STRENGTH, 0),
                    Point2 = end + new Vector(-BEZIER_STRENGTH, 0),
                    Point3 = end
                };
                figure.Segments.Add(segment);
                geometry.Figures.Add(figure);

                if (Visual is Path path)
                {
                    path.Data = geometry;
                }
                else
                {
                    Visual = new Path { Stroke = Brushes.Black, StrokeThickness = 2, IsHitTestVisible = false, Data = geometry };
                }
            }
        }

        public void Delete()
        {
            graph.Children.Remove(Visual);
            graph.edges.Remove(this);
        }
    }
}