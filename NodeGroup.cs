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
        private const double GRIP_SIZE     = 14;
        private const double PADDING       = 24;
        private const double MIN_WIDTH     = 120;
        private const double MIN_HEIGHT    = TITLE_HEIGHT + 40;
        private const double NODE_GRID     = 10.0;

        private readonly Graph    graph;
        private readonly TextBox  nameBox;
        private readonly Border   titleBar;
        private readonly Rectangle resizeGrip;

        private Point  dragLastCanvasPos;
        private bool   isDraggingGroup;
        private bool   isResizing;
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

            resizeGrip = new Rectangle
            {
                Width               = GRIP_SIZE,
                Height              = GRIP_SIZE,
                Fill                = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Bottom,
                Cursor              = Cursors.SizeNWSE
            };
            Grid.SetRow(resizeGrip, 1);
            layout.Children.Add(resizeGrip);

            Child = layout;

            titleBar.PreviewMouseLeftButtonDown   += TitleBar_MouseDown;
            resizeGrip.PreviewMouseLeftButtonDown += Grip_MouseDown;
            PreviewMouseMove                      += NodeGroup_MouseMove;
            PreviewMouseLeftButtonUp              += NodeGroup_MouseUp;

            BuildContextMenu();
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

        private void Grip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isResizing        = true;
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
                Width  = Math.Max(MIN_WIDTH,  Width  + delta.X);
                Height = Math.Max(MIN_HEIGHT, Height + delta.Y);
                return;
            }

            Offset(this, delta);
            foreach (UIElement member in dragMembers)
            {
                Offset(member, delta);
                (member as Node)?.RedrawEdges();
            }
            e.Handled = true;
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
            }

            isDraggingGroup = false;
            isResizing      = false;
            dragMembers.Clear();
            ReleaseMouseCapture();
            e.Handled = true;
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
