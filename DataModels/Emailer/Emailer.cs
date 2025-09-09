using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels.Emailer;

public partial class Emailer : DbContext
{
    public Emailer(DbContextOptions<Emailer> options)
        : base(options)
    {
    }

    public virtual DbSet<EmailAttachment> EmailAttachments { get; set; }

    public virtual DbSet<EmailLog> EmailLogs { get; set; }

    public virtual DbSet<EmailMessage> EmailMessages { get; set; }

    public virtual DbSet<EmailRecipient> EmailRecipients { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailAttachment>(entity =>
        {
            entity.HasOne(d => d.Message).WithMany(p => p.EmailAttachments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmailAttachments_EmailMessages");
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.HasOne(d => d.MessageNavigation).WithMany(p => p.EmailLogs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmailLogs_EmailMessages");
        });

        modelBuilder.Entity<EmailRecipient>(entity =>
        {
            entity.HasOne(d => d.Message).WithMany(p => p.EmailRecipients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmailRecipients_EmailMessages");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
