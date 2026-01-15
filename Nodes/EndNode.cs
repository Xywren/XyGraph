using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json.Nodes;

namespace XyGraph
{
    public class EndNode : Node
    {
        public static readonly double OffsetX = 14;
        public static readonly double OffsetY = 14;

        public Port port { get; private set; }
        private Graph graph;

        public EndNode(Graph graph) : base(graph)
        {
            title = "END";
            this.graph = graph;


            //create port
            Port port = new Port("", PortType.Input, this, 20);
            port.isEditable = false;
            port.isRemovable = false;
            port.socket.Background = Brushes.Black;

            // create border around port
            Border outline = new Border
            {
                Width = 28,
                Height = 28,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12)
            };


            // Hide the title container
            port.HorizontalAlignment = HorizontalAlignment.Center;

            // Hide the title container
            titleContainer.Visibility = Visibility.Collapsed;

            // make backgrounds transparent
            mainContainer.Background = Brushes.Transparent;
            Background = Brushes.Transparent;
            innerBorder.Background = Brushes.Transparent;

            // add END text
            TextBlock tb = new TextBlock { Text = "END", HorizontalAlignment = HorizontalAlignment.Center };
            mainContainer.Add(tb);
            mainContainer.Add(port);

            // add right click delete context item
            this.port = port;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete End Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);
        }

        public override JsonObject Save()
        {
            var obj = base.Save();
            // Add any EndNode-specific data if needed
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            // we are completely overriding base node loading behaviour
            // EndNode has no ports to load except the single input port created in the constructor
            // base.Load(obj);

            if (obj == null) throw new ArgumentNullException(nameof(obj));

            guid = Guid.Parse(obj["id"]?.GetValue<string>() ?? guid.ToString());

            double x = obj["x"]?.GetValue<double>() ?? 0.0;
            Canvas.SetLeft(this, x);

            double y = obj["y"]?.GetValue<double>() ?? 0.0;
            Canvas.SetTop(this, y);


            // load ports that belong to this node
            JsonArray portsArray = obj["ports"] as JsonArray;
            if (portsArray != null)
            {
                foreach (JsonNode? item in portsArray)
                {
                    JsonObject portObj = portsArray[0] as JsonObject;
                    if (portObj == null) continue;
                    ports[0].guid = Guid.Parse(portObj["id"]?.GetValue<string>() ?? ports[0].guid.ToString());
                }
            }
        }

        public new void Delete()
        {
            base.Delete();
            graph.endNode = null;
            graph.endItem.IsEnabled = true;
        }


        // =======================================================================
        //                            Runtime behaviour
        // =======================================================================

        public override void Run()
        {
            // Endnode has no behaviour other than ending graph flow
            base.Run();
            Completed();
        }
        public override void Completed()
        {
            base.Completed();
            graph.Finished();
        }
        public override void Error()
        {
            // Your custom error behaviour here

            base.Error();
        }
    }
}