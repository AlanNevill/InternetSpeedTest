using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels.Emailer;

[Index("MessageId", Name = "IX_EmailAttachments_MessageId")]
public partial class EmailAttachment
{
    [Key]
    public long AttachmentId { get; set; }

    public long MessageId { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string FileName { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string MimeType { get; set; } = null!;

    [StringLength(500)]
    [Unicode(false)]
    public string FilePath { get; set; } = null!;

    public long FileSize { get; set; }

    [ForeignKey("MessageId")]
    [InverseProperty("EmailAttachments")]
    public virtual EmailMessage Message { get; set; } = null!;
}
