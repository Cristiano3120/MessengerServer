using MessengerServer.AppHost.UserResources;
using Microsoft.EntityFrameworkCore;

namespace MessengerServer.AppHost
{
    public sealed class PostgresDbContext : DbContext
    {
        public DbSet<EncryptedUser> Users { get; set; }

        public PostgresDbContext() { }

        public PostgresDbContext(DbContextOptions<PostgresDbContext> dbContextOptions) 
            : base(dbContextOptions) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) 
            => optionsBuilder.UseNpgsql("Host=localhost;Database=Messenger;Username=postgres;Password=Cristiano2007!");
    }
}
