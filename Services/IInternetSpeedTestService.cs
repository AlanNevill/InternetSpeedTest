using System.Threading;
using System.Threading.Tasks;

namespace InternetSpeedTest;

public interface IInternetSpeedTestService
{
    Task<string> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a daily task at most once per calendar day. If the last run date
    /// (persisted across executions) differs from today, executes the daily task
    /// and updates the persisted state. Returns true if the daily task executed.
    /// </summary>
    Task<bool> RunDailyIfNeededAsync(CancellationToken cancellationToken = default);
}
