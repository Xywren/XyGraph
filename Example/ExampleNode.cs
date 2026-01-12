using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class ExampleNode : Node
    {
        public ExampleNode(Graph graph, Point rightClickPos) : base(graph)
        {
            double SpawnOffsetX = 75;
            double SpawnOffsetY = 50;
            Canvas.SetLeft(this, rightClickPos.X - SpawnOffsetX);
            Canvas.SetTop(this, rightClickPos.Y - SpawnOffsetY);

            
            topContainer.Add(new TextBlock { Text = "Top", Foreground = Brushes.White });
            Port inputPort = new Port("Input", NodeType.Input, this);

            // output
            Port outputPort = new Port("Output", NodeType.Output, this);
            inputContainer.Add(inputPort);
            Button addOutputButton = new Button { Content = "Add Output", FontSize = 8, Height = 20 };
            addOutputButton.Click += (s, e) => {
                Port newPort = new Port("New Output", NodeType.Output, this);
                outputContainer.Add(newPort);
            };

            outputContainer.Add(addOutputButton);
            outputContainer.Add(outputPort);

            mainContainer.Add(new TextBlock { Text = "Main", Foreground = Brushes.White });
            bottomContainer.Add(new TextBlock { Text = "Bottom", Foreground = Brushes.White });


            graph.nodes.Add(this);
            graph.Children.Add(this);
        }
    }
}