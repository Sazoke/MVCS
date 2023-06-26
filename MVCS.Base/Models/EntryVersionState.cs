using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MVCS.Base.Models;

public class EntryVersionState
{
    public string Id { get; init; } = null!;

    public Version? PreviousVersion { get; set; }

    public EntityEntry Entry { get; init; } = null!;
}