using System;

namespace XyGraph
{
    /// <summary>
    /// Global single-selection manager for edge handles.
    /// Only one handle can be selected at a time across all edges.
    /// </summary>
    internal static class EdgeHandleSelection
    {
        private static Action currentDeselect;

        /// <summary>Register a new selection, automatically deselecting the previous one.</summary>
        public static void Register(Action onDeselect)
        {
            currentDeselect?.Invoke();
            currentDeselect = onDeselect;
        }

        /// <summary>Deselect the current handle (called on background click etc.).</summary>
        public static void ClearAll()
        {
            currentDeselect?.Invoke();
            currentDeselect = null;
        }
    }
}
