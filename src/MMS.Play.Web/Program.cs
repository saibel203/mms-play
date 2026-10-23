using MMS.Play.Infrastructure;
using MMS.Play.Web;
using Serilog;

InfrastructureServiceExtension.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddBaseServices();
    builder.Services.AddInfrastructureService(builder.Configuration);

    var app = builder.Build();

    if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        await app.Services.InitializeDatabaseAsync();
    
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseCors();

    app.UseRouting();
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
