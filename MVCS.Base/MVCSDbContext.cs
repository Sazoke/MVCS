using Microsoft.EntityFrameworkCore;
using MVCS.Base.Common.Extensions;
using Version = MVCS.Base.Models.Version;

namespace MVCS.Base;

public abstract class MVCSDbContext : DbContext
{
    public MVCSDbContext(DbContextOptions options) : base(options)
    {
        
    }

    public DbSet<Version> Versions { get; set; } = null!;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.CommitChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new())
    {
        this.CommitChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}