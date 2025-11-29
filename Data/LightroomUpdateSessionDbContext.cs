using Microsoft.EntityFrameworkCore;
public class LightroomUpdateSessionDbContext : DbContext
{
    public DbSet<LightroomUpdateSession> UpdateSessions { get; set; }

    public LightroomUpdateSessionDbContext(DbContextOptions<LightroomUpdateSessionDbContext> options)
        : base(options) { }

    public LightroomUpdateSessionDbContext()
    {
    }
}