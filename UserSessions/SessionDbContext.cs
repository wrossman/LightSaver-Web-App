using Microsoft.EntityFrameworkCore;
public class SessionDbContext : DbContext
{
    public DbSet<UserSession> Sessions { get; set; }

    public SessionDbContext(DbContextOptions<SessionDbContext> options)
        : base(options) { }

    public SessionDbContext()
    {
    }
}