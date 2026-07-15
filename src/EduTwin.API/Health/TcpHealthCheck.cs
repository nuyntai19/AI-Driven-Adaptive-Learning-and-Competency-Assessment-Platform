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
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(_host, _port);
            var delayTask = Task.Delay(2000, cancellationToken);
            
            var completedTask = await Task.WhenAny(connectTask, delayTask);
            
            if (completedTask == connectTask && tcpClient.Connected)
            {
                return HealthCheckResult.Healthy();
            }
            
            return HealthCheckResult.Unhealthy("TCP connection failed or timed out.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
