using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.Json.Nodes;

namespace XyGraph
{
    public class StartNode : Node
    {
        public static readonly double OffsetX = 10;
        public static readonly double OffsetY = 10;
        [NodeOutput(SocketSize = 20, DrawOuterRing = false, Name = "")]
        public Node outputNode;

        public Port port { get; private set; }
        private Graph graph;

        public StartNode(Graph graph) : base(graph)
        {
            title = "START";
            this.graph = graph;

            // the port will be created by the base via attributes; find it and move it into the main container

            // Hide the title container
            titleContainer.Visibility = Visibility.Collapsed;

            // Make backgrounds transparent
            mainContainer.Background = Brushes.Transparent;
            outputContainer.Background = Brushes.Transparent;
            Background = Brushes.Transparent; // Remove pink background
            innerBorder.Background = Brushes.Transparent;

            // add START text
            TextBlock tb = new TextBlock { Text = "START", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            outputContainer.Add(tb);

            // add right click delete context item
            this.port = port;
            ContextMenu = new ContextMenu();
            MenuItem deleteItem = new MenuItem { Header = "Delete Start Node" };
            deleteItem.Click += (s, e) => this.Delete();
            ContextMenu.Items.Add(deleteItem);
        }


        public override JsonObject Save()
        {
            var obj = base.Save();
            // Add any StartNode-specific data if needed
            return obj;
        }

        public override void Load(JsonObject obj)
        {
            // we are completely overriding base node loading behaviour
            // StartNode has no ports to load except the single input port created in the constructor
            // base.Load(obj);

            if (obj == null) throw new ArgumentNullException(nameof(obj));

            guid = Guid.Parse(obj["id"]?.GetValue<string>() ?? guid.ToString());


            // loaded coordinates are centered world coordinates; convert to canvas coords
            double centeredX = obj["x"]?.GetValue<double>() ?? 0.0;
            double centeredY = obj["y"]?.GetValue<double>() ?? 0.0;
            Point point = new Point(centeredX, centeredY);
            point = ConvertWorldSpace(point);
            Canvas.SetLeft(this, point.X);
            Canvas.SetTop(this, point.Y);


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
            graph.startNode = null;
            graph.startItem.IsEnabled = true;
        }

        // =======================================================================
        //                            Runtime behaviour
        // =======================================================================

        public override void Run()
        {
            // StartNode has no behaviour other than passing control to the next node
            Completed();
        }
        public override void Completed()
        {
            base.Completed();
            outputNode.Run();
        }
        public override void Error()
        {
            // Your custom error behaviour here

            base.Error();
        }
    }
}