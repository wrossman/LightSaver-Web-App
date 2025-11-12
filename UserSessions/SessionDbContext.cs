using Microsoft.EntityFrameworkCore;
public class UserSessionDbContext : DbContext
{
    public DbSet<UserSession> Sessions { get; set; }

    public UserSessionDbContext(DbContextOptions<UserSessionDbContext> options)
        : base(options) { }

    public UserSessionDbContext()
    {
    }
}