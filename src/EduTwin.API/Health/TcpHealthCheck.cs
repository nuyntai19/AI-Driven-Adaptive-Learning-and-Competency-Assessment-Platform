using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Sockets;

namespace EduTwin.API.Health;

public class TcpHealthCheck : IHealthCheck
{
    private readonly string _host;
    private readonly int _port;

    public TcpHealthCheck(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(2000);

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(_host, _port, cts.Token);
            
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Propagate if the caller actively cancelled
            throw;
        }
        catch (OperationCanceledException)
        {
            // Internal timeout reached
            return HealthCheckResult.Unhealthy("TCP connection timed out after 2 seconds.");
        }
        catch (Exception ex)
        {
            // Socket or network failures
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
