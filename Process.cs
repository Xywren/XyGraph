namespace XyGraph
{
    /// <summary>
    /// A handle to an in-flight workflow — the snapshot created when a Workflow (template) is
    /// run. Events are emitted at a Process, not at a raw id. The engine resolves the handle to
    /// the live graph instance (backed by Kraken's GraphInstance) at delivery time.
    /// </summary>
    public class Process
    {
        /// <summary>The GraphInstance id this handle points at, or ROGUE when unresolved.</summary>
        public int instanceId = -1;

        public Process() { }
        public Process(int instanceId) { this.instanceId = instanceId; }
    }
}
