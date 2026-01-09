using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class Node : Border
    {
        private const double NODE_WIDTH = 150;
        private const double NODE_HEIGHT = 100;
        private const int FIXED_ROW_HEIGHT = 20;
        private const int CORNER_RADIUS = 10;

        public Node()
        {
            Width = NODE_WIDTH;
            Height = NODE_HEIGHT;
            Background = Brushes.DarkGray;
            CornerRadius = new CornerRadius(CORNER_RADIUS);
            Child = CreateContent();
        }

        private Grid CreateContent()
        {
            Grid grid = new Grid();

            // Row definitions
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FIXED_ROW_HEIGHT) }); // Title
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Middle
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FIXED_ROW_HEIGHT) }); // Bottom

            // Title container
            Border titleBorder = new Border
            {
                Background = Brushes.DarkSlateGray,
                CornerRadius = new CornerRadius(CORNER_RADIUS, CORNER_RADIUS, 0, 0),
                Child = new TextBlock { Text = "Node Title", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
            };
            Grid.SetRow(titleBorder, 0);
            grid.Children.Add(titleBorder);

            // Middle container with left and right
            Grid middleGrid = new Grid();
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            middleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border inputBorder = new Border
            {
                Background = Brushes.Gray,
                Child = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new TextBlock { Text = "Inputs", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
                    }
                }
            };
            Grid.SetColumn(inputBorder, 0);
            middleGrid.Children.Add(inputBorder);

            Border outputBorder = new Border
            {
                Background = Brushes.Gray,
                Child = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new TextBlock { Text = "Outputs", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
                    }
                }
            };
            Grid.SetColumn(outputBorder, 1);
            middleGrid.Children.Add(outputBorder);

            Grid.SetRow(middleGrid, 1);
            grid.Children.Add(middleGrid);

            // Input port aligned to left
            Port inputPort = new Port { IsInput = true, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetRow(inputPort, 1);
            Grid.SetColumn(inputPort, 0);
            grid.Children.Add(inputPort);

            // Output port aligned to right
            Port outputPort = new Port { IsInput = false, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetRow(outputPort, 1);
            Grid.SetColumn(outputPort, 1);
            grid.Children.Add(outputPort);

            // Bottom container
            Border bottomBorder = new Border
            {
                Background = Brushes.DimGray,
                CornerRadius = new CornerRadius(0, 0, CORNER_RADIUS, CORNER_RADIUS),
                Child = new TextBlock { Text = "Bottom", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
            };
            Grid.SetRow(bottomBorder, 2);
            grid.Children.Add(bottomBorder);

            return grid;
        }

        public List<Port> GetPorts()
        {
            List<Port> ports = new List<Port>();
            FindPorts(this, ports);
            return ports;
        }

        private void FindPorts(UIElement element, List<Port> ports)
        {
            if (element is Port port)
            {
                ports.Add(port);
            }
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                FindPorts(VisualTreeHelper.GetChild(element, i) as UIElement, ports);
            }
        }
    }
}