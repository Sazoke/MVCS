using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using MVCS.Base.Attributes;
using MVCS.Base.Models;
using Version = MVCS.Base.Models.Version;

namespace MVCS.Base.Common.Extensions;

internal static  class SaveChangesOverridingExtensions
{
    private static JsonSerializerOptions _options = new()
        { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
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
        var id = GetEntryId(entityEntry.Metadata, entityEntry.Entity);
        var version = new Version()
        {
            ObjectId = id,
            ChangeType = entityEntry.State,
            Changes = GetCheckedProperties(entityEntry)
                .Where(p => p.JsonValue is not null)
                .Select(p => new Change
                {
                    PropertyName = p.Name,
                    Value = p.JsonValue
                }).ToList()
        };

        return version;
    }
    
    private static Version GetDeletedVersion(MVCSDbContext context, EntityEntry entityEntry)
    {
        var id = GetEntryId(entityEntry.Metadata, entityEntry.Entity);
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
        var id = GetEntryId(entityEntry.Metadata, entityEntry.Entity);
        var versions = context.Versions.Where(v => v.ObjectId == id).ToArray();
        var previousVersion = versions.First(v => v.IsActual);
        previousVersion.IsActual = false;
        var props = GetNotModifiedProperties(previousVersion);
        var version = new Version()
        {
            ObjectId = id,
            ChangeType = entityEntry.State,
            Changes = GetCheckedProperties(entityEntry)
                .Where(p => props.ContainsKey(p.Name) && p.JsonValue != props[p.Name] ||
                            !props.ContainsKey(p.Name) && p.JsonValue is not null)
                .Select(p => new Change
                {
                    PropertyName = p.Name,
                    Value = p.JsonValue
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

    private static string GetEntryId(IEntityType entityType, object entity) => string.Join('_',
        entityType.FindPrimaryKey()!.Properties
            .Select(p => p.PropertyInfo!.GetValue(entity)!.ToString()));

    private static IEnumerable<(string Name, string? JsonValue)> GetCheckedProperties(EntityEntry entry)
    {
        var versionAttribute = typeof(VersionControlAttribute);
        var query = entry.Properties;
        if (entry.Metadata.ClrType.GetCustomAttribute(versionAttribute) is null)
            query = query.Where(p => p.Metadata.ClrType.GetCustomAttribute(versionAttribute) is not null);
        else
        {
            var ignoreAttribute = typeof(IgnoreVersionControlAttribute);
            query = query.Where(p => p.Metadata.PropertyInfo?.GetCustomAttribute(ignoreAttribute) is null);
        }

        var resultQuery =
            query.Select(q =>
                (q.Metadata.Name, q.CurrentValue is null ? null : JsonSerializer.Serialize(q.CurrentValue, _options)));
        resultQuery = resultQuery!.Concat(entry.Collections.Select(c =>
        {
            var name = c.Metadata.Name;
            var result = new List<string>();
            if (c.CurrentValue is null) return (name, JsonSerializer.Serialize(result, _options));
            foreach (var obj in c.CurrentValue)
                result.Add(GetEntryId(c.Metadata.TargetEntityType, obj));
            return (name, JsonSerializer.Serialize(result, _options));
        }))!;
        return resultQuery;
    }
}