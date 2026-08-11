using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XyGraph
{
    /// <summary>
    /// A draggable container drawn behind the nodes. Membership is geometric — anything
    /// whose bounds sit inside the group's rectangle belongs to it, which is what makes
    /// groups nestable without tracking parentage.
    /// </summary>
    public class NodeGroup : Border
    {
        private const double TITLE_HEIGHT  = 24;
        private const double RESIZE_MARGIN = 8;
        private const double PADDING       = 24;
        private const double MIN_WIDTH     = 120;
        private const double MIN_HEIGHT    = TITLE_HEIGHT + 40;
        private const double NODE_GRID     = 10.0;

        [Flags]
        private enum Edge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

        private readonly Graph    graph;
        private readonly TextBox  nameBox;
        private readonly Border   titleBar;

        private Point  dragLastCanvasPos;
        private bool   isDraggingGroup;
        private bool   isResizing;
        private Edge   activeEdge;
        private List<UIElement> dragMembers = new List<UIElement>();

        public string name
        {
            get => nameBox.Text;
            set => nameBox.Text = value ?? string.Empty;
        }

        public NodeGroup(Graph graph)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));

            // no Background here: the interior tint is a non-hit-testable rectangle so
            // clicks inside the group reach the canvas and the nodes rather than the group
            BorderBrush     = Brushes.Gray;
            BorderThickness = new Thickness(1);
            CornerRadius    = new CornerRadius(4);
            Width           = 320;
            Height          = 220;

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TITLE_HEIGHT) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            nameBox = new TextBox
            {
                Text              = "Group",
                Background        = Brushes.Transparent,
                BorderThickness   = new Thickness(0),
                Foreground        = Brushes.Black,
                FontWeight        = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(6, 0, 6, 0)
            };

            Rectangle interiorTint = new Rectangle
            {
                Fill             = new SolidColorBrush(Color.FromArgb(30, 120, 120, 120)),
                IsHitTestVisible = false
            };
            Grid.SetRow(interiorTint, 1);
            layout.Children.Add(interiorTint);

            titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(170, 170, 170, 170)),
                Child      = nameBox,
                Cursor     = Cursors.SizeAll
            };
            Grid.SetRow(titleBar, 0);
            layout.Children.Add(titleBar);

            // A transparent hit-frame around the whole perimeter. Each zone carries its own
            // resize cursor, so hovering any edge or corner shows the right arrow with no
            // dedicated grip. The interior is left empty so clicks there pass to the canvas.
            Grid resizeFrame = new Grid { Background = null };
            Grid.SetRowSpan(resizeFrame, 2);
            AddResizeZone(resizeFrame, Edge.Top,                 HorizontalAlignment.Stretch, VerticalAlignment.Top,    Cursors.SizeNS,   new Thickness(RESIZE_MARGIN, 0, RESIZE_MARGIN, 0), RESIZE_MARGIN, double.NaN);
            AddResizeZone(resizeFrame, Edge.Bottom,              HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Cursors.SizeNS,   new Thickness(RESIZE_MARGIN, 0, RESIZE_MARGIN, 0), RESIZE_MARGIN, double.NaN);
            AddResizeZone(resizeFrame, Edge.Left,                HorizontalAlignment.Left,    VerticalAlignment.Stretch, Cursors.SizeWE,  new Thickness(0, RESIZE_MARGIN, 0, RESIZE_MARGIN), double.NaN, RESIZE_MARGIN);
            AddResizeZone(resizeFrame, Edge.Right,               HorizontalAlignment.Right,   VerticalAlignment.Stretch, Cursors.SizeWE,  new Thickness(0, RESIZE_MARGIN, 0, RESIZE_MARGIN), double.NaN, RESIZE_MARGIN);
            AddResizeZone(resizeFrame, Edge.Top | Edge.Left,     HorizontalAlignment.Left,    VerticalAlignment.Top,    Cursors.SizeNWSE, new Thickness(0), RESIZE_MARGIN, RESIZE_MARGIN);
            AddResizeZone(resizeFrame, Edge.Top | Edge.Right,    HorizontalAlignment.Right,   VerticalAlignment.Top,    Cursors.SizeNESW, new Thickness(0), RESIZE_MARGIN, RESIZE_MARGIN);
            AddResizeZone(resizeFrame, Edge.Bottom | Edge.Left,  HorizontalAlignment.Left,    VerticalAlignment.Bottom, Cursors.SizeNESW, new Thickness(0), RESIZE_MARGIN, RESIZE_MARGIN);
            AddResizeZone(resizeFrame, Edge.Bottom | Edge.Right, HorizontalAlignment.Right,   VerticalAlignment.Bottom, Cursors.SizeNWSE, new Thickness(0), RESIZE_MARGIN, RESIZE_MARGIN);
            layout.Children.Add(resizeFrame);

            Child = layout;

            titleBar.PreviewMouseLeftButtonDown   += TitleBar_MouseDown;
            PreviewMouseMove                      += NodeGroup_MouseMove;
            PreviewMouseLeftButtonUp              += NodeGroup_MouseUp;

            BuildContextMenu();
        }

        private void AddResizeZone(Grid host, Edge edge, HorizontalAlignment h, VerticalAlignment v,
                                   Cursor cursor, Thickness margin, double height, double width)
        {
            Rectangle zone = new Rectangle
            {
                Fill                = Brushes.Transparent,   // transparent but hit-testable
                HorizontalAlignment = h,
                VerticalAlignment   = v,
                Cursor              = cursor,
                Margin              = margin
            };
            if (!double.IsNaN(height)) zone.Height = height;
            if (!double.IsNaN(width))  zone.Width  = width;
            zone.PreviewMouseLeftButtonDown += (s, e) => Resize_MouseDown(edge, e);
            host.Children.Add(zone);
        }

        private void BuildContextMenu()
        {
            ContextMenu = new ContextMenu();

            MenuItem duplicate = new MenuItem { Header = "Duplicate Group" };
            duplicate.Click += (s, e) => graph.DuplicateGroup(this);

            MenuItem fit = new MenuItem { Header = "Fit To Contents" };
            fit.Click += (s, e) => EncloseNodes(ContainedNodes());

            MenuItem delete = new MenuItem { Header = "Delete Group (keep nodes)" };
            delete.Click += (s, e) => graph.DeleteGroup(this);

            ContextMenu.Items.Add(duplicate);
            ContextMenu.Items.Add(fit);
            ContextMenu.Items.Add(delete);
        }

        // ── Geometry ──────────────────────────────────────────────────────────

        public Rect Bounds
        {
            get
            {
                double left = Canvas.GetLeft(this);
                double top  = Canvas.GetTop(this);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top))  top  = 0;
                return new Rect(left, top, Width, Height);
            }
        }

        public void PlaceAt(Point canvasPosition)
        {
            Canvas.SetLeft(this, canvasPosition.X);
            Canvas.SetTop(this, canvasPosition.Y);
        }

        /// <summary>Sizes and positions the group so it surrounds the given nodes.</summary>
        public void EncloseNodes(IEnumerable<Node> targets)
        {
            Rect? union = null;
            foreach (Node n in targets)
            {
                Rect bounds = ElementBounds(n);
                union = union.HasValue ? Rect.Union(union.Value, bounds) : bounds;
            }

            if (!union.HasValue) return;

            Rect area = union.Value;
            Canvas.SetLeft(this, area.X - PADDING);
            Canvas.SetTop(this,  area.Y - PADDING - TITLE_HEIGHT);
            Width  = Math.Max(MIN_WIDTH,  area.Width  + PADDING * 2);
            Height = Math.Max(MIN_HEIGHT, area.Height + PADDING * 2 + TITLE_HEIGHT);
        }

        public List<Node> ContainedNodes()
        {
            Rect area = Bounds;
            List<Node> contained = new List<Node>();
            foreach (Node n in graph.nodes)
                if (area.Contains(ElementBounds(n))) contained.Add(n);
            return contained;
        }

        public List<NodeGroup> ContainedGroups()
        {
            Rect area = Bounds;
            List<NodeGroup> contained = new List<NodeGroup>();
            foreach (NodeGroup g in graph.groups)
                if (g != this && area.Contains(g.Bounds)) contained.Add(g);
            return contained;
        }

        internal static Rect ElementBounds(FrameworkElement element)
        {
            double left = Canvas.GetLeft(element);
            double top  = Canvas.GetTop(element);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top))  top  = 0;
            return new Rect(left, top, element.ActualWidth, element.ActualHeight);
        }

        // ── Dragging & resizing ───────────────────────────────────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // let the user click into the name field without starting a drag
            if (nameBox.IsKeyboardFocusWithin) return;

            isDraggingGroup   = true;
            dragLastCanvasPos = e.GetPosition(graph);

            dragMembers = new List<UIElement>();
            foreach (Node n in ContainedNodes())      dragMembers.Add(n);
            foreach (NodeGroup g in ContainedGroups()) dragMembers.Add(g);

            CaptureMouse();
            e.Handled = true;
        }

        private void Resize_MouseDown(Edge edge, MouseButtonEventArgs e)
        {
            isResizing        = true;
            activeEdge        = edge;
            dragLastCanvasPos = e.GetPosition(graph);
            CaptureMouse();
            e.Handled = true;
        }

        private void NodeGroup_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDraggingGroup && !isResizing) return;

            Point current = e.GetPosition(graph);
            Vector delta  = current - dragLastCanvasPos;
            dragLastCanvasPos = current;

            if (isResizing)
            {
                ResizeBy(delta);
                return;
            }

            Offset(this, delta);
            foreach (UIElement member in dragMembers)
                Offset(member, delta);

            RedrawMemberEdges();
            e.Handled = true;
        }

        /// <summary>
        /// Resizes from whichever edges are active. Dragging a left/top edge moves the
        /// group's origin as well as its size, so the opposite edge stays put — like a
        /// normal window. Size is clamped to the minimum without dragging the origin past it.
        /// </summary>
        private void ResizeBy(Vector delta)
        {
            double left = Canvas.GetLeft(this); if (double.IsNaN(left)) left = 0;
            double top  = Canvas.GetTop(this);  if (double.IsNaN(top))  top  = 0;
            double w = Width, h = Height;

            if (activeEdge.HasFlag(Edge.Right))  w = Math.Max(MIN_WIDTH, w + delta.X);
            if (activeEdge.HasFlag(Edge.Bottom)) h = Math.Max(MIN_HEIGHT, h + delta.Y);
            if (activeEdge.HasFlag(Edge.Left))
            {
                double nw = Math.Max(MIN_WIDTH, w - delta.X);
                left += w - nw;   // move origin only by the amount width actually changed
                w = nw;
            }
            if (activeEdge.HasFlag(Edge.Top))
            {
                double nh = Math.Max(MIN_HEIGHT, h - delta.Y);
                top += h - nh;
                h = nh;
            }

            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, top);
            Width  = w;
            Height = h;
        }

        private void NodeGroup_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDraggingGroup && !isResizing) return;

            if (isDraggingGroup)
            {
                SnapToGrid(this);
                foreach (UIElement member in dragMembers)
                {
                    SnapToGrid(member);
                    (member as Node)?.OnNodeMoved();
                }

                // Settle the edges against the snapped positions
                RedrawMemberEdges();
            }

            isDraggingGroup = false;
            isResizing      = false;
            activeEdge      = Edge.None;
            dragMembers.Clear();
            ReleaseMouseCapture();
            e.Handled = true;
        }

        /// <summary>
        /// Redraws the edges of every node travelling with this group. Nodes inside nested
        /// groups are already in dragMembers in their own right, since membership is worked
        /// out from bounds.
        /// </summary>
        private void RedrawMemberEdges()
        {
            graph.RedrawEdgesAfterMove(dragMembers.OfType<Node>());
        }

        private static void Offset(UIElement element, Vector delta)
        {
            double left = Canvas.GetLeft(element);
            double top  = Canvas.GetTop(element);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top))  top  = 0;
            Canvas.SetLeft(element, left + delta.X);
            Canvas.SetTop(element,  top  + delta.Y);
        }

        private static void SnapToGrid(UIElement element)
        {
            double left = Canvas.GetLeft(element);
            double top  = Canvas.GetTop(element);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top))  top  = 0;
            Canvas.SetLeft(element, Math.Round(left / NODE_GRID) * NODE_GRID);
            Canvas.SetTop(element,  Math.Round(top  / NODE_GRID) * NODE_GRID);
        }

        // ── Serialisation ─────────────────────────────────────────────────────

        public JsonObject Save()
        {
            Rect area = Bounds;
            return new JsonObject
            {
                ["name"]   = name,
                ["x"]      = area.X,
                ["y"]      = area.Y,
                ["width"]  = Width,
                ["height"] = Height
            };
        }

        public void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            name   = obj["name"]?.GetValue<string>() ?? "Group";
            Width  = obj["width"]?.GetValue<double>()  ?? Width;
            Height = obj["height"]?.GetValue<double>() ?? Height;
            PlaceAt(new Point(obj["x"]?.GetValue<double>() ?? 0, obj["y"]?.GetValue<double>() ?? 0));
        }
    }
}
