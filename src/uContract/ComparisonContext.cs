using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace uContract;

/// <summary>
///     The state of one <see cref="Contract.EnsureAssignable{T}" /> comparison, threaded through the walk.
/// </summary>
internal sealed class ComparisonContext
{
    // The depth of the top-level pair, which stays in progress for the whole call: relying on it is
    // relying on nothing that can still turn out different.
    private const int Permanent = int.MaxValue;

    // Each pair being compared, with its depth in the walk.
    private readonly Dictionary<ReferencePair, int> _inProgress = [];
    private readonly HashSet<ReferencePair> _equal = [];
    private readonly HashSet<ReferencePair> _different = [];

    // Pairs found equal while relying on in-progress pairs, with the shallowest depth relied on, in the
    // order they were found. Each becomes equal or is discarded when the pair it relies on ends.
    private readonly Dictionary<ReferencePair, int> _provisional = [];
    private readonly List<ReferencePair> _provisionalOrder = [];

    // The shallowest in-progress pair the current comparison has relied on.
    private int _reliedOn = Permanent;

    /// <summary>
    ///     Set when a type with no visible members blocked the comparison.
    /// </summary>
    public MemberComparison.HiddenMembers? Hidden { get; set; }

    /// <summary>
    ///     Marks the top-level pair as in progress for the whole call.
    /// </summary>
    public void EnterTopLevel(ReferencePair pair)
    {
        _inProgress[pair] = Permanent;
    }

    /// <summary>
    ///     True when the pair needs no walk: it is known equal or different, or it is being compared
    ///     (reached again through a cycle) or provisionally equal, which counts as equal while recording what
    ///     the current comparison relies on.
    /// </summary>
    public bool TryGetSettled(ReferencePair pair, out bool equal)
    {
        equal = !_different.Contains(pair);
        if (!equal || _equal.Contains(pair))
        {
            return true;
        }

        if (_inProgress.TryGetValue(pair, out int depth) || _provisional.TryGetValue(pair, out depth))
        {
            _reliedOn = Math.Min(_reliedOn, depth);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Marks the pair as in progress before its walk.
    /// </summary>
    public Frame Enter(ReferencePair pair)
    {
        Frame frame = new(_inProgress.Count, _reliedOn, _provisionalOrder.Count);
        _inProgress[pair] = frame.Depth;
        _reliedOn = Permanent;
        return frame;
    }

    /// <summary>
    ///     Records the outcome of the pair's walk. A pair that could not be compared (<see cref="Hidden" /> is
    ///     set) is remembered neither as equal nor as different.
    /// </summary>
    public void Leave(ReferencePair pair, Frame frame, bool equal)
    {
        _inProgress.Remove(pair);

        // Relying only on this pair itself, or on pairs entered below it, is relying on nothing still open.
        bool reliedOnlyOnItself = _reliedOn >= frame.Depth;
        SettleProvisional(frame, equal, reliedOnlyOnItself);
        if (equal && reliedOnlyOnItself)
        {
            _equal.Add(pair);
        }
        else if (equal)
        {
            _provisional[pair] = _reliedOn;
            _provisionalOrder.Add(pair);
        }
        else if (Hidden is null)
        {
            _different.Add(pair);
        }

        _reliedOn = reliedOnlyOnItself ? frame.ReliedOnBefore : Math.Min(frame.ReliedOnBefore, _reliedOn);
    }

    // Settles the provisionally equal pairs found while the frame's pair was compared that rely on it:
    // they become equal when it ends equal relying on nothing still open, are discarded when it ends
    // different (or could not be compared), and otherwise now rely on what it relies on.
    private void SettleProvisional(Frame frame, bool equal, bool reliedOnlyOnItself)
    {
        List<ReferencePair> stillProvisional = [];
        for (int i = frame.ProvisionalStart; i < _provisionalOrder.Count; i++)
        {
            ReferencePair found = _provisionalOrder[i];
            if (_provisional[found] < frame.Depth)
            {
                stillProvisional.Add(found);
            }
            else if (!equal || reliedOnlyOnItself)
            {
                _provisional.Remove(found);
                if (equal)
                {
                    _equal.Add(found);
                }
            }
            else
            {
                _provisional[found] = _reliedOn;
                stillProvisional.Add(found);
            }
        }

        _provisionalOrder.RemoveRange(frame.ProvisionalStart, _provisionalOrder.Count - frame.ProvisionalStart);
        _provisionalOrder.AddRange(stillProvisional);
    }

    /// <summary>
    ///     A pair's place in the walk: its depth, what the enclosing comparison relied on before it, and
    ///     where the provisionally equal pairs found while comparing it start.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Frame(int Depth, int ReliedOnBefore, int ProvisionalStart);
}
