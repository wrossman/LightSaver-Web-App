using Microsoft.EntityFrameworkCore;
public class RokuSessionDbContext : DbContext
{
    public DbSet<RokuSession> Sessions { get; set; }

    public RokuSessionDbContext(DbContextOptions<RokuSessionDbContext> options)
        : base(options) { }

    public RokuSessionDbContext()
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RokuSession>()
            .HasIndex(s => s.SessionCode)
            .IsUnique();
    }
}