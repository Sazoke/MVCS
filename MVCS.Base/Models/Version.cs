using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MVCS.Base.Models;

public class Version
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public EntityState ChangeType { get; set; }

    [Column(TypeName = "jsonb")]
    public List<Change> Changes { get; set; } = new();

    public string ObjectId { get; set; } = null!;

    public bool IsActual { get; set; } = true;

    public Guid? PreviousVersionId { get; set; }

    [ForeignKey(nameof(PreviousVersionId))]
    public Version? PreviousVersion { get; set; }

    public ICollection<Version> NextVersions { get; set; } = new List<Version>();
}