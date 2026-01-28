using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XyGraph;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

namespace Kraken.GraphSystem;



/*   This class is an example of a "data node"
 *   This Node is never "executed" - it never calls Run(), Completed(), or Error()
 *   instead this node is a data container. it holds data that is passed to downstream nodes via the [NodeOuput]
 *   
 *   When the output is connected to other nodes, the downstream node will call Evaluate()
 *   
 *   you should make sure that Evaluate() sets the value of all node outputs
 * 
 */
public class ExampleDataNode : Node
{
    [NodeOutput] public string output;

    private TextBox valueBox;

    public ExampleDataNode(Graph graph) : base(graph)
    {
        title = "String";

        // custom node UI  built here
        valueBox = new TextBox
        {
            Text = "",
            Margin = new Thickness(2),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinWidth = 120,
            MaxWidth = 240,
            MaxHeight = 300,
            VerticalAlignment = VerticalAlignment.Top
        };
        topContainer.Add(valueBox);

        // hide input and main containers for this simple value node
        inputContainer.Visibility = Visibility.Visible;
        mainContainer.Visibility = Visibility.Collapsed;

        string hex = Common.HashColour(typeof(string).ToString());
        BrushConverter conv = new BrushConverter();
        titleContainer.Background = (Brush)conv.ConvertFromString(hex);
    }



    // =======================================================================
    //                            Serialization
    // =======================================================================
    public override JsonObject Save()
    {
        JsonObject obj = base.Save();
        obj["value"] = valueBox.Text ?? string.Empty;
        return obj;
    }

    public override void Load(JsonObject obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        base.Load(obj);
        string val = obj["value"]?.GetValue<string>() ?? string.Empty;
        try { valueBox.Text = val; } catch { }
    }


    // =======================================================================
    //                            Runtime behaviour
    // =======================================================================
    public override void Evaluate()
    {
        // Set the value of all [NodeOutput] fields here
        output = valueBox.Text;
    }


}
