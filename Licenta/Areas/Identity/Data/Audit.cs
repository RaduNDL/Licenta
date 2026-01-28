using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog;

namespace Licenta.Areas.Identity.Data
{
    public partial class AppDbContext
    {
        private static bool IsIdentityEntity(EntityEntry e)
        {
            var ns = e.Metadata.ClrType.Namespace ?? string.Empty;
            return ns.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal)
                   || ns.Contains(".Identity", StringComparison.Ordinal)
                   || ns.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal);
        }

        private static Dictionary<string, object?> BuildKey(EntityEntry e)
        {
            var key = new Dictionary<string, object?>();
            var pk = e.Metadata.FindPrimaryKey();
            if (pk != null)
            {
                foreach (var p in pk.Properties)
                {
                    key[p.Name] = e.Property(p.Name).CurrentValue;
                }
            }
            return key;
        }

        private static Dictionary<string, (object? Original, object? Current)> BuildChanges(EntityEntry e)
        {
            var dict = new Dictionary<string, (object? Original, object? Current)>();

            foreach (var prop in e.Properties)
            {
                if (prop.Metadata.IsPrimaryKey())
                    continue;

                var original = prop.OriginalValue;
                var current = prop.CurrentValue;

                if (e.State == EntityState.Modified && Equals(original, current))
                    continue;

                dict[prop.Metadata.Name] = (original, current);
            }

            return dict;
        }
        public override int SaveChanges()
        {
            try { AuditBeforeSave(); } catch {  }
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try { AuditBeforeSave(); } catch { }
            return base.SaveChangesAsync(cancellationToken);
        }

        private void AuditBeforeSave()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added
                                    or EntityState.Modified
                                    or EntityState.Deleted)
                .ToList();

            if (entries.Count == 0)
                return;

            foreach (var e in entries)
            {
                if (IsIdentityEntity(e))
                    continue;

                var audit = new
                {
                    AuditType = "EFChange",
                    Entity = e.Metadata.ClrType.Name,
                    State = e.State.ToString(),
                    Key = BuildKey(e),
                    Changes = BuildChanges(e),
                    Timestamp = DateTimeOffset.UtcNow
                };
                Log.ForContext("AuditType", "EFChange")
                   .ForContext("Entity", audit.Entity)
                   .ForContext("State", audit.State)
                   .ForContext("Key", audit.Key, destructureObjects: true)
                   .ForContext("Changes", audit.Changes, destructureObjects: true)
                   .Information("EF {State} on {Entity}", audit.State, audit.Entity);
            }
        }
    }
}
