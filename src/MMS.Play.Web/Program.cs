using MMS.Play.Infrastructure;
using MMS.Play.Jobs;
using MMS.Play.Presentation;
using MMS.Play.Web;
using Serilog;

InfrastructureServiceExtension.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddPresentation(builder.Configuration);
    builder.Services.AddInfrastructureService(builder.Configuration);
    builder.Services.AddJobs(builder.Configuration);

    // gives running jobs time to observe the cancellation token on shutdown
    builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));

    var app = builder.Build();

    if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        await app.Services.InitializeDatabaseAsync();

    app.Services.RegisterRecurringJobs();

    // outermost, so errors from every middleware below are turned into problem details
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseRequestLogging();

    app.UseApiDocumentation();

    app.UseHttpsRedirection();
    app.UseCors();

    app.UseRouting();
    app.UseJobsDashboard();

    app.MapHealthEndpoints();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException) // thrown by design-time tools (dotnet ef) to stop the host after Build()
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
