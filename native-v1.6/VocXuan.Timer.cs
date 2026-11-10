using System;
using System.Diagnostics;

namespace ShiCi {
// A monotonic clock caps each inactive interval at five seconds, even if the
// UI thread wakes late after sleep or a blocking system dialog.
public sealed class ActivityTimer {
    readonly Func<double> now;
    readonly Func<DateTime> wall;
    DateTime cursorWall;
    public Action<DateTime, double> OnElapsed { get; set; }
    double total, cursor, lastActivity;
    public bool Running { get; private set; }
    public bool ManualPaused { get; private set; }
    public bool IdlePaused { get; private set; }
    public ActivityTimer() { var sw = Stopwatch.StartNew(); now = () => sw.Elapsed.TotalSeconds; wall = () => DateTime.Now; }
    public ActivityTimer(Func<double> source, Func<DateTime> wallSource = null) { now = source; wall = wallSource ?? (() => DateTime.Now); }
    public void Start(double seconds) {
        total = seconds; cursor = lastActivity = now(); cursorWall = wall();
        Running = true; ManualPaused = IdlePaused = false;
    }
    public double Seconds {
        get {
            double stamp = now();
            if (Running) {
                double end = Math.Min(stamp, lastActivity + 5.0);
                double delta = Math.Max(0, end - cursor);
                total += delta;
                if (delta > 0 && OnElapsed != null) OnElapsed(cursorWall, delta);
                cursorWall = cursorWall.AddTicks((long)Math.Round(delta * TimeSpan.TicksPerSecond)); cursor = end;
                if (stamp >= lastActivity + 5.0) { Running = false; IdlePaused = true; }
            }
            return total;
        }
    }
    public void Activity() {
        double elapsed = Seconds;
        lastActivity = now();
        if (IdlePaused && !ManualPaused) { Running = true; IdlePaused = false; cursor = lastActivity; cursorWall = wall(); }
    }
    public void ToggleManual() {
        double elapsed = Seconds;
        if (ManualPaused) Resume();
        else { Running = false; IdlePaused = false; ManualPaused = true; }
    }
    public void Resume() { Running = true; ManualPaused = IdlePaused = false; cursor = lastActivity = now(); cursorWall = wall(); }
    public void Stop() { double elapsed = Seconds; Running = false; IdlePaused = false; }
}
}
