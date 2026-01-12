using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XyGraph
{
    public class Graph : Canvas
    {
        private enum GraphState { None, Panning, DraggingNode, CreatingEdge }

        private GraphState currentState = GraphState.None;
        private Point lastMousePos;
        public Point rightClickPos;
        private UIElement draggedNode;
        private Point dragStartPos;
        private Port edgeStartPort;
        private Line tempConnectionLine;
        public List<Edge> edges = new List<Edge>();
        public List<Node> nodes = new List<Node>();
        private Port targetPort;

        private const double ZOOM_FACTOR = 1.1;
        private const double ZOOM_REDUCE = 0.9;
        private const int GRID_SIZE = 20;
        private const double SNAP_DISTANCE = 25;

        public StartNode startNode { get; internal set; }
        public EndNode endNode { get; internal set; }
        internal MenuItem startItem { get; private set; }
        internal MenuItem endItem { get; private set; }

        public Graph()
        {
            Background = Brushes.White;
            MouseWheel += OnMouseWheel;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonDown += OnMouseRightButtonDown;

            ContextMenu = new ContextMenu();

            startItem = new MenuItem { Header = "Create Start Node" };
            startItem.Click += (object sender, RoutedEventArgs e) => AddStartNode();
            ContextMenu.Items.Add(startItem);

            endItem = new MenuItem { Header = "Create End Node" };
            endItem.Click += (object sender, RoutedEventArgs e) => AddEndNode();
            ContextMenu.Items.Add(endItem);
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            rightClickPos = e.GetPosition(this);
        }

        private void AddStartNode()
        {
            if (startNode == null)
            {
                startNode = new StartNode(this);
                Canvas.SetLeft(startNode, rightClickPos.X - StartNode.OffsetX);
                Canvas.SetTop(startNode, rightClickPos.Y - StartNode.OffsetY);
                Children.Add(startNode);
                startItem.IsEnabled = false;
            }
        }

        private void AddEndNode()
        {
            if (endNode == null)
            {
                endNode = new EndNode(this);
                Canvas.SetLeft(endNode, rightClickPos.X - EndNode.OffsetX);
                Canvas.SetTop(endNode, rightClickPos.Y - EndNode.OffsetY);
                Children.Add(endNode);
                endItem.IsEnabled = false;
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double scale = e.Delta > 0 ? ZOOM_FACTOR : ZOOM_REDUCE;
            ScaleTransform matrix = LayoutTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            matrix.ScaleX *= scale;
            matrix.ScaleY *= scale;
            LayoutTransform = matrix;
        }

        private UIElement GetNodeFromSource(object source)
        {
            DependencyObject element = source as DependencyObject;
            while (element != null && element != this)
            {
                if (Children.Contains(element as UIElement))
                {
                    return element as UIElement;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }


        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(e.Source is Socket && currentState == GraphState.None)
            {
                Port port = (e.Source as Socket).port;
                edgeStartPort = port;
                currentState = GraphState.CreatingEdge;
                tempConnectionLine = new Line { Stroke = Brushes.Black, StrokeThickness = 2, IsHitTestVisible = false };
                Children.Add(tempConnectionLine);
                CaptureMouse();
            }
            else if (e.Source != this && currentState == GraphState.None)
            {
                UIElement node = GetNodeFromSource(e.Source);
                if (node != null)
                {
                    currentState = GraphState.DraggingNode;
                    draggedNode = node;
                    dragStartPos = e.GetPosition(this);
                    CaptureMouse();
                }
            }
            else if (e.Source == this && currentState == GraphState.None)
            {
                currentState = GraphState.Panning;
                lastMousePos = e.GetPosition(Window.GetWindow(this));
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (currentState == GraphState.CreatingEdge)
            {
                Point mousePos = e.GetPosition(this);
                int startOffset = edgeStartPort.socket.size / 2;
                Point startPos = edgeStartPort.socket.TranslatePoint(new Point(startOffset, startOffset), this);
                tempConnectionLine.X1 = startPos.X;
                tempConnectionLine.Y1 = startPos.Y;
                Point snapPos = mousePos;
                targetPort = null;
                double minDist = SNAP_DISTANCE;
                foreach (Node n in nodes)
                {
                    foreach (Port p in n.ports)
                    {
                        if (p != edgeStartPort && p.type != edgeStartPort.type)
                        {
                            int endOffset = p.socket.size / 2;
                            Point portPos = p.socket.TranslatePoint(new Point(endOffset, endOffset), this);
                            double dist = (mousePos - portPos).Length;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                snapPos = portPos;
                                targetPort = p;
                            }
                        }
                    }
                }
                if (startNode != null && startNode.port != edgeStartPort)
                {
                    Point portPos = startNode.port.socket.TranslatePoint(new Point(10, 10), this);
                    double dist = (mousePos - portPos).Length;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        snapPos = portPos;
                        targetPort = startNode.port;
                    }
                }
                if (endNode != null && endNode.port != edgeStartPort)
                {
                    Point portPos = endNode.port.socket.TranslatePoint(new Point(10, 10), this);
                    double dist = (mousePos - portPos).Length;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        snapPos = portPos;
                        targetPort = endNode.port;
                    }
                }
                tempConnectionLine.X2 = snapPos.X;
                tempConnectionLine.Y2 = snapPos.Y;
            }
            else if (currentState == GraphState.DraggingNode)
            {
                Point pos = e.GetPosition(this);
                Vector delta = pos - dragStartPos;
                double currentLeft = Canvas.GetLeft(draggedNode);
                double currentTop = Canvas.GetTop(draggedNode);
                Canvas.SetLeft(draggedNode, currentLeft + delta.X);
                Canvas.SetTop(draggedNode, currentTop + delta.Y);
                dragStartPos = pos;
                // Update edges
                if(draggedNode is Node node)
                    node.RedrawEdges();
                else if (draggedNode is StartNode start)
                {
                    foreach (Edge edge in start.port.edges)
                        edge.UpdatePosition();
                }
                else if (draggedNode is EndNode end)
                {
                    foreach (Edge edge in end.port.edges)
                        edge.UpdatePosition();
                }
            }
            else if (currentState == GraphState.Panning)
            {
                Point pos = e.GetPosition(Window.GetWindow(this));
                Vector delta = pos - lastMousePos;
                TranslateTransform transform = RenderTransform as TranslateTransform ?? new TranslateTransform();
                transform.X += delta.X;
                transform.Y += delta.Y;
                RenderTransform = transform;
                lastMousePos = pos;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (currentState == GraphState.CreatingEdge)
            {
                currentState = GraphState.None;
                Children.Remove(tempConnectionLine);
                tempConnectionLine = null;
                if (targetPort != null)
                {
                    CreateEdge(edgeStartPort, targetPort);
                }
                edgeStartPort = null;
                targetPort = null;
                ReleaseMouseCapture();
            }
            else if (currentState == GraphState.DraggingNode)
            {
                currentState = GraphState.None;
                draggedNode = null;
                ReleaseMouseCapture();
            }
            else if (currentState == GraphState.Panning)
            {
                currentState = GraphState.None;
                ReleaseMouseCapture();
            }
        }

        private void CreateEdge(Port from, Port to)
        {
            // Check if an edge already exists between these ports (bi-directional)
            if (edges.Any(edge => (edge.fromPort == from && edge.toPort == to) || (edge.fromPort == to && edge.toPort == from)))
            {
                return;
            }

            Edge conn = new Edge(this, from, to);
            conn.UpdatePosition();
            Children.Add(conn.visual);
            edges.Add(conn);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Pen pen = new Pen(Brushes.LightGray, 1);
            for (double x = 0; x < ActualWidth; x += GRID_SIZE)
            {
                dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
            }
            for (double y = 0; y < ActualHeight; y += GRID_SIZE)
            {
                dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
            }
        }
    }
}