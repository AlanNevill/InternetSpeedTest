using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels.Emailer;

[Index("MessageId", Name = "IX_EmailRecipients_MessageId")]
public partial class EmailRecipient
{
    [Key]
    public long RecipientId { get; set; }

    public long MessageId { get; set; }

    [StringLength(320)]
    [Unicode(false)]
    public string EmailAddress { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string RecipientType { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [ForeignKey("MessageId")]
    [InverseProperty("EmailRecipients")]
    public virtual EmailMessage Message { get; set; } = null!;
}
