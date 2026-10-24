using Microsoft.EntityFrameworkCore;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<Score> Scores => Set<Score>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
