using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GraphViewPrototype
{
    public class Node : Border
    {
        public const double MIN_NODE_WIDTH = 150;
        public const double MIN_NODE_HEIGHT = 100;
        private const int CORNER_RADIUS = 10;
        private Grid grid;
        public List<Port> Ports = new List<Port>();

        public NodeContainer TitleContainer { get; private set; }
        public NodeContainer InputContainer { get; private set; }
        public NodeContainer OutputContainer { get; private set; }
        public NodeContainer TopContainer { get; private set; }
        public NodeContainer MainContainer { get; private set; }
        public NodeContainer BottomContainer { get; private set; }

        public Node()
        {
            //MinWidth = MIN_NODE_WIDTH;
            //MinHeight = MIN_NODE_HEIGHT;
            Background = Brushes.DarkGray;
            CornerRadius = new CornerRadius(CORNER_RADIUS);
            grid = CreateContent();
            Child = grid;
        }

        private Grid CreateContent()
        {
            Grid grid = new Grid();

            // Column definitions
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Row definitions
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Middle
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom

            // Title container
            TitleContainer = new NodeContainer(this, Brushes.DarkSlateGray);
            Grid.SetRow(TitleContainer, 0);
            Grid.SetColumn(TitleContainer, 0);
            Grid.SetColumnSpan(TitleContainer, 3);
            grid.Children.Add(TitleContainer);
            TitleContainer.CornerRadius = new CornerRadius(CORNER_RADIUS, CORNER_RADIUS, 0, 0);

            // Top container
            TopContainer = new NodeContainer(this, Brushes.LightGray);
            Grid.SetRow(TopContainer, 1);
            Grid.SetColumn(TopContainer, 0);
            Grid.SetColumnSpan(TopContainer, 3);
            grid.Children.Add(TopContainer);

            // Middle row containers
            InputContainer = new NodeContainer(this, Brushes.Gray, Orientation.Vertical, HorizontalAlignment.Left);
            Grid.SetRow(InputContainer, 2);
            Grid.SetColumn(InputContainer, 0);
            grid.Children.Add(InputContainer);

            MainContainer = new NodeContainer(this, Brushes.LightBlue);
            Grid.SetRow(MainContainer, 2);
            Grid.SetColumn(MainContainer, 1);
            grid.Children.Add(MainContainer);

            OutputContainer = new NodeContainer(this, Brushes.Gray, Orientation.Vertical, HorizontalAlignment.Right);
            Grid.SetRow(OutputContainer, 2);
            Grid.SetColumn(OutputContainer, 2);
            grid.Children.Add(OutputContainer);

            // Bottom container
            BottomContainer = new NodeContainer(this, Brushes.DimGray);
            Grid.SetRow(BottomContainer, 3);
            Grid.SetColumn(BottomContainer, 0);
            Grid.SetColumnSpan(BottomContainer, 3);
            grid.Children.Add(BottomContainer);
            BottomContainer.CornerRadius = new CornerRadius(0, 0, CORNER_RADIUS, CORNER_RADIUS);

            return grid;
        }
    }
}