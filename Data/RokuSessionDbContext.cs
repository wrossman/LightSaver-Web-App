using Microsoft.EntityFrameworkCore;
public class RokuSessionDbContext : DbContext
{
    public DbSet<RokuSession> RokuSessions { get; set; }
    public RokuSessionDbContext(DbContextOptions<RokuSessionDbContext> options)
        : base(options) { }
    public RokuSessionDbContext() { }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // this makes it so we will not be able to write a rokusession that has the same session code
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RokuSession>()
            .HasIndex(s => s.SessionCode)
            .IsUnique();
    }
}