using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GraphViewPrototype
{
    public class Graph : Canvas
    {
        private enum GraphState { None, Panning, DraggingNode, CreatingEdge }

        private GraphState currentState = GraphState.None;
        private Point lastMousePos;
        private Point nodeCreatePos;
        private UIElement draggedNode;
        private Point dragStartPos;
        private Port edgeStartPort;
        private Line tempConnectionLine;
        private List<Edge> edges = new List<Edge>();
        private List<Node> nodes = new List<Node>();
        private Port targetPort;

        private const double ZOOM_FACTOR = 1.1;
        private const double ZOOM_REDUCE = 0.9;
        private const int GRID_SIZE = 20;
        private const double NODE_OFFSET_X = 75;
        private const double NODE_OFFSET_Y = 50;
        private const double SNAP_DISTANCE = 25;

        public Graph()
        {
            Background = Brushes.White;
            MouseWheel += OnMouseWheel;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonDown += OnMouseRightButtonDown;
            ContextMenu = new ContextMenu();
            MenuItem createItem = new MenuItem { Header = "Create Node" };
            createItem.Click += (object sender, RoutedEventArgs e) => AddNode();
            ContextMenu.Items.Add(createItem);
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            nodeCreatePos = e.GetPosition(this);
        }

        private void AddNode()
        {
            Node node = new Node();
            nodes.Add(node);
            Canvas.SetLeft(node, nodeCreatePos.X - NODE_OFFSET_X);
            Canvas.SetTop(node, nodeCreatePos.Y - NODE_OFFSET_Y);
            Children.Add(node);
            node.TitleContainer.Add(new TextBlock { Text = "Title", Foreground = Brushes.White });
            node.TitleContainer.Visibility = Visibility.Visible;
            node.TopContainer.Add(new TextBlock { Text = "Top", Foreground = Brushes.White });
            Port inputPort = new Port("Input", NodeType.Input);
            Port outputPort = new Port("Output with a really really  long name", NodeType.Output);
            node.InputContainer.Add(inputPort);
            Button addOutputButton = new Button { Content = "Add Output", FontSize = 8, Height = 20 };
            addOutputButton.Click += (s, e) => {
                Port newPort = new Port("Output", NodeType.Output);
                node.OutputContainer.Add(newPort);
            };
            node.OutputContainer.Add(addOutputButton);
            node.MainContainer.Add(new TextBlock { Text = "Main", Foreground = Brushes.White });
            node.OutputContainer.Add(outputPort);
            node.BottomContainer.Add(new TextBlock { Text = "Bottom", Foreground = Brushes.White });
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
                Port port = (e.Source as Socket).Port;
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
                Point startPos = edgeStartPort.Socket.TranslatePoint(new Point(5, 5), this);
                tempConnectionLine.X1 = startPos.X;
                tempConnectionLine.Y1 = startPos.Y;
                Point snapPos = mousePos;
                targetPort = null;
                double minDist = SNAP_DISTANCE;
                foreach (Node n in nodes)
                {
                    foreach (Port p in n.Ports)
                    {
                        if (p != edgeStartPort && p.Type != edgeStartPort.Type)
                        {
                            Point portPos = p.Socket.TranslatePoint(new Point(5, 5), this);
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
                foreach (Edge conn in edges)
                {
                    conn.UpdatePosition(this);
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
            Edge conn = new Edge { FromPort = from, ToPort = to };
            conn.UpdatePosition(this);
            Children.Add(conn.Visual);
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