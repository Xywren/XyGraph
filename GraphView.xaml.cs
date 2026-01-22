using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;
using System.Windows.Data;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Markup;
using System.Windows.Controls.Primitives;

namespace XyGraph
{
    public partial class GraphView : UserControl
    {
        // host-configurable preferred types to show in the type selector
        public List<Type> availableInputTypes { get; set; } = new List<Type> { typeof(object) };

        private bool sidebarSlidOff = false;
        private System.Windows.Media.Animation.DoubleAnimation slideAnimation;
        private const double WORLD_SIZE = 10000;
        private const int VISUAL_TREE_SEARCH_DEPTH = 10;
        private const string INPUT_PREVIEW_DRAG_FORMAT = "XyGraph.InputPreview";
        private Point previewDragStart;
        private bool previewMouseDown = false;
        private InputPreview draggingPreview = null;
        private AdornerLayer previewAdornerLayer = null;
        private InputPreviewAdorner previewAdorner = null;
        private RenderTargetBitmap previewAdornerBitmap = null;
        private bool previewDragOverGraph = false;
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
            graph.worldSize = WORLD_SIZE;
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
            // add input button
            AddInputButton.Click += AddInputButton_Click;
            // enable drag-drop from input previews into the graph
            graph.AllowDrop = true;
            graph.DragOver += Graph_DragOver;
            graph.DragLeave += Graph_DragLeave;
            graph.Drop += Graph_Drop;

            // log tunneling preview mouse down (captures handled events too)
            this.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler((object s, MouseButtonEventArgs e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"PreviewMouseLeftButtonDown: sender={s.GetType().Name}, orig={e.OriginalSource?.GetType().Name}, handled={e.Handled}, clickCount={e.ClickCount}");
                }), handledEventsToo: true);

            // log bubbling mouse down
            this.AddHandler(UIElement.MouseLeftButtonDownEvent,
                new MouseButtonEventHandler((object s, MouseButtonEventArgs e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"MouseLeftButtonDown: sender={s.GetType().Name}, orig={e.OriginalSource?.GetType().Name}, handled={e.Handled}, clickCount={e.ClickCount}");
                }), handledEventsToo: true);

        }

        private void AddInputButton_Click(object sender, RoutedEventArgs e)
        {
            InputPreview preview = CreateInputPreview("New Input", "");
            InputsPanel.Children.Add(preview);
            // focus the name textbox inside the preview
            preview.NameBox.Focus();
            preview.NameBox.SelectAll();
        }

        private InputPreview CreateInputPreview(string name, string typeName)
        {
            double cardWidth = SidebarContent.Width - 46.0;
            InputPreview preview = new InputPreview();
            preview.Width = cardWidth;
            preview.AvailableInputTypes = availableInputTypes;
            preview.NameBox.Text = name;
            preview.TypeCombo.Text = typeName;
            preview.TypeCombo.IsEditable = true;

            // attach drag handlers so the preview can be dragged onto the graph
            preview.PreviewMouseLeftButtonDown += (object s, System.Windows.Input.MouseButtonEventArgs e) =>
            {
                previewDragStart = e.GetPosition(this);
                previewMouseDown = true;
                draggingPreview = preview;
            };

            preview.PreviewMouseLeftButtonUp += (object s, System.Windows.Input.MouseButtonEventArgs e) =>
            {
                previewMouseDown = false;
                draggingPreview = null;
            };

            preview.PreviewMouseMove += (object s, System.Windows.Input.MouseEventArgs e) =>
            {
                if (!previewMouseDown) return;
                if (e.LeftButton != MouseButtonState.Pressed) return;
                if (draggingPreview != preview) return;

                Point current = e.GetPosition(this);
                Vector delta = current - previewDragStart;
                const double DRAG_THRESHOLD = 4.0;
                if (System.Math.Abs(delta.X) > DRAG_THRESHOLD || System.Math.Abs(delta.Y) > DRAG_THRESHOLD)
                {
                    // render a bitmap snapshot of the preview for the adorner
                    Size size = new Size(preview.ActualWidth, preview.ActualHeight);
                    if (size.Width <= 0 || size.Height <= 0) size = new Size(200, 60);
                    preview.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    preview.Arrange(new Rect(size));
                    RenderTargetBitmap bmp = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(preview);

                    // store adorner and layer so we can update/remove during drag
                    previewAdornerBitmap = bmp;
                    previewAdornerLayer = AdornerLayer.GetAdornerLayer(this.rootGrid);
                    if (previewAdornerLayer != null)
                    {
                        previewAdorner = new InputPreviewAdorner(this.rootGrid, bmp);
                        previewAdorner.SetOffset(new Point(-100,-100));
                        previewAdornerLayer.Add(previewAdorner);
                    }

                    System.Windows.DataObject data = new System.Windows.DataObject(INPUT_PREVIEW_DRAG_FORMAT, preview);
                    DragDrop.DoDragDrop(preview, data, DragDropEffects.Copy);
                    // ensure adorner is removed after drag completes
                    if (previewAdornerLayer != null && previewAdorner != null)
                    {
                        // only remove adorner if we didn't already remove it via DragLeave
                        previewAdornerLayer.Remove(previewAdorner);
                        previewAdorner = null;
                        previewAdornerLayer = null;
                        previewAdornerBitmap = null;
                        previewDragOverGraph = false;
                    }

                    previewMouseDown = false;
                    draggingPreview = null;
                }
            };
            return preview;
        }

        private void Graph_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(INPUT_PREVIEW_DRAG_FORMAT))
            {
                e.Effects = DragDropEffects.Copy;
                previewDragOverGraph = true;
                if (previewAdorner != null)
                {
                    // position the adorner at the current mouse position relative to rootGrid
                    Point p = e.GetPosition(this.rootGrid);
                    previewAdorner.SetOffset(p);
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                previewDragOverGraph = false;
            }
            e.Handled = true;
        }

        private void Graph_DragLeave(object sender, DragEventArgs e)
        {
            previewDragOverGraph = false;
        }

        private void Graph_Drop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(INPUT_PREVIEW_DRAG_FORMAT)) return;

            InputPreview preview = e.Data.GetData(INPUT_PREVIEW_DRAG_FORMAT) as InputPreview;

            // Only accept drops that actually entered the graph surface during the drag.
            // This prevents immediate drops at the drag source creating unintended nodes.
            if (!previewDragOverGraph)
            {
                e.Handled = true;
                return;
            }

            string inputName = "Input";
            Type resolvedType = typeof(object);
            if (preview != null)
            {
                inputName = preview.NameBox.Text ?? "Input";
                object tag = preview.TypeCombo.Tag;
                if (tag is Type t) resolvedType = t;
                else
                {
                    string typeText = preview.TypeCombo.Text ?? string.Empty;
                    Type maybe = ResolveTypeFromName(typeText);
                    if (maybe != null) resolvedType = maybe;
                }
            }

            // get drop point in canvas coordinates (graph is a Canvas)
            Point canvasPoint = e.GetPosition(graph);
            // Ignore drops that occur inside the sidebar area (likely the source) or outside graph bounds
            // if drop is inside the SidebarContainer, treat as no-op
            Point pSidebar = e.GetPosition(SidebarContainer);
            if (pSidebar.X >= 0 && pSidebar.X <= SidebarContainer.ActualWidth && pSidebar.Y >= 0 && pSidebar.Y <= SidebarContainer.ActualHeight)
            {
                e.Handled = true;
                return;
            }

            // ensure the drop point is within the graph visible area
            if (double.IsNaN(canvasPoint.X) || double.IsNaN(canvasPoint.Y) || canvasPoint.X < 0 || canvasPoint.Y < 0 || canvasPoint.X > graph.ActualWidth || canvasPoint.Y > graph.ActualHeight)
            {
                e.Handled = true;
                return;
            }
            InputNode inNode = new InputNode(graph, Guid.NewGuid(), inputName, resolvedType);
            AddNode(inNode, canvasPoint.X - inNode.SpawnOffsetX, canvasPoint.Y - inNode.SpawnOffsetY);

            e.Handled = true;
        }

        

        private Type ResolveTypeFromName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // check preferred list first
            foreach (Type t in availableInputTypes)
            {
                if (string.Equals(t.Name, input, StringComparison.OrdinalIgnoreCase) || string.Equals(t.FullName, input, StringComparison.OrdinalIgnoreCase))
                    return t;
            }

            // try Type.GetType (supports assembly-qualified names)
            try
            {
                Type byName = Type.GetType(input, false, true);
                if (byName != null) return byName;
            }
            catch { }

            // search loaded assemblies for simple name match
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (Type t in types)
                {
                    if (string.Equals(t.Name, input, StringComparison.OrdinalIgnoreCase)) return t;
                }
            }

            return null;
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