using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text.Json.Nodes;

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

            // bind port-backed members to peer nodes when connection is made
            TryBindPortToPeer(fromPort, toPort.parentContainer?.node);
            TryBindPortToPeer(toPort, fromPort.parentContainer?.node);

            var fromNode = fromPort.parentContainer.node;
            var toNode = toPort.parentContainer.node;


        }

        public void UpdatePosition()
        {
            Point start = fromPort.socket.TranslatePoint(new Point(fromPort.socket.ActualWidth / 2, fromPort.socket.ActualHeight / 2), graph);
            Point end = toPort.socket.TranslatePoint(new Point(toPort.socket.ActualWidth / 2, toPort.socket.ActualHeight / 2), graph);

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
                Vector point1Offset = fromPort.direction == PortDirection.Output ? new Vector(BEZIER_STRENGTH, 0) : new Vector(-BEZIER_STRENGTH, 0);
                Vector point2Offset = toPort.direction == PortDirection.Input ? new Vector(-BEZIER_STRENGTH, 0) : new Vector(BEZIER_STRENGTH, 0);

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
            // unbind port-backed members before removing edge
            TryUnbindPortFromPeer(fromPort, toPort.parentContainer?.node);
            TryUnbindPortFromPeer(toPort, fromPort.parentContainer?.node);

            graph.Children.Remove(visual);
            graph.edges.Remove(this);
        }


        // Magic happens here
        // Uses Reflection to connect Nodes via their ports
        private void TryBindPortToPeer(Port port, Node peer)
        {
            if (port == null || peer == null || port.ownerMember == null) return;

            Node ownerNode = port.parentContainer?.node;
            if (ownerNode == null) return;

            try
            {
                if (port.ownerMember is System.Reflection.FieldInfo fi)
                {
                    if (typeof(Node).IsAssignableFrom(fi.FieldType))
                    {
                        fi.SetValue(ownerNode, peer);
                    }
                    else if (typeof(System.Collections.IList).IsAssignableFrom(fi.FieldType))
                    {
                        object listObj = fi.GetValue(ownerNode);
                        var list = listObj as System.Collections.IList;
                        if (list != null && !list.Contains(peer)) list.Add(peer);
                    }
                }
                else if (port.ownerMember is System.Reflection.PropertyInfo pi)
                {
                    if (!pi.CanWrite) return;
                    if (typeof(Node).IsAssignableFrom(pi.PropertyType))
                    {
                        pi.SetValue(ownerNode, peer);
                    }
                    else if (typeof(System.Collections.IList).IsAssignableFrom(pi.PropertyType))
                    {
                        object listObj = pi.GetValue(ownerNode);
                        var list = listObj as System.Collections.IList;
                        if (list != null && !list.Contains(peer)) list.Add(peer);
                    }
                }
            }
            catch
            {
                // ignore binding errors
            }
        }

        private void TryUnbindPortFromPeer(Port port, Node peer)
        {
            if (port == null || peer == null || port.ownerMember == null) return;

            Node ownerNode = port.parentContainer?.node;
            if (ownerNode == null) return;

            try
            {
                if (port.ownerMember is System.Reflection.FieldInfo fi)
                {
                    if (typeof(Node).IsAssignableFrom(fi.FieldType))
                    {
                        object cur = fi.GetValue(ownerNode);
                        if (object.ReferenceEquals(cur, peer)) fi.SetValue(ownerNode, null);
                    }
                    else if (typeof(System.Collections.IList).IsAssignableFrom(fi.FieldType))
                    {
                        object listObj = fi.GetValue(ownerNode);
                        var list = listObj as System.Collections.IList;
                        if (list != null && list.Contains(peer)) list.Remove(peer);
                    }
                }
                else if (port.ownerMember is System.Reflection.PropertyInfo pi)
                {
                    if (!pi.CanWrite) return;
                    if (typeof(Node).IsAssignableFrom(pi.PropertyType))
                    {
                        object cur = pi.GetValue(ownerNode);
                        if (object.ReferenceEquals(cur, peer)) pi.SetValue(ownerNode, null);
                    }
                    else if (typeof(System.Collections.IList).IsAssignableFrom(pi.PropertyType))
                    {
                        object listObj = pi.GetValue(ownerNode);
                        var list = listObj as System.Collections.IList;
                        if (list != null && list.Contains(peer)) list.Remove(peer);
                    }
                }
            }
            catch
            {
                // ignore unbind errors
            }
        }

        public JsonObject Save()
        {
            var obj = new JsonObject
            {
                ["id"] = guid.ToString(),
                ["from"] = fromPort?.guid.ToString() ?? string.Empty,
                ["to"] = toPort?.guid.ToString() ?? string.Empty,
                ["style"] = style.ToString()
            };

            return obj;
        }

        // Make sure you load all nodes before attempting to load edges
        public static Edge Load(JsonObject obj, Graph graph)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            Guid id = Guid.Parse(obj["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString());
            Guid fromId = Guid.Parse(obj["from"]?.GetValue<string>() ?? Guid.Empty.ToString());
            Guid toId = Guid.Parse(obj["to"]?.GetValue<string>() ?? Guid.Empty.ToString());

            Port fromPort = graph.GetPortById(fromId);
            Port toPort = graph.GetPortById(toId);
            if (fromPort == null || toPort == null) throw new Exception("Port could not be found! maek sure you load all nodes before loading any edges");

            Edge e = graph.CreateEdge(fromPort, toPort);
            if (e == null) return null;

            e.guid = id;
            e.style = Enum.Parse<EdgeStyle>(obj["style"]?.GetValue<string>() ?? e.style.ToString());

            e.UpdatePosition();

            return e;
        }
    }
}