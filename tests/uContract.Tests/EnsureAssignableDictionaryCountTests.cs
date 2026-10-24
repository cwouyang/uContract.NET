using System.Collections;
using System.Collections.Frozen;
using uContract.Exceptions;

namespace uContract.Tests;

public class EnsureAssignableDictionaryCountTests
{
    private const string ItemsChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoDictionariesOfDifferentCounts_ReportsTheMemberWithoutReadingAnEntry()
    {
        EntryRecordingDictionary actualItems = new(new Hashtable { ["apple"] = 1, ["pear"] = 2 });
        EntryRecordingDictionary expectedItems = new(new Hashtable { ["apple"] = 1 });
        Holder<IDictionary> actual = new() { Items = actualItems };
        Holder<IDictionary> expected = new() { Items = expectedItems };

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.Equal(ItemsChanged, exception.Description);
        Assert.Equal(0, actualItems.EntryReads);
        Assert.Equal(0, expectedItems.EntryReads);
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }

    // A dictionary that counts every read of its entries: enumeration, keys, values, the indexer and CopyTo.
    // Count and the other properties that say nothing about an entry are not counted.
    private sealed class EntryRecordingDictionary(Hashtable entries) : IDictionary
    {
        public int EntryReads { get; private set; }

        public int Count => entries.Count;

        public bool IsFixedSize => entries.IsFixedSize;

        public bool IsReadOnly => entries.IsReadOnly;

        public bool IsSynchronized => entries.IsSynchronized;

        public object SyncRoot => entries.SyncRoot;

        public ICollection Keys
        {
            get
            {
                EntryReads++;
                return entries.Keys;
            }
        }

        public ICollection Values
        {
            get
            {
                EntryReads++;
                return entries.Values;
            }
        }

        public object? this[object key]
        {
            get
            {
                EntryReads++;
                return entries[key];
            }
            set => entries[key] = value;
        }

        public void Add(object key, object? value) => entries.Add(key, value);

        public void Clear() => entries.Clear();

        public bool Contains(object key)
        {
            EntryReads++;
            return entries.Contains(key);
        }

        public void CopyTo(Array array, int index)
        {
            EntryReads++;
            entries.CopyTo(array, index);
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            EntryReads++;
            return entries.GetEnumerator();
        }

        public void Remove(object key) => entries.Remove(key);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

// The runtime is simulated as Native AOT, so a value of a type with no members cannot be compared: reading
// an entry that holds one throws InvalidOperationException ("cannot compare") instead of reporting a change.
[Collection("EnvironmentVariables")]
public class EnsureAssignableDictionaryCountWithoutDynamicCodeTests
{
    private const string ItemsChanged =
        "Fields were modified that are not marked as assignable:\n  - Items\n  - <Items>k__BackingField";

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsDictionariesOfDifferentTypesAndCounts_ReportsTheMemberWithoutReadingAnEntry()
    {
        Dictionary<string, NoMembers> actualItems = new(StringComparer.Ordinal) { ["apple"] = new NoMembers() };
        SortedDictionary<string, NoMembers> expectedItems = new(StringComparer.Ordinal)
        {
            ["apple"] = new NoMembers(),
            ["pear"] = new NoMembers(),
        };
        Holder<IDictionary<string, NoMembers>> actual = new() { Items = actualItems };
        Holder<IDictionary<string, NoMembers>> expected = new() { Items = expectedItems };

        // Under WithoutDynamicCode, reading the "apple" entry would end in "cannot compare", so the violation
        // shows that no entry was read.
        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(ItemsChanged, violation.Description);
    }

    [Fact]
    public void EnsureAssignable_WhenMemberHoldsTwoFrozenDictionariesOfDifferentCounts_ReportsTheMemberWithoutReadingAnEntry()
    {
        // Every key is its own instance, so the first pair of keys read, in whatever order, cannot be compared:
        // under WithoutDynamicCode reading an entry would end in "cannot compare", so the violation shows that
        // no entry was read.
        FrozenDictionary<NoMembers, int> actualItems = KeysWithNoMembers(count: 1);
        FrozenDictionary<NoMembers, int> expectedItems = KeysWithNoMembers(count: 2);
        Holder<FrozenDictionary<NoMembers, int>> actual = new() { Items = actualItems };
        Holder<FrozenDictionary<NoMembers, int>> expected = new() { Items = expectedItems };

        Exception? exception = TestRuntime.WithoutDynamicCode(() => Contract.EnsureAssignable(actual, expected));

        PostconditionViolationException violation = Assert.IsType<PostconditionViolationException>(exception);
        Assert.Equal(ItemsChanged, violation.Description);
    }

    private static FrozenDictionary<NoMembers, int> KeysWithNoMembers(int count)
    {
        return Enumerable.Range(0, count).ToFrozenDictionary(_ => new NoMembers(), value => value);
    }

    private sealed class Holder<TItems>
    {
        public required TItems Items { get; set; }
    }

    private sealed class NoMembers;
}
