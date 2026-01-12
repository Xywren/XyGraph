using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XyGraph
{
    public enum EdgeStyle { Linear, Bezier }

    public class Edge
    {

        private const double BEZIER_STRENGTH = 100;

        public Guid guid;

        public Port fromPort { get; private set; }
        public Port toPort { get; private set; }
        public EdgeStyle style { get; private set; } = EdgeStyle.Bezier;
        public UIElement visual { get; private set; }
        private Graph graph;


        public Edge(Graph graph, Port fromPort, Port toPort)
        {
            guid = Guid.NewGuid();

            this.graph = graph;
            this.fromPort = fromPort;
            this.toPort = toPort;

            fromPort.ConnectionMade(this);
            toPort.ConnectionMade(this);
        }

        public void UpdatePosition()
        {
            Point start = fromPort.socket.TranslatePoint(new Point(fromPort.socket.size / 2, fromPort.socket.size / 2), graph);
            Point end = toPort.socket.TranslatePoint(new Point(toPort.socket.size / 2, toPort.socket.size / 2), graph);

            if (style == EdgeStyle.Linear)
            {
                if (visual is Line line)
                {
                    line.X1 = start.X;
                    line.Y1 = start.Y;
                    line.X2 = end.X;
                    line.Y2 = end.Y;
                }
                else
                {
                    visual = new Line { Stroke = Brushes.Black, StrokeThickness = 2, IsHitTestVisible = false, X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y };
                }
            }
            else // Bezier
            {
                Vector point1Offset = fromPort.type == PortType.Output ? new Vector(BEZIER_STRENGTH, 0) : new Vector(-BEZIER_STRENGTH, 0);
                Vector point2Offset = toPort.type == PortType.Input ? new Vector(-BEZIER_STRENGTH, 0) : new Vector(BEZIER_STRENGTH, 0);

                PathGeometry geometry = new PathGeometry();
                PathFigure figure = new PathFigure { StartPoint = start };
                BezierSegment segment = new BezierSegment
                {
                    Point1 = start + point1Offset,
                    Point2 = end + point2Offset,
                    Point3 = end
                };
                figure.Segments.Add(segment);
                geometry.Figures.Add(figure);

                if (visual is Path path)
                {
                    path.Data = geometry;
                }
                else
                {
                    visual = new Path { Stroke = Brushes.Black, StrokeThickness = 2, IsHitTestVisible = false, Data = geometry };
                }
            }
        }

        public void Delete()
        {
            graph.Children.Remove(visual);
            graph.edges.Remove(this);
        }
    }
}