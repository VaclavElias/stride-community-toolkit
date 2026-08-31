using Stride.Core.Diagnostics;
using System.Numerics;
using System.Threading.Channels;
using static Hexa.NET.ImGui.ImGui;
using static Stride.CommunityToolkit.ImGui.ImGuiExtension;
using TimeSpan = System.TimeSpan;

namespace Stride.CommunityToolkit.ImGui.DebugTools;

/// <summary>
/// The stateless companions of <see cref="PerfMonitor"/>: sample-strip drawing, Stride profiler
/// marker collection, and small formatting and lookup helpers. Split out of
/// <see cref="PerfMonitor"/>, whose code keeps calling them unqualified through
/// <c>using static</c>.
/// </summary>
internal static class PerfMonitorHelpers
{
    static readonly ProfilingKey _dummyKey = new("dummy");

    internal static async void StartProcessingMarkers(List<PerfMonitor.EventWrapper> _sortedList, CancellationToken token)
    {
        ChannelReader<ProfilingEvent> events = Profiler.Subscribe();
        try
        {
            while (token.IsCancellationRequested == false)
            {
                ProfilingEvent perfEvent = await events.ReadAsync(token);
                PerfMonitor.EventWrapper begin = new(perfEvent, true, perfEvent.TimeStamp);
                PerfMonitor.EventWrapper end = new(perfEvent, false, perfEvent.TimeStamp + perfEvent.ElapsedTime);
                lock (_sortedList)
                {
                    var index = _sortedList.BinarySearch(begin);
                    if (index < 0)
                        _sortedList.Insert(~index, begin);
                    else
                        _sortedList.Insert(index, begin);
                    var index2 = _sortedList.BinarySearch(end);
                    if (index2 < 0)
                        _sortedList.Insert(~index2, end);
                    else
                        _sortedList.Insert(index2, end);
                }
            }
        }
        finally
        {
            Profiler.Unsubscribe(events);
        }
    }

    /// <summary>
    /// Turns the frame's collected Stride profiler begin/end markers into displayable samples, or just
    /// clears them while the monitor is paused. Depth bookkeeping and the min/max window come along verbatim.
    /// </summary>
    internal static void ParseStrideProfilerEvents(
        List<PerfMonitor.EventWrapper> sorter,
        ref (List<PerfMonitor.SampleInstance> samples, TimeSpan start, double duration, int depth) gpu,
        ref (List<PerfMonitor.SampleInstance> samples, TimeSpan start, double duration, int depth) stride,
        bool pauseEval)
    {
        if (pauseEval)
        {
            lock (sorter)
                sorter.Clear();

            return;
        }

        TimeSpan min = TimeSpan.MaxValue, max = TimeSpan.MinValue;

        stride.samples.Clear();
        gpu.samples.Clear();

        lock (sorter)
        {
            foreach (var e in sorter)
            {
                ref var data = ref e.Event.IsGPUEvent() ? ref gpu : ref stride;

                if (e.Begin)
                {
                    data.depth++;
                    continue;
                }

                var end = e.Event.TimeStamp + e.Event.ElapsedTime;
                min = min <= e.Event.TimeStamp ? min : e.Event.TimeStamp;
                max = max > end ? max : end;
                var sample = new PerfMonitor.SampleInstance(e.Event.Key.Name, data.depth, e.Event.TimeStamp, e.Event.ElapsedTime.TotalMilliseconds, null);
                data.samples.Add(sample);
                data.depth--;
            }
            sorter.Clear();
        }

        if (stride.samples.Count > 0)
            stride = stride with { start = min, duration = (max - min).TotalMilliseconds };

        if (gpu.samples.Count > 0)
            gpu = gpu with { start = min, duration = (max - min).TotalMilliseconds };
    }

    internal static void DrawSample(Vector2 corner, float maxWidth, PerfMonitor.SampleInstance sample, TimeSpan start, double duration)
    {
        const float MIN_SIZE = 2f;
        float height = GetTextLineHeightWithSpacing();
        // Get ratio of this sample compared to total frame duration
        float size = (float)(sample.Duration / duration);
        size *= maxWidth; // Fit ratio to window
        size = size < MIN_SIZE ? MIN_SIZE : size;
        // Compute offset from the window's edge
        float pos = (float)(sample.Start - start).TotalMilliseconds;
        pos /= (float)duration;
        pos *= maxWidth;
        // outside of view:left
        if (pos + size < MIN_SIZE)
            size += pos + size + MIN_SIZE;
        // outside of view:right
        if (pos > maxWidth - MIN_SIZE)
            pos = maxWidth - MIN_SIZE;

        SetCursorPos(corner + new Vector2(pos, sample.Depth * height));
        Button(sample.Id, new Vector2(size, height));
        if (IsItemHovered())
        {
            using (Tooltip())
            {
                TextUnformatted(sample.DeltaMemAlloc.HasValue
                    ? $"{sample.Id}:\n{S(sample.Duration)}ms - {Ts(sample.DeltaMemAlloc)} byte(s)"
                    : $"{sample.Id}:\n{S(sample.Duration)}ms");
            }
        }
    }

    internal static string S(float val, string? format = null)
    {
        return val.ToString(format ?? "F2", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static string S(double val, string? format = null)
    {
        return val.ToString(format ?? "F2", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static string Ts<T>(T val)
    {
        return val?.ToString() ?? string.Empty;
    }

    internal static bool IsStrideProfilingAll()
    {
        Profiler.Disable(_dummyKey);
        // With the given disabled key this function will return true if EnableAll is set
        return Profiler.IsEnabled(_dummyKey);
    }

    /// <summary> Guarantees that this key exist and returns at least a default new() value </summary>
    internal static TValue Guaranteed<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key) where TValue : new()
    {
        if (dictionary.TryGetValue(key, out var value) == false)
        {
            value = new TValue();
            dictionary.Add(key, value);
        }

        return value;
    }
}