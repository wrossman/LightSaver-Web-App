using Microsoft.EntityFrameworkCore;

public class GlobalImageStoreDbContext : DbContext
{
    public DbSet<ImageShare> Resources { get; set; }

    public GlobalImageStoreDbContext(DbContextOptions<GlobalImageStoreDbContext> options) : base(options) { }

    public GlobalImageStoreDbContext() { }

}