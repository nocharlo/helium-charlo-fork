using System;
using System.Diagnostics;

namespace HeliumHybrid
{
    // Disabled unless the local process has HELIUM_NAVIGATOR_DIAGNOSTICS=1.
    // Messages contain state names, booleans, counts and timings only: never game or preset IDs.
    internal sealed class NavigatorDiagnostics
    {
        private readonly Action<string> write;
        private bool enabled;
        private NavigatorViewSnapshot previous;
        private long refreshes;
        private long skipped;
        private long rebuilds;
        private double elapsedTotal;
        private double elapsedMaximum;
        private long lastSummaryTick;

        internal NavigatorDiagnostics(bool enabled, Action<string> write)
        {
            this.enabled = enabled;
            this.write = write;
            lastSummaryTick = Stopwatch.GetTimestamp();
        }

        internal bool Enabled => enabled;

        internal static bool Requested()
        {
            return string.Equals(Environment.GetEnvironmentVariable("HELIUM_NAVIGATOR_DIAGNOSTICS"),
                "1", StringComparison.Ordinal);
        }

        internal static double MillisecondsSince(long start)
        {
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        internal NavigatorViewSnapshot Capture(Func<NavigatorViewSnapshot> capture)
        {
            if (!enabled) return null;
            try { return capture(); }
            catch { enabled = false; return null; }
        }

        internal void Record(NavigatorViewSnapshot state, bool dirty, bool membershipChanged,
            bool orderChanged, bool viewStateChanged, bool rebuilt, double totalMs, double projectionMs, double viewMs)
        {
            if (!enabled) return;
            refreshes++;
            elapsedTotal += totalMs;
            elapsedMaximum = Math.Max(elapsedMaximum, totalMs);
            if (rebuilt) rebuilds++; else skipped++;

            if (state != null && NavigatorRefreshDecision.ViewStateChanged(state, previous)) {
                var presetChanged = previous != null && state.PresetId != previous.PresetId;
                Emit("state sort=" + state.SortOrder + " direction=" + state.SortDirection +
                    " nativeGroup=" + state.Grouping + " navigatorGroup=" + state.NavigatorGrouping +
                    " completionFilter=" + state.CompletionStatusFilterActive +
                    " presetChanged=" + presetChanged);
            }
            previous = state;

            if (rebuilt) {
                Emit("rebuild dirty=" + dirty + " membershipChanged=" + membershipChanged +
                    " orderChanged=" + orderChanged + " viewStateChanged=" + viewStateChanged +
                    " totalMs=" + Format(totalMs) + " projectionMs=" + Format(projectionMs) +
                    " viewMs=" + Format(viewMs));
            }

            var now = Stopwatch.GetTimestamp();
            if (now - lastSummaryTick >= Stopwatch.Frequency * 60L) {
                Emit("refresh summary checks=" + refreshes + " skipped=" + skipped +
                    " rebuilt=" + rebuilds + " averageMs=" + Format(elapsedTotal / refreshes) +
                    " maxMs=" + Format(elapsedMaximum));
                refreshes = skipped = rebuilds = 0;
                elapsedTotal = elapsedMaximum = 0;
                lastSummaryTick = now;
            }
        }

        internal void VisualUpdate(int occurrences)
        {
            if (enabled) Emit("visual-update occurrences=" + occurrences);
        }

        private static string Format(double value)
        {
            return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Emit(string message)
        {
            if (!enabled) return;
            try { write?.Invoke("Helium Charlo Navigator diagnostics: " + message); }
            catch { enabled = false; }
        }
    }
}
