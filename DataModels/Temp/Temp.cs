using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels.Temp;

public partial class Temp : DbContext
{
    public Temp(DbContextOptions<Temp> options)
        : base( options )
    {
    }

    public virtual DbSet<VGigaClearByDay> VGigaClearByDays { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VGigaClearByDay>( entity =>
        {
            entity.ToView( "vGigaClearByDay" );
        } );

        OnModelCreatingPartial( modelBuilder );
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
