using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    // MultiPort: behaves like Port but uses a TextBox for the label so the user can rename ports
    public class MultiPort : Port
    {
        public MultiPort(string name, PortDirection direction, Type type, int socketSize = 10, Brush color = null, bool drawSocketOuterRing = true)
            : base(name, direction, type, socketSize, color, drawSocketOuterRing)
        {
            // Replace the existing label (TextBlock) inside the visual tree with a TextBox so the label is editable.
            if (this.Child is Grid grid)
            {
                StackPanel verticalStack = null;
                foreach (UIElement child in grid.Children)
                {
                    if (child is StackPanel sp)
                    {
                        verticalStack = sp;
                        break;
                    }
                }

                if (verticalStack != null && verticalStack.Children.Count > 0)
                {
                    UIElement horizElement = verticalStack.Children[0];
                    if (horizElement is StackPanel horiz)
                    {
                        int labelIndex = -1;
                        for (int i = 0; i < horiz.Children.Count; i++)
                        {
                            if (horiz.Children[i] is TextBlock)
                            {
                                labelIndex = i;
                                break;
                            }
                        }

                        if (labelIndex != -1)
                        {
                            TextBox textBox = new TextBox { Text = this.name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
                            textBox.TextChanged += (object sender, TextChangedEventArgs e) => { this.name = textBox.Text; };

                            if (this.direction == PortDirection.Input)
                            {
                                textBox.HorizontalAlignment = HorizontalAlignment.Left;
                                textBox.TextAlignment = TextAlignment.Left;
                            }
                            else
                            {
                                textBox.HorizontalAlignment = HorizontalAlignment.Right;
                                textBox.TextAlignment = TextAlignment.Right;
                            }

                            // remove existing TextBlock labels (there may be one) and extract the socket element
                            for (int j = horiz.Children.Count - 1; j >= 0; j--)
                            {
                                if (horiz.Children[j] is TextBlock)
                                {
                                    horiz.Children.RemoveAt(j);
                                }
                            }

                            UIElement socketElem = null;
                            for (int j = horiz.Children.Count - 1; j >= 0; j--)
                            {
                                if (horiz.Children[j] is Socket)
                                {
                                    socketElem = horiz.Children[j];
                                    horiz.Children.RemoveAt(j);
                                    break;
                                }
                            }

                            // create delete button
                            Button deleteButton = new Button { Content = "X", Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Left };
                            deleteButton.Click += (object sender, RoutedEventArgs e) => { this.Delete(); };

                            // Rebuild the horizontal stack to ensure correct ordering: [deleteButton] [textBox] [socket]
                            StackPanel newHoriz = new StackPanel { Orientation = horiz.Orientation, HorizontalAlignment = horiz.HorizontalAlignment, VerticalAlignment = horiz.VerticalAlignment };
                            newHoriz.Children.Add(deleteButton);
                            newHoriz.Children.Add(textBox);
                            if (socketElem != null)
                            {
                                newHoriz.Children.Add(socketElem);
                            }

                            // replace old horiz with newHoriz in the vertical stack
                            int horizIndex = verticalStack.Children.IndexOf(horiz);
                            if (horizIndex >= 0)
                            {
                                verticalStack.Children.RemoveAt(horizIndex);
                                verticalStack.Children.Insert(horizIndex, newHoriz);
                            }
                        }
                    }
                }
            }
        }
    }
}
