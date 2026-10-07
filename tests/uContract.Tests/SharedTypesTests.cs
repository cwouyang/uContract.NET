using System.Collections.Frozen;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace uContract.Tests;

public sealed class SharedTypesTests
{
    public static TheoryData<string, Func<object>> ListedTypeInstances =>
        new()
        {
            { "string", () => new string('a', 3) },
            { "Delegate", () => (Action)(() => { }) },
            { "Type", () => typeof(int) },
            { "MemberInfo", () => typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)! },
            { "Assembly", () => typeof(object).Assembly },
            { "Module", () => typeof(object).Module },
            { "Pointer", NullIntPointer },
            { "Stream", () => new MemoryStream() },
            { "WaitHandle", () => new ManualResetEvent(initialState: false) },
            { "CancellationTokenSource", () => new CancellationTokenSource() },
            { "Thread", () => new Thread(() => { }) },
            { "Timer", () => new Timer(_ => { }) },
            { "SynchronizationContext", () => new SynchronizationContext() },
            { "SemaphoreSlim", () => new SemaphoreSlim(1) },
            { "ManualResetEventSlim", () => new ManualResetEventSlim() },
            { "CountdownEvent", () => new CountdownEvent(1) },
            { "ReaderWriterLockSlim", () => new ReaderWriterLockSlim() },
            { "Barrier", () => new Barrier(1) },
            { "ThreadLocal<T>", () => new ThreadLocal<int>() },
            { "Task", () => Task.CompletedTask },
            { "Lazy<T>", () => new Lazy<int>(() => 1) },
            { "WeakReference", () => new WeakReference(new object()) },
            { "WeakReference<T>", () => new WeakReference<object>(new object()) },
            { "ConditionalWeakTable<TKey, TValue>", () => new ConditionalWeakTable<object, object>() },
            { "Component", () => new Component() },
            { "HttpMessageHandler", () => new HttpClientHandler() },
            { "HttpClient", () => new HttpClient() },
            { "Socket", () => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) },
            { "Regex", () => new Regex("^a+$", RegexOptions.None, TimeSpan.FromSeconds(1)) },
            { "AsyncLocal<T>", () => new AsyncLocal<int>() },
        };

    [Theory]
    [MemberData(nameof(ListedTypeInstances))]
    public void IsShared_WhenTypeIsListed_ReturnsTrue(string listedType, Func<object> createInstance)
    {
        object instance = createInstance();

        try
        {
            Assert.True(SharedTypes.IsShared(instance.GetType()), $"{listedType}: {instance.GetType()}");
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    public static TheoryData<string, Func<object>> CategoryInstances =>
        new()
        {
            { "SafeHandle", () => new SafeFileHandle(IntPtr.Zero, ownsHandle: false) },
            { "CriticalHandle", () => new InvalidCriticalHandle() },
            { "frozen set", () => new List<int> { 1, 2 }.ToFrozenSet() },
            {
                "frozen dictionary",
                () =>
                    new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 }.ToFrozenDictionary(
                        StringComparer.Ordinal
                    )
            },
            { "comparer stored by new HashSet<string>()", StoredStringSetComparer },
            { "StringComparer.OrdinalIgnoreCase", () => StringComparer.OrdinalIgnoreCase },
            { "Comparer<string>.Default", () => Comparer<string>.Default },
        };

    [Theory]
    [MemberData(nameof(CategoryInstances))]
    public void IsShared_WhenTypeBelongsToASharedCategory_ReturnsTrue(string category, Func<object> createInstance)
    {
        object instance = createInstance();

        try
        {
            Assert.True(SharedTypes.IsShared(instance.GetType()), $"{category}: {instance.GetType()}");
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    // System.Threading.Lock exists from .NET 9 only and this suite runs on net8.0, so LockStandIn.cs declares a
    // type with that full name here. The rule matches the full name, so the stand-in exercises the same check.
    [Theory]
    [InlineData(typeof(System.Threading.Lock))]
    [InlineData(typeof(DerivedLock))]
    public void IsShared_WhenTypeIsNamedSystemThreadingLockOrDerivesFromIt_ReturnsTrue(Type type)
    {
        Assert.True(SharedTypes.IsShared(type));
    }

    [Fact]
    public void IsShared_WhenTypeIsAComImport_ReturnsTrue()
    {
        Assert.True(typeof(ImportedComClass).IsCOMObject);

        Assert.True(SharedTypes.IsShared(typeof(ImportedComClass)));
    }

    [Fact]
    public void IsShared_WhenTypeIsTheSourceGeneratedComObject_ReturnsTrue()
    {
        Assert.True(SharedTypes.IsShared(typeof(ComObject)));
    }

    [Theory]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(Elem))]
    [InlineData(typeof(object))]
    public void IsShared_WhenTypeIsAnOrdinaryValueHolder_ReturnsFalse(Type type)
    {
        Assert.False(SharedTypes.IsShared(type));
    }

    [Fact]
    public void IsShared_WhenTypeDerivesFromAListedType_ReturnsTrue()
    {
        Assert.True(SharedTypes.IsShared(typeof(RecordingStream)));
    }

    // The comparer a HashSet<string> stores is an internal CoreLib type, distinct from what its Comparer property returns.
    private static object StoredStringSetComparer()
    {
        return typeof(HashSet<string>)
            .GetField("_comparer", BindingFlags.NonPublic | BindingFlags.Instance)!
#pragma warning disable MA0002 // The row is about the comparer a HashSet<string> picks when given none.
            .GetValue(new HashSet<string>())!;
#pragma warning restore MA0002
    }

    private static unsafe object NullIntPointer()
    {
        return Pointer.Box(null, typeof(int*));
    }

    private sealed class Elem(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class RecordingStream : MemoryStream;

    private sealed class DerivedLock : System.Threading.Lock;

    private sealed class InvalidCriticalHandle : CriticalHandleZeroOrMinusOneIsInvalid
    {
        protected override bool ReleaseHandle() => true;
    }

    [ComImport]
    [Guid("6B0F1C2E-6E0A-4F43-9E56-2D1B7A9C3F10")]
    private class ImportedComClass;
}
