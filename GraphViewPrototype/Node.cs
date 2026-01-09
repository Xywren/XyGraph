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
                Child = new TextBlock { Text = "Inputs", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
            };
            Grid.SetColumn(inputBorder, 0);
            middleGrid.Children.Add(inputBorder);

            Border outputBorder = new Border
            {
                Background = Brushes.Gray,
                Child = new TextBlock { Text = "Outputs", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White }
            };
            Grid.SetColumn(outputBorder, 1);
            middleGrid.Children.Add(outputBorder);

            Grid.SetRow(middleGrid, 1);
            grid.Children.Add(middleGrid);

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
    }
}