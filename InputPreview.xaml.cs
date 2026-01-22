using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public partial class InputPreview : UserControl
    {
        public List<Type> AvailableInputTypes { get; set; } = new List<Type> { typeof(object) };

        public InputPreview()
        {
            InitializeComponent();
            Loaded += InputPreview_Loaded;

            // wire up default behaviors
            NameBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitPreview(NameBox); };
            NameBox.LostFocus += (s, e) => CommitPreview(NameBox);

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
            };

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
        }


        private void CommitPreview(TextBox tb)
        {
            if (tb == null) return;
            tb.IsReadOnly = true;
            tb.Background = Brushes.Transparent;
        }

        private void CommitPreview(ComboBox combo)
        {
            if (combo == null) return;
            string text = (combo.Text ?? string.Empty).Trim();
            Type resolved = ResolveTypeFromName(text) ?? typeof(object);
            combo.Tag = resolved;
            combo.Text = resolved.Name;
            combo.IsEditable = false;
            combo.ToolTip = resolved.FullName;
        }

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
    }
}
