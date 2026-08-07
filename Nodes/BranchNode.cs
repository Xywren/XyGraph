using System.Windows.Media;

namespace XyGraph
{
    /// <summary>
    /// Splits the execution path in two. Both outputs run — A first, then B — so a single
    /// upstream node can drive two independent chains without duplicating it.
    /// </summary>
    public class BranchNode : Node
    {
        [NodeInput(Color = "#FF000000")] public Node execute;

        [NodeOutput(Name = "A", Color = "#FF000000")] public Node branchA;
        [NodeOutput(Name = "B", Color = "#FF000000")] public Node branchB;

        public BranchNode(Graph graph) : base(graph)
        {
            title = "Branch";
            titleContainer.Background = Brushes.DimGray;
        }

        public override void Run()
        {
            base.Run();
            Completed();
        }

        public override void Completed()
        {
            base.Completed();

            // an unconnected side is legitimate — a branch with one leg still routes
            if (branchA == null && branchB == null) { Error(); return; }

            branchA?.Run();
            branchB?.Run();
        }

        public override void Error() { base.Error(); }
    }
}
