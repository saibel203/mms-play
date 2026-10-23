using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MMS.Play.Infrastructure.Persistence;

public class DatabaseInitializer(ApplicationDbContext context, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeDatabaseAsync()
    {
        try
        {
            if (context.Database.IsSqlServer())
                await context.Database.MigrateAsync();

            logger.LogInformation("The database has been successfully initialized");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while trying to initialize the database");
            throw;
        }
    }
}
