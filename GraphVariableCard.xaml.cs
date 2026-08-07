using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XyGraph
{
    public partial class GraphVariableCard : UserControl
    {
        private Graph graph;

        public GraphVariable Variable { get; private set; } = new GraphVariable();
        public List<Type> AvailableInputTypes { get; set; } = new List<Type> { typeof(object) };

        public event Action<GraphVariableCard> GraphVariableChanged;

        // Fired when the user requests a Set node be placed on the canvas
        public event Action PlaceSetNodeRequested;
        public event Action PlaceGetNodeRequested;

        public string VariableName  => NameBox.Text ?? string.Empty;
        public Type   ResolvedType
        {
            get
            {
                if (TypeCombo.Tag is Type t) return t;
                return ResolveTypeFromName(TypeCombo.Text ?? string.Empty) ?? typeof(object);
            }
        }

        // set while the UI is being populated from Variable, so the change handlers do not
        // write a half-initialised UI state back over the definition
        private bool isBinding;

        public GraphVariableCard(Graph graph, GraphVariable variable = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            this.graph = graph;
            if (variable != null) Variable = variable;

            InitializeComponent();
            Loaded += Card_Loaded;

            NameBox.TextChanged += (s, e) =>
            {
                if (isBinding) return;
                Variable.Name = NameBox.Text ?? string.Empty;
                GraphVariableChanged?.Invoke(this);
            };

            TypeCombo.SelectionChanged += (s, e) => OnTypeChanged();
            TypeCombo.LostFocus        += (s, e) => OnTypeChanged();

            // Context menu: Delete / Place Get / Place Set
            ContextMenu cm = new ContextMenu();

            MenuItem getItem = new MenuItem { Header = "Place Get Node" };
            getItem.Click += (s, e) => PlaceGetNodeRequested?.Invoke();
            cm.Items.Add(getItem);

            MenuItem setItem = new MenuItem { Header = "Place Set Node" };
            setItem.Click += (s, e) => PlaceSetNodeRequested?.Invoke();
            cm.Items.Add(setItem);

            cm.Items.Add(new Separator());

            MenuItem delItem = new MenuItem { Header = "Delete Variable" };
            delItem.Click += (s, e) => Delete();
            cm.Items.Add(delItem);

            this.ContextMenu = cm;
            this.AddHandler(UIElement.PreviewMouseRightButtonDownEvent,
                new MouseButtonEventHandler((object s, MouseButtonEventArgs e) =>
                {
                    if (this.ContextMenu != null)
                    {
                        this.ContextMenu.PlacementTarget = this;
                        this.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                        this.ContextMenu.IsOpen    = true;
                        e.Handled = true;
                    }
                }), handledEventsToo: true);

            // GET chip drag start
            GetChip.MouseLeftButtonDown += (s, e) =>
            {
                DragChip(isDragSet: false);
                e.Handled = true;
            };
            // SET chip drag start
            SetChip.MouseLeftButtonDown += (s, e) =>
            {
                DragChip(isDragSet: true);
                e.Handled = true;
            };
        }

        private void DragChip(bool isDragSet)
        {
            string format = isDragSet ? GraphVariableCard.SET_DRAG_FORMAT : GraphVariableCard.GET_DRAG_FORMAT;
            DataObject data = new DataObject(format, this);
            DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
        }

        public static readonly string GET_DRAG_FORMAT = "XyGraph.GraphVariable.Get";
        public static readonly string SET_DRAG_FORMAT  = "XyGraph.GraphVariable.Set";

        private void Card_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshFromVariable();
        }

        /// <summary>
        /// Pushes the definition into the UI. Nothing is written back to Variable here —
        /// binding must never be able to overwrite a loaded definition with a default.
        /// </summary>
        public void RefreshFromVariable()
        {
            isBinding = true;
            try
            {
                Type resolved = Variable.ResolvedType;

                // a loaded graph can reference a type the host has not listed; show it anyway
                if (!AvailableInputTypes.Contains(resolved)) AvailableInputTypes.Add(resolved);

                TypeCombo.ItemsSource       = null;
                TypeCombo.ItemsSource       = AvailableInputTypes;
                TypeCombo.DisplayMemberPath = "Name";
                TypeCombo.SelectedItem      = resolved;
                TypeCombo.Tag               = resolved;

                NameBox.Text = Variable.Name;

                ApplyTypeColour(resolved);
            }
            finally { isBinding = false; }
        }

        private void OnTypeChanged()
        {
            if (isBinding) return;

            Type resolved;
            if (TypeCombo.SelectedItem is Type sel) resolved = sel;
            else resolved = ResolveTypeFromName(TypeCombo.Text ?? string.Empty) ?? Variable.ResolvedType;

            TypeCombo.Tag         = resolved;
            Variable.ResolvedType = resolved;

            ApplyTypeColour(resolved);

            GraphVariableChanged?.Invoke(this);
        }

        private void ApplyTypeColour(Type resolved)
        {
            string hex = Node.DerivePortColour(resolved ?? typeof(object));
            Brush brush = (Brush)new BrushConverter().ConvertFromString(hex);
            if (brush == null) return;

            SocketInner.Background  = brush;
            SocketOuter.BorderBrush = brush;
        }

        // ── Serialisation ─────────────────────────────────────────────────────────

        public JsonObject Save()
        {
            Variable.Name = NameBox.Text ?? string.Empty;
            if (TypeCombo.Tag is Type t) Variable.ResolvedType = t;
            return Variable.Save();
        }

        public void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            Variable = GraphVariable.Load(obj);
            RefreshFromVariable();
            GraphVariableChanged?.Invoke(this);
        }

        // ── Delete ────────────────────────────────────────────────────────────────

        public void Delete()
        {
            // Remove all Get/Set nodes referencing this variable
            List<Node> toRemove = new List<Node>();
            foreach (Node n in graph.nodes)
            {
                if (n is GetVariableNode g && g.variableId == Variable.Id) toRemove.Add(n);
                if (n is SetVariableNode s && s.variableId == Variable.Id) toRemove.Add(n);
            }
            foreach (Node n in toRemove) n.Delete();

            graph.variableDefinitions.Remove(Variable);

            if (this.Parent is ItemsControl ic) ic.Items.Remove(this);
        }

        // ── Type resolution ───────────────────────────────────────────────────────

        private Type ResolveTypeFromName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            foreach (Type t in AvailableInputTypes)
                if (string.Equals(t.Name, input, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.FullName, input, StringComparison.OrdinalIgnoreCase))
                    return t;

            try { Type byName = Type.GetType(input, false, true); if (byName != null) return byName; } catch { }

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (Type t in types)
                    if (string.Equals(t.Name, input, StringComparison.OrdinalIgnoreCase)) return t;
            }

            return null;
        }
    }
}
