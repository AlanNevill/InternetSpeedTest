using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels
{
    public partial class PopsContext : DbContext
    {
        public PopsContext()
        {
        }

        public PopsContext(DbContextOptions<PopsContext> options)
            : base( options )
        {
        }

        public virtual DbSet<InternetSpeed> internetSpeed { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if ( !optionsBuilder.IsConfigured )
            {
                optionsBuilder.UseSqlServer( InternetSpeedTestLib._cnStr );
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseCollation( "SQL_Latin1_General_CP1_CI_AS" );

            OnModelCreatingPartial( modelBuilder );
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
