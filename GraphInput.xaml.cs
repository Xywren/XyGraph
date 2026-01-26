using System;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace XyGraph
{
    public partial class GraphInput : UserControl
    {
        private Graph graph;
        public Guid InputId { get; private set; } = Guid.NewGuid();
        public event Action<GraphInput> GraphInputChanged;

        public List<Type> AvailableInputTypes { get; set; } = new List<Type> { typeof(object) };

        // now requires the owning Graph so deletion can operate without external parameter
        public GraphInput(Graph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            this.graph = graph;
            InitializeComponent();
            Loaded += InputPreview_Loaded;

            // wire up default behaviors
            // update live on changes to name or type
            NameBox.TextChanged += (s, e) => { GraphInputChanged?.Invoke(this); };

            TypeCombo.SelectionChanged += (s, e) =>
            {
                // Determine the resolved Type for the selection (prefer SelectedItem when it's a Type)
                Type resolved = null;
                if (TypeCombo.SelectedItem is Type selType) resolved = selType;
                else
                {
                    string typeTextLocal = (TypeCombo.Text ?? string.Empty).Trim();
                    resolved = ResolveTypeFromName(typeTextLocal) ?? typeof(object);
                }

                // store resolved type for consumers
                TypeCombo.Tag = resolved;

                // compute colour using the same key InputNode uses (resolved.ToString())
                string hex = Common.HashColour(resolved.ToString());
                BrushConverter conv = new BrushConverter();
                Brush brush = (Brush)conv.ConvertFromString(hex);
                if (brush != null)
                {
                    SocketInner.Background = brush;
                    SocketOuter.BorderBrush = brush;
                }
                // notify listeners on each change
                GraphInputChanged?.Invoke(this);
            };

            ContextMenu cm = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Input" };
            deleteItem.Click += (s, e) =>
            {
                Delete();
            };
            cm.Items.Add(deleteItem);
            this.ContextMenu = cm;

            // ensure right-clicks on any child control show this control's context menu
            this.AddHandler(UIElement.PreviewMouseRightButtonDownEvent,
                new MouseButtonEventHandler((object s, MouseButtonEventArgs e) =>
                {
                    if (this.ContextMenu != null)
                    {
                        this.ContextMenu.PlacementTarget = this;
                        this.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                        this.ContextMenu.IsOpen = true;
                        e.Handled = true;
                    }
                }), handledEventsToo: true);

        }

        private void InputPreview_Loaded(object sender, RoutedEventArgs e)
        {
            TypeCombo.ItemsSource = AvailableInputTypes;
            TypeCombo.DisplayMemberPath = "Name";
            // set default socket colours based on current selected type (prefer SelectedItem)
            Type resolvedOnLoad = null;
            if (TypeCombo.SelectedItem is Type selOnLoad) resolvedOnLoad = selOnLoad;
            else
            {
                string typeTextLoad = (TypeCombo.Text ?? string.Empty).Trim();
                resolvedOnLoad = ResolveTypeFromName(typeTextLoad) ?? typeof(object);
            }
            TypeCombo.Tag = resolvedOnLoad;
            string hex2 = Common.HashColour(resolvedOnLoad.ToString());
            BrushConverter conv2 = new BrushConverter();
            Brush brush2 = (Brush)conv2.ConvertFromString(hex2);
            if (brush2 != null)
            {
                SocketInner.Background = brush2;
                SocketOuter.BorderBrush = brush2;
            }
            // notify listeners of initial state
            GraphInputChanged?.Invoke(this);
        }


        // =======================================================================
        //                            Serialization
        // =======================================================================

        public JsonObject Save()
        {
            JsonObject obj = new JsonObject();
            obj["id"] = InputId.ToString();
            obj["name"] = NameBox.Text ?? string.Empty;
            // prefer resolved Type in Tag, otherwise persist the text
            if (TypeCombo.Tag is Type t)
                obj["type"] = t.AssemblyQualifiedName ?? string.Empty;
            else
                obj["type"] = TypeCombo.Text ?? string.Empty;
            return obj;
        }

        public void Load(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            string idStr = obj["id"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrEmpty(idStr) && Guid.TryParse(idStr, out Guid parsed))
            {
                InputId = parsed;
            }
            NameBox.Text = obj["name"]?.GetValue<string>() ?? string.Empty;
            string typeName = obj["type"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrEmpty(typeName))
            {
                Type resolved = ResolveTypeFromName(typeName) ?? typeof(object);
                TypeCombo.Tag = resolved;
                TypeCombo.Text = resolved.Name;
            }
            else
            {
                TypeCombo.Tag = typeof(object);
                TypeCombo.Text = typeof(object).Name;
            }
            GraphInputChanged?.Invoke(this);
        }


        // CommitPreview methods removed: updates fire live via events instead of manual commit

        private Type ResolveTypeFromName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;


            foreach (Type t in AvailableInputTypes)
            {
                if (string.Equals(t.Name, input, StringComparison.OrdinalIgnoreCase) || string.Equals(t.FullName, input, StringComparison.OrdinalIgnoreCase))
                    return t;
            }

            try
            {
                Type byName = Type.GetType(input, false, true);
                if (byName != null) return byName;
            }
            catch { }

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

        // Remove this GraphInput and any InputNode instances that reference it from the given graph
        public void Delete()
        {
            if (graph == null) throw new InvalidOperationException("Owner graph not set for GraphInput.");

            // delete any InputNode instances that reference this input id
            List<Node> nodesToRemove = new List<Node>();
            foreach (Node n in graph.nodes)
            {
                if (n is InputNode inNode && inNode.inputId == this.InputId)
                {
                    nodesToRemove.Add(n);
                }
            }
            foreach (Node n in nodesToRemove)
            {
                n.Delete();
            }

            graph.inputs.Remove(this);

            if (this.Parent is ItemsControl ic)
            {
                ic.Items.Remove(this);
            }
        }
    }
}
