using Microsoft.EntityFrameworkCore;
public class RokuSessionDbContext : DbContext
{
    public DbSet<RokuSession> Sessions { get; set; }

    public RokuSessionDbContext(DbContextOptions<RokuSessionDbContext> options)
        : base(options) { }

    public RokuSessionDbContext()
    {
    }
}