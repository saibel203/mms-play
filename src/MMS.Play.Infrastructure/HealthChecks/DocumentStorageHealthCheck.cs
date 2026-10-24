using Microsoft.Extensions.Diagnostics.HealthChecks;
using MMS.Play.Contracts.InfrastructureServiceAbstractions.Storage;

namespace MMS.Play.Infrastructure.HealthChecks;

/// <summary>
/// Makes one cheap call to the storage: for cloud providers it fails on unreachable endpoints and invalid credentials.
/// </summary>
public class DocumentStorageHealthCheck(IDocumentManager documentManager) : IHealthCheck
{
    private const string ProbePath = "health-check.probe";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // storage errors are caught by the health check service and reported with FailureStatus
        await documentManager.ExistsAsync(ProbePath, cancellationToken);
        return HealthCheckResult.Healthy("Document storage is reachable");
    }
}
