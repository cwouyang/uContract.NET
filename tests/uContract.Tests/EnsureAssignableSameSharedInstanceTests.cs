using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices.Marshalling;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using uContract.Exceptions;

namespace uContract.Tests;

/// <summary>
///     Issue #54: <c>Old</c> shares an instance of a resource type, and of every class derived from one, with
///     the original. <c>EnsureAssignable</c> reports one such instance passed as <c>actual</c> and as
///     <c>expected</c> when its state can change, because there is no earlier state to compare with.
/// </summary>
public class EnsureAssignableSameSharedInstanceTests
{
    internal static string ExpectedMessage(Type runtimeType, Type sharedBase)
    {
        return $"EnsureAssignable cannot compare {runtimeType}: actual and expected are the same instance. "
            + $"Contract.Old shares an instance of {sharedBase} or of a type derived from it with the original "
            + "instead of copying it, so there is no earlier state to compare with. "
            + "Assignable patterns do not apply: no member was compared. "
            + "Ways out: check each state member with Contract.Ensure and a value taken with Contract.Old; "
            + "to check only that a reference was not replaced, use Contract.Ensure with ReferenceEquals; "
            + "or set DBC_POST=off (disables all postcondition checks).";
    }

    private static void AssertReportsTheSameInstance(Type runtimeType, Type sharedBase, Exception? exception)
    {
        InvalidOperationException cannotCompare = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(ExpectedMessage(runtimeType, sharedBase), cannotCompare.Message);
    }

    // A Task that was not started is not disposed: Dispose throws for it. A ComObject is not asked for an
    // interface: it answers the cast itself, and the one used here is not initialised.
    private static void Release(object instance)
    {
        if (instance is not (Task or ComObject))
        {
            (instance as IDisposable)?.Dispose();
        }
    }

    // ---- A class derived from Component that takes Old(() => this) ----

    private sealed class Worker : Component
    {
        public int State;

        public void Run(bool changeState)
        {
            var old = Contract.Old(() => this);
            if (changeState)
            {
                State++;
            }

            Contract.EnsureAssignable(this, old);
        }
    }

    [Fact]
    public void EnsureAssignable_WhenAComponentComparesItselfWithOldAfterAChange_ReportsTheSameInstance()
    {
        using Worker worker = new();

        Exception? exception = Record.Exception(() => worker.Run(changeState: true));

        AssertReportsTheSameInstance(typeof(Worker), typeof(Component), exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAComponentComparesItselfWithOldAndNothingChanged_ReportsTheSameInstance()
    {
        using Worker worker = new();

        Exception? exception = Record.Exception(() => worker.Run(changeState: false));

        AssertReportsTheSameInstance(typeof(Worker), typeof(Component), exception);
    }

    private class Mid : Component;

    private sealed class Leaf : Mid
    {
        public void Run()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    [Fact]
    public void EnsureAssignable_WhenAClassTwoLevelsBelowAComponentComparesItselfWithOld_ReportsTheSameInstance()
    {
        using Leaf leaf = new();

        Exception? exception = Record.Exception(leaf.Run);

        AssertReportsTheSameInstance(typeof(Leaf), typeof(Component), exception);
    }

    // ---- Every type that Old shares and whose state can change ----

    public static TheoryData<Func<object>, Type> SharedInstancesWhoseStateCanChange =>
        new()
        {
            { () => new MemoryStream(), typeof(Stream) },
            { () => new ManualResetEvent(initialState: false), typeof(WaitHandle) },
            { () => new CancellationTokenSource(), typeof(CancellationTokenSource) },
            // A Thread is also a CriticalFinalizerObject: the listed type is the one that is named.
            { () => new Thread(() => { }), typeof(Thread) },
            { () => new Timer(_ => { }), typeof(Timer) },
            { () => new SynchronizationContext(), typeof(SynchronizationContext) },
            { () => new SemaphoreSlim(1), typeof(SemaphoreSlim) },
            { () => new ManualResetEventSlim(), typeof(ManualResetEventSlim) },
            { () => new CountdownEvent(1), typeof(CountdownEvent) },
            { () => new ReaderWriterLockSlim(), typeof(ReaderWriterLockSlim) },
            { () => new Barrier(1), typeof(Barrier) },
            { () => Task.CompletedTask, typeof(Task) },
            { () => new WeakReference(new object()), typeof(WeakReference) },
            { () => new Component(), typeof(Component) },
            { () => new HttpClientHandler(), typeof(HttpMessageHandler) },
            { () => new HttpClient(), typeof(HttpClient) },
            { () => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), typeof(Socket) },
            { () => new ThreadLocal<int>(), typeof(ThreadLocal<>) },
            { () => new Lazy<int>(() => 1), typeof(Lazy<>) },
            { () => new WeakReference<object>(new object()), typeof(WeakReference<>) },
            { () => new ConditionalWeakTable<object, object>(), typeof(ConditionalWeakTable<,>) },
            { () => new AsyncLocal<int>(), typeof(AsyncLocal<>) },
            // System.Threading.Lock is the stand-in of LockStandIn.cs: this suite runs on net8.0.
            { () => new System.Threading.Lock(), typeof(System.Threading.Lock) },
            { () => new DerivedLock(), typeof(System.Threading.Lock) },
            { () => new SafeFileHandle(IntPtr.Zero, ownsHandle: false), typeof(CriticalFinalizerObject) },
            { () => s_uninitializedComObject, typeof(ComObject) },
        };

    [Theory]
    [MemberData(nameof(SharedInstancesWhoseStateCanChange))]
    public void EnsureAssignable_WhenOneSharedInstanceWhoseStateCanChangeIsOnBothSides_NamesTheSharedBaseType(
        Func<object> createInstance,
        Type sharedBase
    )
    {
        object instance = createInstance();

        try
        {
            Exception? exception = Record.Exception(() => Contract.EnsureAssignable<object>(instance, instance));

            AssertReportsTheSameInstance(instance.GetType(), sharedBase, exception);
        }
        finally
        {
            Release(instance);
        }
    }

    private sealed class DerivedLock : System.Threading.Lock;

    // A ComObject cannot be constructed without a COM interface pointer. The instance is never initialised,
    // so its finalizer must not run: this field keeps it reachable for the life of the process.
    private static readonly object s_uninitializedComObject = RuntimeHelpers.GetUninitializedObject(typeof(ComObject));

    // ---- User classes derived from a shared type that take Old(() => this) ----

    private interface IComparesItselfWithOld
    {
        void CompareWithOld();
    }

    // Never started.
    private sealed class UserTask() : Task(() => { }), IComparesItselfWithOld
    {
        public void CompareWithOld()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    private sealed class UserHandler : DelegatingHandler, IComparesItselfWithOld
    {
        public void CompareWithOld()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    private sealed class UserTokenSource : CancellationTokenSource, IComparesItselfWithOld
    {
        public void CompareWithOld()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    private sealed class UserContext : SynchronizationContext, IComparesItselfWithOld
    {
        public void CompareWithOld()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    private sealed class UserWeakReference() : WeakReference(target: null), IComparesItselfWithOld
    {
        public void CompareWithOld()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    private sealed class FactoryProbe
    {
        public bool Ran;
    }

    private sealed class UserLazy(FactoryProbe probe)
        : Lazy<int>(() =>
        {
            probe.Ran = true;
            return 1;
        }),
            IComparesItselfWithOld
    {
        public void CompareWithOld()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    private sealed class UserStream : MemoryStream, IComparesItselfWithOld
    {
        public void CompareWithOld()
        {
            var old = Contract.Old(() => this);
            Contract.EnsureAssignable(this, old);
        }
    }

    public static TheoryData<Func<object>, Type> UserClassesDerivedFromASharedType =>
        new()
        {
            { () => new UserTask(), typeof(Task) },
            { () => new UserHandler(), typeof(HttpMessageHandler) },
            { () => new UserTokenSource(), typeof(CancellationTokenSource) },
            { () => new UserContext(), typeof(SynchronizationContext) },
            { () => new UserWeakReference(), typeof(WeakReference) },
            { () => new UserLazy(new FactoryProbe()), typeof(Lazy<>) },
            { () => new UserStream(), typeof(Stream) },
        };

    [Theory]
    [MemberData(nameof(UserClassesDerivedFromASharedType))]
    public void EnsureAssignable_WhenAUserClassDerivedFromASharedTypeComparesItselfWithOld_NamesTheSharedBaseType(
        Func<object> createInstance,
        Type sharedBase
    )
    {
        object instance = createInstance();

        try
        {
            Exception? exception = Record.Exception(((IComparesItselfWithOld)instance).CompareWithOld);

            AssertReportsTheSameInstance(instance.GetType(), sharedBase, exception);
        }
        finally
        {
            Release(instance);
        }
    }

    [Fact]
    public void EnsureAssignable_WhenALazyComparesItselfWithOld_DoesNotRunItsValueFactory()
    {
        FactoryProbe probe = new();
        UserLazy lazy = new(probe);

        Record.Exception(lazy.CompareWithOld);

        Assert.False(probe.Ran);
    }

    // ---- What the call is given besides the instance ----

    [Fact]
    public void EnsureAssignable_WhenTheSameInstanceIsPassedOutsideAnyContractCheck_ReportsTheSameInstance()
    {
        using Worker worker = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(worker, worker));

        AssertReportsTheSameInstance(typeof(Worker), typeof(Component), exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheSameInstanceIsComparedThroughAnInterface_ReportsTheSameInstance()
    {
        using Worker worker = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<IDisposable>(worker, worker));

        AssertReportsTheSameInstance(typeof(Worker), typeof(Component), exception);
    }

    [RequiresUnreferencedCode("Forwards to EnsureAssignable.")]
    private static void Forward<TArg>(TArg actual, TArg expected)
    {
        Contract.EnsureAssignable(actual, expected);
    }

    [Fact]
    public void EnsureAssignable_WhenTheSameInstanceArrivesThroughAGenericForwarder_ReportsTheSameInstance()
    {
        using Worker worker = new();

        Exception? exception = Record.Exception(() => Forward(worker, worker));

        AssertReportsTheSameInstance(typeof(Worker), typeof(Component), exception);
    }

    [Theory]
    [InlineData(".*")]
    [InlineData("[")]
    [InlineData(null)]
    public void EnsureAssignable_WhenPatternsComeWithTheSameSharedInstance_ReportsItWithoutExaminingThem(
        string? pattern
    )
    {
        using Worker worker = new();
        string[] patterns = [pattern!];

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(worker, worker, patterns));

        AssertReportsTheSameInstance(typeof(Worker), typeof(Component), exception);
    }

    [Fact]
    public void EnsureAssignable_WhenThePatternArrayIsNull_StillThrowsArgumentNullException()
    {
        using Worker worker = new();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            Contract.EnsureAssignable(worker, worker, null!)
        );

        Assert.Equal("assignableFieldPatterns", exception.ParamName);
    }

    [Fact]
    public void EnsureAssignable_WhenAComponentIsDeclaredInTheFrozenNamespace_ReportsTheSameInstance()
    {
        using FrozenNamespaceComponent component = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<object>(component, component));

        AssertReportsTheSameInstance(typeof(FrozenNamespaceComponent), typeof(Component), exception);
    }

    // ---- The call and the contract checks around it ----

    [Fact]
    public void EnsureAssignable_AfterReportingTheSameInstance_LetsALaterContractCheckRun()
    {
        using Worker worker = new();
        bool laterRan = false;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(worker, worker));
        Contract.Ensure(
            "Later contract",
            () =>
            {
                laterRan = true;
                return true;
            }
        );

        AssertReportsTheSameInstance(typeof(Worker), typeof(Component), exception);
        Assert.True(laterRan);
    }

    [Fact]
    public void EnsureAssignable_WhenTheSameInstanceIsPassedWhileAnotherContractCheckRuns_DoesNotThrow()
    {
        using Worker worker = new();
        bool outerRan = false;

        Exception? exception = Record.Exception(() =>
            Contract.Ensure(
                "Outer contract",
                () =>
                {
                    outerRan = true;
                    Contract.EnsureAssignable(worker, worker);
                    return true;
                }
            )
        );

        Assert.Null(exception);
        Assert.True(outerRan);
    }

    // ---- A member that holds the shared instance ----

    private sealed class Job(CancellationTokenSource? cancellation)
    {
        private CancellationTokenSource? _cancellation = cancellation;

        public void Run(Func<CancellationTokenSource?, CancellationTokenSource?> body)
        {
            var old = Contract.Old(() => _cancellation);
            _cancellation = body(_cancellation);
            Contract.EnsureAssignable(_cancellation, old);
        }
    }

    [Fact]
    public void EnsureAssignable_WhenAMemberIsComparedWithItsOldAndIsTheSameInstance_ReportsTheSameInstance()
    {
        using CancellationTokenSource cancellation = new();
        Job job = new(cancellation);

        Exception? exception = Record.Exception(() => job.Run(current => current));

        AssertReportsTheSameInstance(typeof(CancellationTokenSource), typeof(CancellationTokenSource), exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAMemberIsComparedWithItsOldAndBothAreNull_DoesNotThrow()
    {
        Job job = new(cancellation: null);

        Exception? exception = Record.Exception(() => job.Run(current => current));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenAMemberIsComparedWithItsOldAndWasReplaced_ThrowsPostconditionViolation()
    {
        using CancellationTokenSource cancellation = new();
        using CancellationTokenSource replacement = new();
        Job job = new(cancellation);

        Exception? exception = Record.Exception(() => job.Run(_ => replacement));

        Assert.IsType<PostconditionViolationException>(exception);
    }

    private sealed class Crew : IDisposable
    {
        public Worker Lead = new();

        public void Dispose()
        {
            Lead.Dispose();
        }
    }

    [Fact]
    public void EnsureAssignable_WhenASharedInstanceBelowTheTopLevelChanged_DoesNotThrow()
    {
        using Crew crew = new();
        var old = Contract.Old(() => crew);
        crew.Lead.State++;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(crew, old));

        Assert.Null(exception);
    }

    // ---- Two different instances ----

    [Fact]
    public void EnsureAssignable_WhenTwoDifferentComponentsDiffer_ReportsTheChangedMember()
    {
        using Worker actual = new() { State = 1 };
        using Worker expected = new();

        PostconditionViolationException exception = Assert.Throws<PostconditionViolationException>(() =>
            Contract.EnsureAssignable(actual, expected)
        );

        Assert.EndsWith(
            "Fields were modified that are not marked as assignable:\n  - State",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void EnsureAssignable_WhenTwoDifferentComponentsAreComparedAsObject_DoesNotThrow()
    {
        using Worker actual = new() { State = 1 };
        using Worker expected = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<object>(actual, expected));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTwoDifferentComponentsAreComparedThroughAnInterface_DoesNotThrow()
    {
        using Worker actual = new() { State = 1 };
        using Worker expected = new();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<IDisposable>(actual, expected));

        Assert.Null(exception);
    }

    // ---- The same instance of a type that is shared and fixed, or that is not shared ----

    public static TheoryData<Func<object>> SharedInstancesThatAreFixed =>
        [
            () => new List<int> { 1, 2 }.ToFrozenSet(),
            () => new Regex("^a+$", RegexOptions.None, TimeSpan.FromSeconds(1)),
            () => typeof(int),
            () => typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!,
            () => StringComparer.Ordinal,
            () => (Action)(() => { }),
            () => new string('a', 3),
        ];

    [Theory]
    [MemberData(nameof(SharedInstancesThatAreFixed))]
    public void EnsureAssignable_WhenOneSharedInstanceThatIsFixedIsOnBothSides_DoesNotThrow(Func<object> createInstance)
    {
        object instance = createInstance();

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable<object>(instance, instance));

        Assert.Null(exception);
    }

    private sealed class CountingRegex() : Regex("^a+$", RegexOptions.None, TimeSpan.FromSeconds(1))
    {
        public int Uses;

        public void Use()
        {
            var old = Contract.Old(() => this);
            Uses++;
            Contract.EnsureAssignable(this, old);
        }
    }

    [Fact]
    public void EnsureAssignable_WhenAClassDerivedFromRegexComparesItselfWithOldAfterAChange_DoesNotThrow()
    {
        CountingRegex regex = new();

        Exception? exception = Record.Exception(regex.Use);

        Assert.Null(exception);
    }

    private sealed class Plain
    {
        public int Value;
    }

    [Fact]
    public void EnsureAssignable_WhenTheSameInstanceOfAnOrdinaryClassIsOnBothSides_DoesNotThrow()
    {
        Plain plain = new() { Value = 3 };

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(plain, plain));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureAssignable_WhenTheSameBoxedValueIsOnBothSides_DoesNotThrow()
    {
        object box = 5;

        Exception? exception = Record.Exception(() => Contract.EnsureAssignable(box, box));

        Assert.Null(exception);
    }
}
