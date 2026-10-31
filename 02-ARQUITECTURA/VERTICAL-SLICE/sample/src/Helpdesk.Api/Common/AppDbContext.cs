using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Api.Common;

/// <summary>
/// Slices use this directly: no repository layer in between.
/// (In-memory provider here; swap for SQL Server/PostgreSQL by changing the registration.)
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(t =>
        {
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).ValueGeneratedNever();
            t.Property(x => x.Title).HasMaxLength(Ticket.MaxTitleLength);
        });
    }
}
