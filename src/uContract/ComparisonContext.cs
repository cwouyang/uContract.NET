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

    // Pairs found equal while relying on in-progress pairs, each in the group of the shallowest pair it
    // relies on. A group is settled once, when its pair ends: all its pairs become equal, are discarded, or
    // are handed over whole to the group of the pair it relied on.
    private readonly Dictionary<ReferencePair, ProvisionalGroup> _provisional = [];

    // The group of each in-progress depth, created when a pair first relies on that depth.
    private readonly List<ProvisionalGroup?> _groupsByDepth = [];

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

        if (_inProgress.TryGetValue(pair, out int depth))
        {
            _reliedOn = Math.Min(_reliedOn, depth);
            return true;
        }

        if (_provisional.TryGetValue(pair, out ProvisionalGroup? group))
        {
            _reliedOn = Math.Min(_reliedOn, group.Current().Depth);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Marks the pair as in progress before its walk.
    /// </summary>
    public Frame Enter(ReferencePair pair)
    {
        Frame frame = new(_inProgress.Count, _reliedOn);
        _inProgress[pair] = frame.Depth;
        while (_groupsByDepth.Count <= frame.Depth)
        {
            _groupsByDepth.Add(null);
        }

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
        ProvisionalGroup? relyingOnThis = _groupsByDepth[frame.Depth];
        _groupsByDepth[frame.Depth] = null;

        // Relying only on this pair itself, or on pairs entered below it, is relying on nothing still open.
        bool reliedOnlyOnItself = _reliedOn >= frame.Depth;
        if (!equal)
        {
            // Every enclosing pair ends different too (a difference ends each walk it is found in), so the
            // pairs relying on those are discarded as each of them ends.
            Discard(relyingOnThis);
            if (Hidden is null)
            {
                _different.Add(pair);
            }
        }
        else if (reliedOnlyOnItself)
        {
            Commit(relyingOnThis);
            _equal.Add(pair);
        }
        else
        {
            ProvisionalGroup reliedOn = GroupOf(_reliedOn);
            reliedOn.Absorb(relyingOnThis);
            reliedOn.Add(pair);
            _provisional[pair] = reliedOn;
        }

        _reliedOn = reliedOnlyOnItself ? frame.ReliedOnBefore : Math.Min(frame.ReliedOnBefore, _reliedOn);
    }

    private ProvisionalGroup GroupOf(int depth)
    {
        return _groupsByDepth[depth] ??= new ProvisionalGroup(depth);
    }

    private void Commit(ProvisionalGroup? group)
    {
        for (PairNode? node = group?.First; node is not null; node = node.Next)
        {
            _provisional.Remove(node.Pair);
            _equal.Add(node.Pair);
        }
    }

    private void Discard(ProvisionalGroup? group)
    {
        for (PairNode? node = group?.First; node is not null; node = node.Next)
        {
            _provisional.Remove(node.Pair);
        }
    }

    /// <summary>
    ///     A pair's place in the walk: its depth, and what the enclosing comparison relied on before it.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Frame(int Depth, int ReliedOnBefore);

    // The provisionally equal pairs relying on one in-progress depth, as a linked list, so that handing a
    // whole group to another takes constant time. A group handed over forwards to the group it joined.
    private sealed class ProvisionalGroup(int depth)
    {
        private ProvisionalGroup? _joined;

        public int Depth { get; } = depth;

        public PairNode? First { get; private set; }

        private PairNode? Last { get; set; }

        public void Add(ReferencePair pair)
        {
            PairNode node = new(pair);
            Append(node, node);
        }

        public void Absorb(ProvisionalGroup? other)
        {
            if (other is null)
            {
                return;
            }

            other._joined = this;
            if (other.First is not null)
            {
                Append(other.First, other.Last!);
            }
        }

        // The group a pair added to this one now belongs to, shortening the forwarding chain on the way.
        public ProvisionalGroup Current()
        {
            ProvisionalGroup current = this;
            while (current._joined is not null)
            {
                current = current._joined;
            }

            for (ProvisionalGroup group = this; group._joined is not null; )
            {
                ProvisionalGroup next = group._joined;
                group._joined = current;
                group = next;
            }

            return current;
        }

        private void Append(PairNode first, PairNode last)
        {
            if (Last is null)
            {
                First = first;
            }
            else
            {
                Last.Next = first;
            }

            Last = last;
        }
    }

    private sealed class PairNode(ReferencePair pair)
    {
        public ReferencePair Pair { get; } = pair;

        public PairNode? Next { get; set; }
    }
}
