using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MVCS.Base.Attributes;
using MVCS.Base.Models;
using Version = MVCS.Base.Models.Version;

namespace MVCS.Base.Common.Extensions;

internal static  class SaveChangesOverridingExtensions
{
    public static void CommitChanges(this MVCSDbContext context)
    { 
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => NeedVersion(e))
            .ToArray();
        var versions = entries.Select(e => e.State switch
            {
                EntityState.Added => GetAddedVersion(e),
                EntityState.Modified => GetModifiedVersion(context, e),
                EntityState.Deleted => GetDeletedVersion(context, e),
                _ => null
            })
            .Where(v => v is not null)
            .ToArray();
        context.Versions.AddRange(versions!);
    }

    private static bool NeedVersion(EntityEntry entry)
    {
        var type = entry.Entity.GetType();
        var versionType = typeof(VersionControlAttribute);
        return type.GetCustomAttribute(versionType) is not null ||
               type.GetProperties().Any(p => p.GetCustomAttribute(versionType) is not null);
    }

    private static Version GetAddedVersion(EntityEntry entityEntry)
    {
        var id = GetEntryId(entityEntry);
        var version = new Version()
        {
            ObjectId = id,
            ChangeType = entityEntry.State,
            Changes = GetCheckedProperties(entityEntry)
                .Where(p => p.CurrentValue != null)
                .Select(p => new Change
                {
                    PropertyName = p.Metadata.Name,
                    Value = JsonSerializer.Serialize(p.CurrentValue)
                }).ToList()
        };

        return version;
    }
    
    private static Version GetDeletedVersion(MVCSDbContext context, EntityEntry entityEntry)
    {
        var id = GetEntryId(entityEntry);
        var previousVersion = context.Versions.First(v => v.IsActual && v.ObjectId == id);
        previousVersion.IsActual = false;
        var version = new Version()
        {
            ObjectId = id,
            ChangeType = entityEntry.State,
            PreviousVersion = previousVersion
        };

        return version;
    }

    private static Version GetModifiedVersion(MVCSDbContext context, EntityEntry entityEntry)
    {
        var id = GetEntryId(entityEntry);
        var versions = context.Versions.Where(v => v.ObjectId == id).ToArray();
        var previousVersion = versions.First(v => v.IsActual);
        previousVersion.IsActual = false;
        var props = GetNotModifiedProperties(previousVersion);
        var version = new Version()
        {
            ObjectId = id,
            ChangeType = entityEntry.State,
            Changes = GetCheckedProperties(entityEntry)
                .Select(p => (p, JsonSerializer.Serialize(p.CurrentValue)))
                .Where(p => props.ContainsKey(p.p.Metadata.Name) && p.Item2 != props[p.p.Metadata.Name] ||
                            !props.ContainsKey(p.p.Metadata.Name) && p.p.CurrentValue is not null)
                .Select(p => new Change
                {
                    PropertyName = p.p.Metadata.Name,
                    Value = p.Item2
                }).ToList(),
            PreviousVersion = previousVersion
        };

        return version;
    }

    private static Dictionary<string, string?> GetNotModifiedProperties(Version actualVersion)
    {
        var result = new Dictionary<string, string?>();
        var versionCopy = actualVersion;
        while (versionCopy is not null)
        {
            foreach (var change in versionCopy.Changes
                         .Where(change => !result.ContainsKey(change.PropertyName)))
                result[change.PropertyName] = change.Value;
            versionCopy = versionCopy.PreviousVersion;
        }

        return result;
    }

    private static string GetEntryId(EntityEntry entityEntry) => string.Join('_',
        entityEntry.Properties.Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString()));

    private static IEnumerable<PropertyEntry> GetCheckedProperties(EntityEntry entry)
    {
        var versionAttribute = typeof(VersionControlAttribute);
        var query = entry.Properties;
        if (entry.Metadata.ClrType.GetCustomAttribute(versionAttribute) is null)
            return query.Where(p => p.Metadata.ClrType.GetCustomAttribute(versionAttribute) is not null);
        var ignoreAttribute = typeof(IgnoreVersionControlAttribute);
        return query.Where(p => p.Metadata.PropertyInfo?.GetCustomAttribute(ignoreAttribute) is null);
    }
}