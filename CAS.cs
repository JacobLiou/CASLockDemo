namespace CASLockDemo;

public static class Cas
{
    private static long _currentTime;

    private static long _current;

    // C# CAS API Interlocked ，保证每个计数操作都是原子操作，从而达到无锁
    public static long FixedWindow()
    {
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var ct = Interlocked.Read(ref _currentTime);
        if (now > ct)
        {
            if (Interlocked.CompareExchange(ref _currentTime, now, ct) == ct)
            {
                Interlocked.Exchange(ref _current, 0);
            }
        }

        return Interlocked.Increment(ref _current);
    }

}