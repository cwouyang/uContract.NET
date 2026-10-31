namespace uContract.Tests;

public sealed class TypeCacheTests
{
    [Fact]
    public void GetOrAdd_WhenNonCollectibleTypeIsCached_AllocatesNothing()
    {
        TypeCache<bool> cache = new();

        long allocated = AllocatedBy(() => cache.GetOrAdd(typeof(TestAccount), static _ => true));

        Assert.Equal(0, allocated);
    }

    // The call runs once before it is measured, so the value cached on the first call is not counted.
    private static long AllocatedBy(Action act)
    {
        act();
        long before = GC.GetAllocatedBytesForCurrentThread();
        act();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
