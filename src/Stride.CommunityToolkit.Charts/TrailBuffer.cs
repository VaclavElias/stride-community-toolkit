using Stride.Core.Mathematics;
using System.Runtime.InteropServices;

namespace Stride.CommunityToolkit.Charts;

/// <summary>
/// The points of a growing trail, oldest first, with breaks between runs kept in the same list as a point
/// that is not finite. Holds at most <see cref="Capacity"/> points; a full trail either ignores the next
/// point or drops the oldest, as <see cref="RollOver"/> says.
/// </summary>
internal sealed class TrailBuffer
{
    private static readonly Vector3 BreakMark = new(float.NaN);

    private readonly List<Vector3> _items;

    /// <summary>How many points the trail holds; breaks are not counted.</summary>
    internal int Count { get; private set; }

    /// <summary>The most points the trail can hold.</summary>
    internal int Capacity { get; }

    /// <summary>Whether a full trail drops its oldest point to make room, instead of ignoring the new one.</summary>
    internal bool RollOver { private get; set; }

    internal TrailBuffer(int capacity)
    {
        Capacity = capacity;

        // A break can follow every point, so the list may hold up to twice the capacity
        _items = new List<Vector3>(capacity * 2);
    }

    /// <summary>The points and break marks, oldest first; a break is a point whose <c>x</c> is not finite.</summary>
    internal ReadOnlySpan<Vector3> Items => CollectionsMarshal.AsSpan(_items);

    /// <summary>Whether an item is a break rather than a point.</summary>
    internal static bool IsBreak(in Vector3 item) => float.IsNaN(item.X);

    /// <summary>Appends a finite point; a repeat of the last point is ignored.</summary>
    internal void Add(Vector3 point)
    {
        if (_items.Count > 0 && !IsBreak(_items[^1]) && (point - _items[^1]).LengthSquared() <= MathUtil.ZeroTolerance * MathUtil.ZeroTolerance)
            return;

        if (Count == Capacity)
        {
            if (!RollOver)
                return;

            DropOldest();
        }

        _items.Add(point);
        Count++;
    }

    /// <summary>Ends the current run; the next point starts a new one. Nothing happens on an empty or already-broken trail.</summary>
    internal void Break()
    {
        if (_items.Count > 0 && !IsBreak(_items[^1]))
        {
            _items.Add(BreakMark);
        }
    }

    /// <summary>Removes everything.</summary>
    internal void Clear()
    {
        _items.Clear();
        Count = 0;
    }

    private void DropOldest()
    {
        _items.RemoveAt(0);
        Count--;

        // A break with nothing before it separates nothing
        if (_items.Count > 0 && IsBreak(_items[0]))
        {
            _items.RemoveAt(0);
        }
    }
}