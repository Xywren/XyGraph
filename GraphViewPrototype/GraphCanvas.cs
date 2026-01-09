using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class GraphCanvas : Canvas
    {
        private Point lastMousePos;
        private bool isPanning;
        private Point nodeCreatePos;
        private bool isDraggingNode;
        private UIElement draggedNode;
        private Point dragStartPos;

        private const double ZOOM_FACTOR = 1.1;
        private const double ZOOM_REDUCE = 0.9;
        private const int GRID_SIZE = 20;
        private const double NODE_OFFSET_X = 75;
        private const double NODE_OFFSET_Y = 50;

        public GraphCanvas()
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
            Canvas.SetLeft(node, nodeCreatePos.X - NODE_OFFSET_X);
            Canvas.SetTop(node, nodeCreatePos.Y - NODE_OFFSET_Y);
            Children.Add(node);
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
            if (e.Source != this && !isDraggingNode)
            {
                UIElement node = GetNodeFromSource(e.Source);
                if (node != null)
                {
                    isDraggingNode = true;
                    draggedNode = node;
                    dragStartPos = e.GetPosition(this);
                    CaptureMouse();
                }
            }
            else if (e.Source == this && !isDraggingNode)
            {
                isPanning = true;
                lastMousePos = e.GetPosition(Window.GetWindow(this));
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingNode)
            {
                Point pos = e.GetPosition(this);
                Vector delta = pos - dragStartPos;
                Canvas.SetLeft(draggedNode, Canvas.GetLeft(draggedNode) + delta.X);
                Canvas.SetTop(draggedNode, Canvas.GetTop(draggedNode) + delta.Y);
                dragStartPos = pos;
            }
            else if (isPanning)
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
            if (isDraggingNode)
            {
                isDraggingNode = false;
                draggedNode = null;
                ReleaseMouseCapture();
            }
            else if (isPanning)
            {
                isPanning = false;
                ReleaseMouseCapture();
            }
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