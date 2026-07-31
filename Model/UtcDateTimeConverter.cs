using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace mps.Model
{
    /// <summary>
    /// Normalizes every <see cref="DateTime"/> that crosses the EF boundary to <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <remarks>
    /// SQLite has no native date type - it stores whatever text EF hands it and reads it back as
    /// Kind-less <see cref="DateTimeKind.Unspecified"/>. Without this, <c>System.Text.Json</c> omits
    /// the trailing "Z" on serialization, and the Angular client (which parses with the browser's
    /// <c>Date</c> constructor) silently reinterprets the value as local time.
    /// <para>
    /// On write, only <see cref="DateTimeKind.Local"/> is converted via
    /// <see cref="DateTime.ToUniversalTime"/> (production/test seeds built from
    /// <c>DateTime.Now</c>). A <see cref="DateTimeKind.Unspecified"/> value is passed through
    /// unchanged rather than run through <c>ToUniversalTime()</c>, which would apply the
    /// *server's* local offset - exactly the machine-dependent behaviour this converter exists to
    /// remove. Every value actually written by this app is already <see cref="DateTimeKind.Utc"/>
    /// (all domain code uses <c>DateTime.UtcNow</c>), so in practice this branch is a safety net,
    /// not the common path.
    /// </para>
    /// <para>
    /// On read, the value is stamped <see cref="DateTimeKind.Utc"/> so every round trip comes back
    /// with a Kind the caller can rely on.
    /// </para>
    /// </remarks>
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(
                v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }
}
