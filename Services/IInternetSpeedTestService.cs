using System.Threading;
using System.Threading.Tasks;

namespace InternetSpeedTest;

public interface IInternetSpeedTestService
{
    Task<string> RunAsync(CancellationToken cancellationToken = default);
}
