using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MMS.Play.Infrastructure.Persistence.Entities;

namespace MMS.Play.Infrastructure.Persistence;

// Identity tables (AspNetUsers, AspNetRoles...) live in the same database, users and their data share one transaction
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<TelegramSubscriber> TelegramSubscribers => Set<TelegramSubscriber>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonReminderDelivery> LessonReminderDeliveries => Set<LessonReminderDelivery>();
    public DbSet<SchoolSettings> SchoolSettings => Set<SchoolSettings>();
    public DbSet<LessonRescheduleRequest> LessonRescheduleRequests => Set<LessonRescheduleRequest>();
    public DbSet<ParentBroadcast> ParentBroadcasts => Set<ParentBroadcast>();
    public DbSet<ParentBroadcastDelivery> ParentBroadcastDeliveries => Set<ParentBroadcastDelivery>();
    public DbSet<PaymentReminderDelivery> PaymentReminderDeliveries => Set<PaymentReminderDelivery>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<AdminDigestDelivery> AdminDigestDeliveries => Set<AdminDigestDelivery>();
    public DbSet<Homework> Homeworks => Set<Homework>();
    public DbSet<HomeworkPractice> HomeworkPractices => Set<HomeworkPractice>();
    public DbSet<HomeworkReminderDelivery> HomeworkReminderDeliveries => Set<HomeworkReminderDelivery>();
    public DbSet<TrialRequest> TrialRequests => Set<TrialRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // configures the Identity tables, must run before our configurations
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
