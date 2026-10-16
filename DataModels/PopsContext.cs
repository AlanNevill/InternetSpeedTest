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
        public virtual DbSet<VGigaClearByDay> VGigaClearByDays { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Intentionally left blank. DbContext is configured via DI in Program.cs.
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseCollation( "SQL_Latin1_General_CP1_CI_AS" );

            modelBuilder.Entity<VGigaClearByDay>( entity =>
            {
                entity.ToView( "vGigaClearByDay" );
            } );


            OnModelCreatingPartial( modelBuilder );
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
