using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class ExampleNode : Node
    {

        private TextBox exampleProperty;



        public ExampleNode(Graph graph) : base(graph)
        {
            //overriden properties
            title = "Example Node";
            SpawnOffsetX = 75;
            SpawnOffsetY = 50;



            exampleProperty = new TextBox { Text = title, Margin = new Thickness(2), MinWidth = 120 };
            topContainer.Add(exampleProperty);


            topContainer.Add(new TextBlock { Text = "Top", Foreground = Brushes.White });
            Port inputPort = new Port("Input", PortType.Input, this);

            // output
            Port outputPort = new Port("Output", PortType.Output, this);
            inputContainer.Add(inputPort);
            Button addOutputButton = new Button { Content = "Add Output", FontSize = 8, Height = 20 };
            addOutputButton.Click += (s, e) =>
            {
                Port newPort = new Port("New Output", PortType.Output, this);
                outputContainer.Add(newPort);
            };

            outputContainer.Add(addOutputButton);
            outputContainer.Add(outputPort);

            mainContainer.Add(new TextBlock { Text = "Main", Foreground = Brushes.White });
            bottomContainer.Add(new TextBlock { Text = "Bottom", Foreground = Brushes.White });


        }


        public override JsonObject Save()
        {
            // run the default Node Save function first
            JsonObject obj = base.Save();

            // Save your custom properties in the node.
            obj["ExampleProperty"] = exampleProperty.Text;

            return obj;
        }

        public override void Load(JsonObject obj)
        {
            // run the default Node Load function first
            base.Load(obj);

            // Load your custom properties.
            string exampleText = obj["ExampleProperty"]?.GetValue<string>() ?? string.Empty;
            exampleProperty.Text = exampleText;
        }
    }
}