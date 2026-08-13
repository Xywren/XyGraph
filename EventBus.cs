using System;

namespace XyGraph
{
    /// <summary>
    /// Seam between an EmitEvent node (which lives in the engine) and the process runtime
    /// (which lives in Kraken and knows how to resolve a Process handle to a live instance and
    /// deliver to it). Kraken wires <see cref="Deliver"/> at load; the node just calls it.
    /// </summary>
    public static class EventBus
    {
        // (target process, channel, payload) → engine delivers to the target's matching entries.
        public static Action<Process, string, object> Deliver;
    }
}
