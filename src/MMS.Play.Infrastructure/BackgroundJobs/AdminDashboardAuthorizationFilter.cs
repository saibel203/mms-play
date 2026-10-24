using Hangfire.Dashboard;
using MMS.Play.Contracts.Authorization;

namespace MMS.Play.Infrastructure.BackgroundJobs;

/// <summary>
/// Lets only admins into the Hangfire dashboard. The user comes from the authentication cookie,
/// so UseAuthentication must run before UseJobsDashboard.
/// </summary>
public class AdminDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => context.GetHttpContext().User.IsInRole(Roles.Admin);
}
