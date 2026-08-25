using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// The Jellyfin server line this plugin claims, read off its own assembly.
/// </summary>
/// <remarks>
/// <para>
/// <b>The number is not written here and may not be.</b> It lives in
/// <c>build.yaml</c> as <c>targetAbi</c>, which is the manifest a catalogue
/// reads, and <c>Directory.Build.props</c> already derives the floor build from
/// that same field. A constant typed into this file would be a second copy of a
/// version, which is the drift the props file exists to refuse, and the copy
/// that went stale would be the one deciding whether the plugin runs at all.
/// So the project hands the major and minor parts to the compiler as assembly
/// metadata and this class reads them back.
/// </para>
/// <para>
/// <b>A line is the major and minor parts and nothing further.</b>
/// <c>targetAbi</c> is four parts and names the oldest server of the line the
/// plugin claims to load on; the patch and build parts move inside a line
/// without moving the interfaces this plugin reaches, so comparing them would
/// refuse a server the plugin is built for. #97 decided equality on the major
/// and minor parts, and the two halves of that decision are this reduction and
/// the comparison in <see cref="ServerLine"/>.
/// </para>
/// <para>
/// <b>An absent or unreadable declaration is refused at build time</b> by the
/// <c>RefuseUnreadableDeclaredServerLine</c> target in the project file, so a
/// build that produced an assembly cannot have produced one without this. The
/// throw below is what stands where somebody loads this type out of an assembly
/// built some other way, and it names the field rather than the symptom.
/// </para>
/// </remarks>
public static class DeclaredLine
{
    /// <summary>
    /// The metadata key the project writes the line under.
    /// </summary>
    public const string MetadataKey = "JellyfinServerLine";

    /// <summary>
    /// Gets the declared line, as <c>major.minor</c>.
    /// </summary>
    public static string Value => Of(typeof(DeclaredLine).Assembly);

    /// <summary>
    /// Reads the declared line off an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The declared line, as <c>major.minor</c>.</returns>
    /// <remarks>
    /// It takes the assembly rather than reading its own so a test can ask the
    /// question of an assembly that answers differently, which is the only way
    /// the absence below can be reached without breaking the build.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The assembly is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The assembly carries no readable declaration.
    /// </exception>
    public static string Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var declared = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(metadata => string.Equals(metadata.Key, MetadataKey, StringComparison.Ordinal))
            .Select(metadata => metadata.Value)
            .FirstOrDefault(value => IsALine(value));

        if (declared is null)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} carries no readable {1}, so this plugin cannot say which Jellyfin line it was built for. The line is derived from targetAbi in build.yaml by the project file; an assembly without it was not built by this repository's project.",
                    assembly.GetName().Name,
                    MetadataKey));
        }

        return declared;
    }

    /// <summary>
    /// Whether a declaration reads as a line.
    /// </summary>
    /// <param name="value">The declared value.</param>
    /// <returns><c>true</c> where it is two dot-separated numbers.</returns>
    /// <remarks>
    /// <para>
    /// A reading that took an empty or malformed value for a line would compare
    /// nothing against the running server and refuse every request on a server
    /// the plugin is built for, which is worse than the failure this whole file
    /// is about.
    /// </para>
    /// <para>
    /// It is public so the rule can be asserted directly. The values it has to
    /// refuse cannot be reached through <see cref="Of"/> without building an
    /// assembly that carries a malformed declaration, and a rule reachable only
    /// through a fixture nobody can construct is a rule nobody checks.
    /// </para>
    /// </remarks>
    public static bool IsALine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.');
        return parts.Length == 2
            && parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit));
    }
}
