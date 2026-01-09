using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class GraphCanvas : Canvas
    {
        private Point _lastMousePos;
        private bool _isPanning;
        private Point _nodeCreatePos;
        private bool _isDraggingNode;
        private UIElement _draggedNode;
        private Point _dragStartPos;

        public GraphCanvas()
        {
            Background = Brushes.White;
            MouseWheel += OnMouseWheel;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseRightButtonDown += OnMouseRightButtonDown;
            ContextMenu = new ContextMenu();
            var createItem = new MenuItem { Header = "Create Node" };
            createItem.Click += (s, e) => AddNode();
            ContextMenu.Items.Add(createItem);
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _nodeCreatePos = e.GetPosition(this);
        }

        private void AddNode()
        {
            var node = new Border
            {
                Width = 100,
                Height = 50,
                Background = Brushes.LightBlue,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = "Node", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            Canvas.SetLeft(node, _nodeCreatePos.X - 50);
            Canvas.SetTop(node, _nodeCreatePos.Y - 25);
            Children.Add(node);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scale = e.Delta > 0 ? 1.1 : 0.9;
            var matrix = LayoutTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            matrix.ScaleX *= scale;
            matrix.ScaleY *= scale;
            LayoutTransform = matrix;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is Border && !_isDraggingNode)
            {
                _isDraggingNode = true;
                _draggedNode = e.Source as UIElement;
                _dragStartPos = e.GetPosition(this);
                CaptureMouse();
            }
            else if (e.Source == this && !_isDraggingNode)
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(Window.GetWindow(this));
                CaptureMouse();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingNode)
            {
                var pos = e.GetPosition(this);
                var delta = pos - _dragStartPos;
                Canvas.SetLeft(_draggedNode, Canvas.GetLeft(_draggedNode) + delta.X);
                Canvas.SetTop(_draggedNode, Canvas.GetTop(_draggedNode) + delta.Y);
                _dragStartPos = pos;
            }
            else if (_isPanning)
            {
                var pos = e.GetPosition(Window.GetWindow(this));
                var delta = pos - _lastMousePos;
                var transform = RenderTransform as TranslateTransform ?? new TranslateTransform();
                transform.X += delta.X;
                transform.Y += delta.Y;
                RenderTransform = transform;
                _lastMousePos = pos;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingNode)
            {
                _isDraggingNode = false;
                _draggedNode = null;
                ReleaseMouseCapture();
            }
            else if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var pen = new Pen(Brushes.LightGray, 1);
            for (double x = 0; x < ActualWidth; x += 20)
            {
                dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
            }
            for (double y = 0; y < ActualHeight; y += 20)
            {
                dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
            }
        }
    }
}