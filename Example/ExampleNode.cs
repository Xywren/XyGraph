using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XyGraph
{
    public class ExampleNode : Node
    {
        [NodeInput(Color = "#FF00FF")]
        public Node input;
        [NodeOutput]
        public Node output;

        private TextBox exampleProperty;



        public ExampleNode(Graph graph) : base(graph)
        {
            //overriden properties
            title = "Example Node";
            SpawnOffsetX = 75;
            SpawnOffsetY = 50;


            // build your node UI here:

            exampleProperty = new TextBox { Text = title, Margin = new Thickness(2), MinWidth = 120 };
            topContainer.Add(exampleProperty);


            topContainer.Add(new TextBlock { Text = "Top", Foreground = Brushes.White });

            mainContainer.Add(new TextBlock { Text = "Main", Foreground = Brushes.White });
            bottomContainer.Add(new TextBlock { Text = "Bottom", Foreground = Brushes.White });


        }


        // =======================================================================
        //                            Serialization
        // =======================================================================

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

        // =======================================================================
        //                            Runtime behaviour
        // =======================================================================

        public override void Run()
        {
            base.Run();
            // === Your custom runtime behaviour here ===
        }
        public override void Completed()
        {
            base.Completed();
            // === Your custom completion behaviour here ===


            // once completed run the output
            output.Run();
        }
        public override void Error()
        {
            // === Your custom error behaviour here ===

            base.Error();
        }
    }
}