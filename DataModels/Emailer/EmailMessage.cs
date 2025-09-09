using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels.Emailer;

public partial class EmailMessage
{
    [Key]
    public long MessageId { get; set; }

    [StringLength(320)]
    [Unicode(false)]
    public string FromAddress { get; set; } = null!;

    [StringLength(512)]
    [Unicode(false)]
    public string Subject { get; set; } = null!;

    public string BodyText { get; set; } = null!;

    public string? BodyHtml { get; set; }

    public int Priority { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ScheduledAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? SentAt { get; set; }

    [InverseProperty("Message")]
    public virtual ICollection<EmailAttachment> EmailAttachments { get; set; } = new List<EmailAttachment>();

    [InverseProperty("MessageNavigation")]
    public virtual ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();

    [InverseProperty("Message")]
    public virtual ICollection<EmailRecipient> EmailRecipients { get; set; } = new List<EmailRecipient>();
}
