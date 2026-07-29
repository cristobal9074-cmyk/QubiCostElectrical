using System.Diagnostics;

namespace QubiCostElectrical.Tests.Infrastructure;

public static class AsyncAssert
{
    public static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (predicate())
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException($"AsyncAssert.WaitForAsync timed out after {timeoutMs}ms.");
    }
}
