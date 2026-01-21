using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Globalization;
using System.Windows.Data;

namespace XyGraph
{
    public partial class GraphView : UserControl
    {
        private bool sidebarSlidOff = false;
        private System.Windows.Media.Animation.DoubleAnimation slideAnimation;
        private const double WORLD_SIZE = 10000;
        private const int VISUAL_TREE_SEARCH_DEPTH = 10;
        // Cached transforms for the inner graph to avoid repeated visual-tree searches
        private TransformGroup transformGroup;
        private ScaleTransform scaleTransform;
        private TranslateTransform translateTransform;

        public Graph Graph => graph;
        public Point rightClickPos => graph.rightClickPos;


        public GraphView()
        {
            InitializeComponent();
            Loaded += GraphView_Loaded;
            SizeChanged += GraphView_SizeChanged;

            // Set Graph to desired Size
            graph.WorldSize = WORLD_SIZE;
            graph.Width = WORLD_SIZE;
            graph.Height = WORLD_SIZE;

            // initialize transforms for zoom/pan
            ScaleTransform scale = new ScaleTransform(1.0, 1.0);
            TranslateTransform translate = new TranslateTransform(0, 0);
            TransformGroup tg = new TransformGroup();
            tg.Children.Add(scale);
            tg.Children.Add(translate);
            graph.RenderTransform = tg;

            // cache transforms for reuse
            scaleTransform = scale;
            translateTransform = translate;
            transformGroup = tg;

            // wire up event handlers for the graph
            this.MouseWheel += GraphView_MouseWheel;
            this.MouseLeftButtonDown += GraphView_MouseLeftButtonDown;
            this.MouseMove += GraphView_MouseMove;
            this.MouseLeftButtonUp += GraphView_MouseLeftButtonUp;
            this.MouseRightButtonDown += GraphView_MouseRightButtonDown;

            // sidebar toggle button
            ToggleSidebarButton.Click += ToggleSidebarButton_Click;
        }

        private void GraphView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSidebarLayout();
        }

        private void UpdateSidebarLayout()
        {
            const double toggleHalf = 14.0; // half of toggle width used for overlap
            double totalWidth = this.ActualWidth;
            if (double.IsNaN(totalWidth) || totalWidth <= 0.0) return;


            // position the toggle button inside the container so it overlaps the right edge of the sidebar
            double buttonLeft = SidebarContent.Width - toggleHalf;
            ToggleSidebarButton.Margin = new Thickness(buttonLeft, 0, 0, 0);
            ToggleSidebarButton.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSidebar();
        }

        private void ToggleSidebar()
        {
            const double animationDurationSeconds = 0.32;

            if (!sidebarSlidOff)
            {
                double to = -SidebarContent.Width;
                slideAnimation = new System.Windows.Media.Animation.DoubleAnimation(0, to, new Duration(TimeSpan.FromSeconds(animationDurationSeconds)));
                slideAnimation.EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
                SidebarContainerTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideAnimation);
                ToggleSidebarButton.ToolTip = "Expand sidebar";
                ToggleIconPath.Data = Geometry.Parse("M8.59,16.58L13.17,12L8.59,7.41L10,6L16,12L10,18L8.59,16.58Z");
                sidebarSlidOff = true;
            }
            else
            {
                slideAnimation = new System.Windows.Media.Animation.DoubleAnimation(SidebarContainerTranslate.X, 0, new Duration(TimeSpan.FromSeconds(animationDurationSeconds)));
                slideAnimation.EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
                SidebarContainerTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideAnimation);
                ToggleSidebarButton.ToolTip = "Collapse sidebar";
                ToggleIconPath.Data = Geometry.Parse("M15.41,16.58L10.83,12L15.41,7.41L14,6L8,12L14,18L15.41,16.58Z");
                sidebarSlidOff = false;
            }
            UpdateSidebarLayout();
        }

        // Walk up to VISUAL_TREE_SEARCH_DEPTH ancestors to find a Socket. Returns null if none found.
        private Socket GetSocketFromSource(object source)
        {
            DependencyObject element = source as DependencyObject;
            int depth = 0;
            while (element != null && element != graph && depth < VISUAL_TREE_SEARCH_DEPTH)
            {
                if (element is Socket s) return s;
                element = VisualTreeHelper.GetParent(element);
                depth++;
            }
            return null;
        }

        // Expose a helper to run the graph's start node from higher-level UIs
        public void Run()
        {
            graph.Run();
        }


        // The visual Canvas used for the graph uses content coordinates with (0,0) at the top-left
        // while the graph's logical/world origin is intended to be the center.
        // On load the view must pan the canvas so that the graph's center is visible at the control's center. This method performs that initial centering.
        private void GraphView_Loaded(object sender, RoutedEventArgs e)
        {
            double graphOriginOffset = WORLD_SIZE / 2.0;
            double viewportCenterX = ActualWidth / 2.0;
            double viewportCenterY = ActualHeight / 2.0;

            Matrix transformMatrix = transformGroup.Value;
            Point mapped = transformMatrix.Transform(new Point(graphOriginOffset, graphOriginOffset));
            translateTransform.X += viewportCenterX - mapped.X;
            translateTransform.Y += viewportCenterY - mapped.Y;
            UpdateSidebarLayout();
        }

        public void AddNode(Node n, double posX = 0, double posY = 0)
        {
            graph.AddNode(n, posX, posY);
        }


        // used to reset and scaling and traslation to recenter the graph
        public void Recenter()
        {
            const double WORLD_HALF = WORLD_SIZE / 2.0;

            // ensure layout is up to date so ActualWidth/Height are correct
            UpdateLayout();

            Point viewportCenter = new Point(this.ActualWidth / 2.0, this.ActualHeight / 2.0);
            Point worldCenterContent = new Point(WORLD_HALF, WORLD_HALF);

            // reset scale to 1.0
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;

            // compute where the content point is currently mapped to (after transforms)
            Matrix m = transformGroup.Value;
            Point mapped = m.Transform(worldCenterContent);

            // shift translate so mapped point lands at viewport center
            translateTransform.X += viewportCenter.X - mapped.X;
            translateTransform.Y += viewportCenter.Y - mapped.Y;
        }


        // convert viewport (control) point to centered world coordinates (-half..+half)
        public Point ViewportToWorld(Point viewportPoint)
        {
            Matrix graphMatrix = transformGroup.Value;
            Matrix inverse = graphMatrix;
            inverse.Invert();
            Point contentPoint = inverse.Transform(viewportPoint);

            double half = WORLD_SIZE / 2.0;
            return new Point(contentPoint.X - half, contentPoint.Y - half);
        }

        // convert viewport (control) point to content/canvas coordinates (0..WorldSize)
        private Point ViewportToCanvas(Point viewportPoint)
        {
            Matrix graphMatrix = transformGroup.Value;
            Matrix inverse = graphMatrix;
            inverse.Invert();
            return inverse.Transform(viewportPoint);
        }

        // convert centered world to viewport coords
        public Point WorldToViewport(Point worldPoint)
        {
            double half = WORLD_SIZE / 2.0;
            Point contentPoint = new Point(worldPoint.X + half, worldPoint.Y + half);
            Matrix graphMatrix = transformGroup.Value;
            return graphMatrix.Transform(contentPoint);
        }




        // =======================================================================
        //                     Mouse state & handlers
        // =======================================================================

        private enum MouseState { None, CreatingEdge, DraggingNode, Panning }
        private MouseState mouseState = MouseState.None;

        private Point lastMousePos;
        private UIElement draggedNode = null;
        private Point dragStartContent;
        private Port edgeStartPort = null;
        private Line tempConnectionLine = null;
        private Port targetPort = null;
        private const double SNAP_DISTANCE = 25.0;

        private void GraphView_MouseWheel(object sender, MouseWheelEventArgs e)
        {

            Point mousePos = e.GetPosition(this);

            double oldScale = scaleTransform.ScaleX;
            if (oldScale <= 0.0) oldScale = 0.0001;

            double SCALE_STEP = 0.1;
            double MIN_SCALE = 0.2;
            double MAX_SCALE = 2.0;

            double newScale = oldScale * (e.Delta > 0 ? (1 + SCALE_STEP) : (1 - SCALE_STEP));
            newScale = System.Math.Round(newScale, 2);
            if (newScale < MIN_SCALE) newScale = MIN_SCALE;
            if (newScale > MAX_SCALE) newScale = MAX_SCALE;

            // Compute content coordinates under the mouse before zoom
            Point contentBefore = new Point(
                (mousePos.X - translateTransform.X) / oldScale,
                (mousePos.Y - translateTransform.Y) / oldScale
            );

            // Apply new scale
            scaleTransform.ScaleX = newScale;
            scaleTransform.ScaleY = newScale;

            // Adjust translate so the same content point remains under the mouse
            translateTransform.X = mousePos.X - (contentBefore.X * newScale);
            translateTransform.Y = mousePos.Y - (contentBefore.Y * newScale);
        }

        private void GraphView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (mouseState != MouseState.None) return;

            // if a socket (or a child of a socket) was clicked, start creating an edge
            Socket clickedSocket = GetSocketFromSource(e.OriginalSource);
            if (clickedSocket != null)
            {
                edgeStartPort = clickedSocket.port;
                if (edgeStartPort == null)
                {
                    edgeStartPort = clickedSocket.port;
                }
                mouseState = MouseState.CreatingEdge;
                tempConnectionLine = new Line { Stroke = Brushes.Black, StrokeThickness = 2, IsHitTestVisible = false };
                // add to graph so it uses the same coordinate space
                graph.Children.Add(tempConnectionLine);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            // look for a Node
            UIElement nodeElement = graph.GetNodeFromSource(e.OriginalSource);
            if (nodeElement != null)
            {
                mouseState = MouseState.DraggingNode;
                draggedNode = nodeElement;
                // record drag start in content (canvas) coordinates
                dragStartContent = ViewportToWorld(e.GetPosition(this));
                CaptureMouse();
                e.Handled = true;
                return;
            }

            // otherwise if we actually clicked the graph background start panning
            if (e.OriginalSource == graph || e.Source == graph)
            {
                mouseState = MouseState.Panning;
                lastMousePos = e.GetPosition(Window.GetWindow(this));
                CaptureMouse();
                e.Handled = true;
            }
        }

        private void GraphView_MouseMove(object sender, MouseEventArgs e)
        {
            switch (mouseState)
            {
                case MouseState.Panning:
                    {
                        Point pos = e.GetPosition(Window.GetWindow(this));
                        Vector delta = pos - lastMousePos;
                        translateTransform.X += delta.X;
                        translateTransform.Y += delta.Y;
                        lastMousePos = pos;
                    }
                    break;
                case MouseState.DraggingNode:
                    {
                        if (draggedNode == null) break;
                        Point contentPos = ViewportToWorld(e.GetPosition(this));
                        Vector delta = contentPos - dragStartContent;
                        double currentLeft = Canvas.GetLeft(draggedNode);
                        double currentTop = Canvas.GetTop(draggedNode);
                        Canvas.SetLeft(draggedNode, currentLeft + delta.X);
                        Canvas.SetTop(draggedNode, currentTop + delta.Y);
                        dragStartContent = contentPos;
                        if (draggedNode is Node n)
                            n.RedrawEdges();
                        else if (draggedNode is StartNode s)
                        {
                            foreach (Edge edge in s.port.edges)
                                edge.ReDraw();
                        }
                        else if (draggedNode is EndNode en)
                        {
                            foreach (Edge edge in en.port.edges)
                                edge.ReDraw();
                        }
                    }
                    break;
                case MouseState.CreatingEdge:
                    {
                        Point viewportPos = e.GetPosition(this);
                        if (edgeStartPort == null)
                        {
                            // Defensive: if we somehow lost the start port, cancel creating edge
                            if (tempConnectionLine != null)
                            {
                                graph.Children.Remove(tempConnectionLine);
                                tempConnectionLine = null;
                            }
                            targetPort = null;
                            ReleaseMouseCapture();
                            mouseState = MouseState.None;
                            break;
                        }
                        // convert viewport mouse to content/canvas coords
                        Point mousePosContent = ViewportToCanvas(viewportPos);
                        // start position of socket in canvas coords
                        int startOffset = (int)edgeStartPort.socket.ActualWidth/2;
                        Point startPos = edgeStartPort.socket.TranslatePoint(new Point(startOffset, startOffset), graph);
                        tempConnectionLine.X1 = startPos.X;
                        tempConnectionLine.Y1 = startPos.Y;

                        // find snapping port (in content coords)
                        Point snapPos = mousePosContent;
                        targetPort = null;
                        double minDist = SNAP_DISTANCE;
                        foreach (Node node in graph.nodes)
                        {
                            foreach (Port p in node.ports)
                            {
                                // Only consider ports that are opposite direction, not the start port, and have the same CLR type
                                if (p != edgeStartPort && p.direction != edgeStartPort.direction &&
                                    p.portType != null && edgeStartPort.portType != null && p.portType == edgeStartPort.portType)
                                {
                                    int endOffset = (int)p.socket.ActualWidth / 2;
                                    Point portPos = p.socket.TranslatePoint(new Point(endOffset, endOffset), graph);
                                    double dist = (mousePosContent - portPos).Length;
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
                    break;
            }
        }

        private void GraphView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            switch (mouseState)
            {
                case MouseState.CreatingEdge:
                    {
                        if (targetPort != null)
                        {
                            // Only create the edge if the two ports have the same CLR type
                            if (edgeStartPort != null && edgeStartPort.portType != null && targetPort.portType != null && edgeStartPort.portType == targetPort.portType)
                            {
                                graph.CreateEdge(edgeStartPort, targetPort);
                            }
                        }
                        if (tempConnectionLine != null)
                        {
                            graph.Children.Remove(tempConnectionLine);
                            tempConnectionLine = null;
                        }
                        edgeStartPort = null;
                        targetPort = null;
                        ReleaseMouseCapture();
                        mouseState = MouseState.None;
                    }
                    break;
                case MouseState.DraggingNode:
                    {
                        draggedNode = null;
                        ReleaseMouseCapture();
                        mouseState = MouseState.None;
                    }
                    break;
                case MouseState.Panning:
                    {
                        ReleaseMouseCapture();
                        mouseState = MouseState.None;
                    }
                    break;
                default:
                    break;
            }
        }

        private void GraphView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Matrix m = transformGroup.Value;
            if (!m.HasInverse) return;
            Matrix inv = m; inv.Invert();
            Point contentPoint = inv.Transform(e.GetPosition(this));
            graph.rightClickPos = contentPoint;
        }
    }
}