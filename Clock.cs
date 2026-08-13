using System;

namespace XyGraph
{
    /// <summary>
    /// The engine's source of "now". In production it is the real system clock; in a test it
    /// can be switched to a virtual clock that only moves when advanced, so a workflow's
    /// day-by-day progression (waits, notice cadence) can be driven and observed on demand.
    /// </summary>
    public static class Clock
    {
        private static DateTime? _virtualNow;

        public static bool IsVirtual => _virtualNow.HasValue;

        public static DateTime Now => _virtualNow ?? DateTime.Now;

        /// <summary>Switches to a virtual clock fixed at <paramref name="start"/>.</summary>
        public static void UseVirtual(DateTime start) => _virtualNow = start;

        /// <summary>Returns to the real system clock.</summary>
        public static void UseReal() => _virtualNow = null;

        public static void Advance(TimeSpan by)
        {
            if (_virtualNow.HasValue) _virtualNow = _virtualNow.Value + by;
        }

        public static void AdvanceDays(int days) => Advance(TimeSpan.FromDays(days));
    }
}
