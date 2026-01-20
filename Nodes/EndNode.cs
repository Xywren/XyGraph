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
        [NodeInput(Color = "Black", SocketSize = 20, DrawOuterRing = true, Name = "")]
        public Node END;

        public Port port { get; private set; }
        private Graph graph;

        public EndNode(Graph graph) : base(graph)
        {
            title = "END";
            this.graph = graph;



            // Hide the title container
            titleContainer.Visibility = Visibility.Collapsed;

            // make backgrounds transparent
            mainContainer.Background = Brushes.Transparent;
            inputContainer.Background = Brushes.Transparent;
            Background = Brushes.Transparent;
            innerBorder.Background = Brushes.Transparent;

            // add END text
            TextBlock tb = new TextBlock { Text = "END", HorizontalAlignment = HorizontalAlignment.Center };
            inputContainer.Add(tb);

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