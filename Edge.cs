using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Collections;

namespace XyGraph
{
    public enum EdgeStyle { Linear, Bezier }

    public class Edge
    {
        private const double BEZIER_STRENGTH = 100;
        private const double GRID_SIZE        = 10.0;

        // Arrow triangle dimensions (canvas coords).
        // Points: back-top (0,0), tip (12,5), back-bottom (0,10)
        // Centroid ≈ (4, 5) — used as rotation centre.
        private const double ARROW_W  = 12.0;
        private const double ARROW_H  = 10.0;
        private const double ARROW_CX = 4.0;   // centroid X
        private const double ARROW_CY = 5.0;   // centroid Y

        private const double CONTROL_RADIUS = 4.0;
        private const double ARM_LENGTH     = 60.0;
        private const int    CURVE_SAMPLES  = 24;

        public Guid guid;
        public Port outputPort { get; private set; }
        public Port inputPort  { get; private set; }
        public EdgeStyle style { get; private set; } = EdgeStyle.Bezier;
        public UIElement visual { get; private set; }
        private Graph graph;

        public List<EdgeHandle> handles = new List<EdgeHandle>();
        private bool interactionAttached = false;

        // ── Constructor ────────────────────────────────────────────────────────

        public Edge(Graph graph, Port fromPort, Port toPort)
        {
            if (fromPort == null || toPort == null) return;

            if (fromPort.direction == PortDirection.Output) this.outputPort = fromPort;
            else this.inputPort = fromPort;

            if (toPort.direction == PortDirection.Input) this.inputPort = toPort;
            else this.outputPort = toPort;

            guid       = Guid.NewGuid();
            this.graph = graph;

            fromPort.ConnectionMade(this);
            toPort.ConnectionMade(this);

            BindNodeReferenceToOwnerMember(fromPort, toPort.parentContainer?.node);
            BindNodeReferenceToOwnerMember(toPort, fromPort.parentContainer?.node);
        }

        // ── Draw ──────────────────────────────────────────────────────────────

        public void ReDraw()
        {
            if (outputPort == null || inputPort == null) return;

            Point start = outputPort.socket.TranslatePoint(new Point(outputPort.socket.ActualWidth / 2, outputPort.socket.ActualHeight / 2), graph);
            Point end   = inputPort.socket.TranslatePoint(new Point(inputPort.socket.ActualWidth / 2, inputPort.socket.ActualHeight / 2), graph);

            Brush stroke = GetStrokeBrush();

            if (style == EdgeStyle.Linear)
            {
                if (visual is Line line)
                {
                    line.X1 = start.X; line.Y1 = start.Y;
                    line.X2 = end.X;   line.Y2 = end.Y;
                    line.Stroke = stroke;
                    AttachEdgeInteraction(line);
                }
                else
                {
                    Line l = new Line { Stroke = stroke, StrokeThickness = 2, IsHitTestVisible = true, X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y };
                    AttachEdgeInteraction(l);
                    visual = l;
                }
            }
            else // Bezier / poly-bezier
            {
                Vector p1Off = outputPort.direction == PortDirection.Output ? new Vector(BEZIER_STRENGTH, 0) : new Vector(-BEZIER_STRENGTH, 0);
                Vector p2Off = inputPort.direction  == PortDirection.Input  ? new Vector(-BEZIER_STRENGTH, 0) : new Vector(BEZIER_STRENGTH, 0);

                PathFigure figure = new PathFigure { StartPoint = start };

                foreach (Point[] seg in BuildBezierSegments(start, end, p1Off, p2Off))
                    figure.Segments.Add(new BezierSegment { Point1 = seg[1], Point2 = seg[2], Point3 = seg[3] });

                PathGeometry geom = new PathGeometry();
                geom.Figures.Add(figure);

                if (visual is Path path)
                {
                    path.Data   = geom;
                    path.Stroke = stroke;
                    AttachEdgeInteraction(path);
                }
                else
                {
                    Path p = new Path { Stroke = stroke, StrokeThickness = 2, IsHitTestVisible = true, Data = geom };
                    AttachEdgeInteraction(p);
                    visual = p;
                }
            }
        }

        private Brush GetStrokeBrush()
        {
            if (inputPort  != null && inputPort.direction  == PortDirection.Input)  return inputPort.colour;
            if (outputPort != null && outputPort.direction == PortDirection.Input)   return outputPort.colour;
            return outputPort?.colour ?? Brushes.Black;
        }

        // ── Interaction attachment ────────────────────────────────────────────

        private void AttachEdgeInteraction(FrameworkElement element)
        {
            if (element.ContextMenu == null)
            {
                ContextMenu menu = new ContextMenu();
                MenuItem deleteItem = new MenuItem { Header = "Delete Edge" };
                deleteItem.Click += (s, e) => Delete();
                MenuItem addHandleItem = new MenuItem { Header = "Add Handle Here" };
                addHandleItem.Click += (s, e) => AddHandle(graph.rightClickPos);
                menu.Items.Add(deleteItem);
                menu.Items.Add(addHandleItem);
                element.ContextMenu = menu;
            }

            if (interactionAttached) return;
            interactionAttached = true;

            // Double-click inserts a handle at the clicked position.
            element.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount != 2) return;
                AddHandle(e.GetPosition(graph));
                e.Handled = true;
            };
        }

        // ── Curve geometry ────────────────────────────────────────────────────

        /// <summary>
        /// The poly-bezier as a list of cubic segments, each {P0, P1, P2, P3}. Segment i
        /// runs from handle i-1 (or the start socket) to handle i (or the end socket), so
        /// the index of the segment a point falls on is also the insertion index for a new
        /// handle at that point.
        /// </summary>
        private List<Point[]> BuildBezierSegments(Point start, Point end, Vector p1Off, Vector p2Off)
        {
            List<Point[]> segments = new List<Point[]>();

            if (handles.Count == 0)
            {
                segments.Add(new Point[] { start, start + p1Off, end + p2Off, end });
                return segments;
            }

            segments.Add(new Point[] { start, start + p1Off, handles[0].Position + handles[0].ControlIn, handles[0].Position });

            for (int i = 1; i < handles.Count; i++)
                segments.Add(new Point[]
                {
                    handles[i - 1].Position,
                    handles[i - 1].Position + handles[i - 1].ControlOut,
                    handles[i].Position     + handles[i].ControlIn,
                    handles[i].Position
                });

            EdgeHandle last = handles[handles.Count - 1];
            segments.Add(new Point[] { last.Position, last.Position + last.ControlOut, end + p2Off, end });

            return segments;
        }

        private int ClosestSegmentIndex(Point position)
        {
            Point start = outputPort.socket.TranslatePoint(new Point(outputPort.socket.ActualWidth / 2, outputPort.socket.ActualHeight / 2), graph);
            Point end   = inputPort.socket.TranslatePoint(new Point(inputPort.socket.ActualWidth / 2, inputPort.socket.ActualHeight / 2), graph);

            Vector p1Off = outputPort.direction == PortDirection.Output ? new Vector(BEZIER_STRENGTH, 0) : new Vector(-BEZIER_STRENGTH, 0);
            Vector p2Off = inputPort.direction  == PortDirection.Input  ? new Vector(-BEZIER_STRENGTH, 0) : new Vector(BEZIER_STRENGTH, 0);

            List<Point[]> segments = BuildBezierSegments(start, end, p1Off, p2Off);

            int best = 0;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < segments.Count; i++)
            {
                for (int step = 0; step <= CURVE_SAMPLES; step++)
                {
                    Point sample = SampleCubic(segments[i], (double)step / CURVE_SAMPLES);
                    double distance = (sample - position).LengthSquared;
                    if (distance < bestDistance) { bestDistance = distance; best = i; }
                }
            }

            return best;
        }

        private static Point SampleCubic(Point[] segment, double t)
        {
            double u = 1 - t;
            double a = u * u * u;
            double b = 3 * u * u * t;
            double c = 3 * u * t * t;
            double d = t * t * t;

            return new Point(
                a * segment[0].X + b * segment[1].X + c * segment[2].X + d * segment[3].X,
                a * segment[0].Y + b * segment[1].Y + c * segment[2].Y + d * segment[3].Y);
        }

        // ── Handle management ─────────────────────────────────────────────────

        public void AddHandle(Point position)
        {
            if (outputPort == null || inputPort == null) return;

            Point start = outputPort.socket.TranslatePoint(new Point(outputPort.socket.ActualWidth / 2, outputPort.socket.ActualHeight / 2), graph);
            Point end   = inputPort.socket.TranslatePoint(new Point(inputPort.socket.ActualWidth / 2, inputPort.socket.ActualHeight / 2), graph);

            Vector dir = end - start;
            if (dir.Length > 0) dir.Normalize();

            // Snap insertion position to grid
            Point snapped = Snap(position);

            EdgeHandle h = new EdgeHandle
            {
                Position   = snapped,
                ControlIn  = dir * -ARM_LENGTH,
                ControlOut = dir *  ARM_LENGTH
            };

            handles.Insert(ClosestSegmentIndex(position), h);

            BuildHandleVisuals(h);
            PositionHandleVisuals(h);
            ReDraw();
        }

        private void RemoveHandle(EdgeHandle h)
        {
            EdgeHandleSelection.ClearAll();
            RemoveHandleVisuals(h);
            handles.Remove(h);
            ReDraw();
        }

        private void RemoveHandleVisuals(EdgeHandle h)
        {
            graph.Children.Remove(h.anchorArrow);
            graph.Children.Remove(h.controlInDot);
            graph.Children.Remove(h.controlOutDot);
            graph.Children.Remove(h.controlInLine);
            graph.Children.Remove(h.controlOutLine);
        }

        private void BuildHandleVisuals(EdgeHandle h)
        {
            Brush edgeColor = GetStrokeBrush();

            // ── Anchor arrow (always visible) ──────────────────────────────────
            // Triangle pointing right: (0,0)→(ARROW_W, ARROW_H/2)→(0,ARROW_H)
            h.anchorArrow = new Polygon
            {
                Points = new PointCollection(new[]
                {
                    new Point(0,       0),
                    new Point(ARROW_W, ARROW_CY),
                    new Point(0,       ARROW_H)
                }),
                Fill             = edgeColor,
                Stroke           = Brushes.White,
                StrokeThickness  = 1.2,
                IsHitTestVisible = true,
                Cursor           = System.Windows.Input.Cursors.SizeAll
            };
            Panel.SetZIndex(h.anchorArrow, 20);

            // ── Control-arm lines (collapsed until selected) ───────────────────
            h.controlInLine  = new Line { Stroke = Brushes.DimGray, StrokeThickness = 1, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            h.controlOutLine = new Line { Stroke = Brushes.DimGray, StrokeThickness = 1, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            Panel.SetZIndex(h.controlInLine,  18);
            Panel.SetZIndex(h.controlOutLine, 18);

            // ── Control-arm dots (collapsed until selected) ────────────────────
            h.controlInDot = MakeControlDot();
            h.controlOutDot = MakeControlDot();
            Panel.SetZIndex(h.controlInDot,  21);
            Panel.SetZIndex(h.controlOutDot, 21);

            graph.Children.Add(h.controlInLine);
            graph.Children.Add(h.controlOutLine);
            graph.Children.Add(h.anchorArrow);
            graph.Children.Add(h.controlInDot);
            graph.Children.Add(h.controlOutDot);

            WireHandleInteraction(h);
        }

        private static Ellipse MakeControlDot() => new Ellipse
        {
            Width            = CONTROL_RADIUS * 2,
            Height           = CONTROL_RADIUS * 2,
            Fill             = Brushes.White,
            Stroke           = Brushes.DimGray,
            StrokeThickness  = 1,
            IsHitTestVisible = true,
            Visibility       = Visibility.Collapsed,
            Cursor           = System.Windows.Input.Cursors.SizeAll
        };

        private void WireHandleInteraction(EdgeHandle h)
        {
            // ── Selection: clicking the arrow selects this handle ──────────────
            h.anchorArrow.MouseLeftButtonDown += (s, e) =>
            {
                EdgeHandleSelection.Register(() =>
                {
                    h.controlInLine.Visibility  = Visibility.Collapsed;
                    h.controlOutLine.Visibility = Visibility.Collapsed;
                    h.controlInDot.Visibility   = Visibility.Collapsed;
                    h.controlOutDot.Visibility  = Visibility.Collapsed;
                });
                h.controlInLine.Visibility  = Visibility.Visible;
                h.controlOutLine.Visibility = Visibility.Visible;
                h.controlInDot.Visibility   = Visibility.Visible;
                h.controlOutDot.Visibility  = Visibility.Visible;
                e.Handled = true;
            };

            // ── Drag anchor (snaps to grid) ────────────────────────────────────
            h.anchorArrow.MouseMove += (s, e) =>
            {
                if (!h.draggingAnchor) return;
                h.Position = Snap(e.GetPosition(graph));
                ReDraw();
                PositionHandleVisuals(h);
            };
            h.anchorArrow.MouseLeftButtonUp += (s, e) =>
            {
                if (!h.draggingAnchor) return;
                h.draggingAnchor = false;
                h.anchorArrow.ReleaseMouseCapture();
                e.Handled = true;
            };

            // Separate MouseDown to start drag vs. select — MouseDown fires first
            // We use PreviewMouseLeftButtonDown to capture before the selection handler.
            h.anchorArrow.PreviewMouseLeftButtonDown += (s, e) =>
            {
                h.draggingAnchor = true;
                h.anchorArrow.CaptureMouse();
                // do NOT set e.Handled here — let the bubbling MouseLeftButtonDown fire the selection logic
            };

            // ── Drag control-in arm ────────────────────────────────────────────
            // Arms are always collinear: dragging one mirrors the direction onto
            // the other while preserving each arm's individual length.
            h.controlInDot.MouseLeftButtonDown += (s, e) => { h.draggingControlIn = true; h.controlInDot.CaptureMouse(); e.Handled = true; };
            h.controlInDot.MouseMove += (s, e) =>
            {
                if (!h.draggingControlIn) return;
                h.ControlIn = Snap(e.GetPosition(graph)) - h.Position;
                MirrorArm(ref h.ControlIn, ref h.ControlOut);
                ReDraw();
                PositionHandleVisuals(h);
            };
            h.controlInDot.MouseLeftButtonUp += (s, e) => { h.draggingControlIn = false; h.controlInDot.ReleaseMouseCapture(); e.Handled = true; };

            // ── Drag control-out arm ───────────────────────────────────────────
            h.controlOutDot.MouseLeftButtonDown += (s, e) => { h.draggingControlOut = true; h.controlOutDot.CaptureMouse(); e.Handled = true; };
            h.controlOutDot.MouseMove += (s, e) =>
            {
                if (!h.draggingControlOut) return;
                h.ControlOut = Snap(e.GetPosition(graph)) - h.Position;
                MirrorArm(ref h.ControlOut, ref h.ControlIn);
                ReDraw();
                PositionHandleVisuals(h);
            };
            h.controlOutDot.MouseLeftButtonUp += (s, e) => { h.draggingControlOut = false; h.controlOutDot.ReleaseMouseCapture(); e.Handled = true; };

            // ── Right-click on anchor to delete handle ─────────────────────────
            ContextMenu menu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Handle" };
            deleteItem.Click += (s, e) => RemoveHandle(h);
            menu.Items.Add(deleteItem);
            h.anchorArrow.ContextMenu = menu;
        }

        private void PositionHandleVisuals(EdgeHandle h)
        {
            // Arrow: rotated to face ControlOut direction, centroid at h.Position
            double angle = Math.Atan2(h.ControlOut.Y, h.ControlOut.X) * 180.0 / Math.PI;
            h.anchorArrow.RenderTransform = new RotateTransform(angle, ARROW_CX, ARROW_CY);
            Canvas.SetLeft(h.anchorArrow, h.Position.X - ARROW_CX);
            Canvas.SetTop (h.anchorArrow, h.Position.Y - ARROW_CY);

            // Control-in dot and line
            Point cpIn = h.Position + h.ControlIn;
            Canvas.SetLeft(h.controlInDot, cpIn.X - CONTROL_RADIUS);
            Canvas.SetTop (h.controlInDot, cpIn.Y - CONTROL_RADIUS);
            h.controlInLine.X1 = h.Position.X; h.controlInLine.Y1 = h.Position.Y;
            h.controlInLine.X2 = cpIn.X;       h.controlInLine.Y2 = cpIn.Y;

            // Control-out dot and line
            Point cpOut = h.Position + h.ControlOut;
            Canvas.SetLeft(h.controlOutDot, cpOut.X - CONTROL_RADIUS);
            Canvas.SetTop (h.controlOutDot, cpOut.Y - CONTROL_RADIUS);
            h.controlOutLine.X1 = h.Position.X; h.controlOutLine.Y1 = h.Position.Y;
            h.controlOutLine.X2 = cpOut.X;      h.controlOutLine.Y2 = cpOut.Y;
        }

        private static Point Snap(Point p) =>
            new Point(Math.Round(p.X / GRID_SIZE) * GRID_SIZE, Math.Round(p.Y / GRID_SIZE) * GRID_SIZE);

        // Keeps arms collinear: mirrors `source` direction onto `opposite` while
        // preserving `opposite`'s current length.
        private static void MirrorArm(ref Vector source, ref Vector opposite)
        {
            double len = opposite.Length;
            if (source.Length < 0.001 || len < 0.001) return;
            Vector dir = source;
            dir.Normalize();
            opposite = -dir * len;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Delete()
        {
            TryUnbindPortFromPeer(outputPort, inputPort.parentContainer?.node);
            TryUnbindPortFromPeer(inputPort, outputPort.parentContainer?.node);

            outputPort?.ConnectionRemoved(this);
            inputPort?.ConnectionRemoved(this);

            foreach (EdgeHandle h in handles) RemoveHandleVisuals(h);
            handles.Clear();

            graph.Children.Remove(visual);
            graph.edges.Remove(this);
        }

        // ── Serialisation ─────────────────────────────────────────────────────

        public JsonObject Save()
        {
            JsonObject obj = new JsonObject
            {
                ["id"]    = guid.ToString(),
                ["from"]  = outputPort?.guid.ToString() ?? string.Empty,
                ["to"]    = inputPort?.guid.ToString()  ?? string.Empty,
                ["style"] = style.ToString()
            };

            if (handles.Count > 0)
            {
                JsonArray arr = new JsonArray();
                foreach (EdgeHandle h in handles)
                {
                    arr.Add(new JsonObject
                    {
                        ["x"]   = h.Position.X,   ["y"]   = h.Position.Y,
                        ["cix"] = h.ControlIn.X,  ["ciy"] = h.ControlIn.Y,
                        ["cox"] = h.ControlOut.X, ["coy"] = h.ControlOut.Y
                    });
                }
                obj["handles"] = arr;
            }

            return obj;
        }

        public static Edge Load(JsonObject obj, Graph graph)
        {
            if (obj == null)   throw new ArgumentNullException(nameof(obj));
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            Guid id     = Guid.Parse(obj["id"]?.GetValue<string>()   ?? Guid.NewGuid().ToString());
            Guid fromId = Guid.Parse(obj["from"]?.GetValue<string>()  ?? Guid.Empty.ToString());
            Guid toId   = Guid.Parse(obj["to"]?.GetValue<string>()    ?? Guid.Empty.ToString());

            Port fromPort = graph.GetPortById(fromId);
            Port toPort   = graph.GetPortById(toId);
            if (fromPort == null || toPort == null)
                throw new Exception("Port could not be found. Make sure all nodes are loaded before loading edges.");

            Edge e = graph.CreateEdge(fromPort, toPort);
            if (e == null) return null;

            e.guid  = id;
            e.style = Enum.Parse<EdgeStyle>(obj["style"]?.GetValue<string>() ?? e.style.ToString());

            if (obj["handles"] is JsonArray handleArray)
            {
                foreach (JsonNode hn in handleArray)
                {
                    if (hn is not JsonObject ho) continue;
                    EdgeHandle h = new EdgeHandle
                    {
                        Position   = new Point(ho["x"].GetValue<double>(),   ho["y"].GetValue<double>()),
                        ControlIn  = new Vector(ho["cix"].GetValue<double>(), ho["ciy"].GetValue<double>()),
                        ControlOut = new Vector(ho["cox"].GetValue<double>(), ho["coy"].GetValue<double>())
                    };
                    e.handles.Add(h);
                    e.BuildHandleVisuals(h);
                    e.PositionHandleVisuals(h);
                }
            }

            e.ReDraw();
            return e;
        }

        // ── Node-reference binding (unchanged) ────────────────────────────────

        private void BindNodeReferenceToOwnerMember(Port port, Node peer)
        {
            if (port == null || peer == null || port.ownerMember == null) return;

            Node ownerNode = port.parentContainer?.node;
            if (ownerNode == null) return;

            try
            {
                MemberInfo member = port.ownerMember;
                Type memberType = null;
                Func<object> getMemberValue = null;
                Action<object> setMemberValue = null;

                if (member is FieldInfo fieldInfo)
                {
                    memberType     = fieldInfo.FieldType;
                    getMemberValue = () => fieldInfo.GetValue(ownerNode);
                    setMemberValue = v  => fieldInfo.SetValue(ownerNode, v);
                }
                else if (member is PropertyInfo propInfo)
                {
                    if (!propInfo.CanWrite) return;
                    memberType     = propInfo.PropertyType;
                    getMemberValue = () => propInfo.GetValue(ownerNode);
                    setMemberValue = v  => propInfo.SetValue(ownerNode, v);
                }
                if (memberType == null || getMemberValue == null || setMemberValue == null) return;

                if (typeof(Node).IsAssignableFrom(memberType))
                {
                    setMemberValue(peer);
                }
                else if (typeof(IList).IsAssignableFrom(memberType))
                {
                    object listObj = getMemberValue();
                    IList list = listObj as IList;

                    if (list != null)
                    {
                        if (port.ownerIndex >= 0)
                        {
                            int idx = port.ownerIndex;
                            while (list.Count <= idx) list.Add(null);
                            list[idx] = peer;
                        }
                        else
                        {
                            if (!list.Contains(peer)) list.Add(peer);
                        }
                    }
                    else if (memberType.IsGenericType)
                    {
                        Type elemType = memberType.GetGenericArguments()[0];
                        Type listType = typeof(List<>).MakeGenericType(elemType);
                        object newListObj = Activator.CreateInstance(listType);
                        IList newList = newListObj as IList;
                        if (newList != null)
                        {
                            if (port.ownerIndex >= 0)
                            {
                                int idx = port.ownerIndex;
                                while (newList.Count <= idx) newList.Add(null);
                                newList[idx] = peer;
                            }
                            else
                            {
                                newList.Add(peer);
                            }
                            setMemberValue(newList);
                        }
                    }
                }
            }
            catch { }
        }

        private void TryUnbindPortFromPeer(Port port, Node peer)
        {
            if (port == null || peer == null || port.ownerMember == null) return;

            Node ownerNode = port.parentContainer?.node;
            if (ownerNode == null) return;

            try
            {
                MemberInfo member = port.ownerMember;
                Type memberType = null;
                Func<object> getter = null;
                Action<object> setter = null;

                if (member is FieldInfo fi)
                {
                    memberType = fi.FieldType;
                    getter     = () => fi.GetValue(ownerNode);
                    setter     = v  => fi.SetValue(ownerNode, v);
                }
                else if (member is PropertyInfo pi)
                {
                    if (!pi.CanWrite) return;
                    memberType = pi.PropertyType;
                    getter     = () => pi.GetValue(ownerNode);
                    setter     = v  => pi.SetValue(ownerNode, v);
                }
                if (memberType == null || getter == null) return;

                if (typeof(Node).IsAssignableFrom(memberType))
                {
                    object cur = getter();
                    if (object.ReferenceEquals(cur, peer)) setter?.Invoke(null);
                }
                else if (typeof(IList).IsAssignableFrom(memberType))
                {
                    IList list = getter() as IList;
                    if (list != null)
                    {
                        if (port.ownerIndex >= 0)
                        {
                            int idx = port.ownerIndex;
                            if (idx < list.Count && object.ReferenceEquals(list[idx], peer))
                                list[idx] = null;
                        }
                        else
                        {
                            if (list.Contains(peer)) list.Remove(peer);
                        }
                    }
                }
            }
            catch { }
        }
    }

    public class EdgeHandle
    {
        public Point  Position;
        public Vector ControlIn;    // offset from Position to the incoming tangent control point
        public Vector ControlOut;   // offset from Position to the outgoing tangent control point

        // drag state
        internal bool draggingAnchor;
        internal bool draggingControlIn;
        internal bool draggingControlOut;

        // visuals (owned by Edge)
        internal Polygon anchorArrow;
        internal Ellipse controlInDot;
        internal Ellipse controlOutDot;
        internal Line    controlInLine;
        internal Line    controlOutLine;
    }
}
