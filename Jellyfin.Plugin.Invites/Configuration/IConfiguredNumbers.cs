namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// The three numbers an operator may set, read from wherever the plugin keeps
/// its configuration.
/// </summary>
/// <remarks>
/// A seam for the reason <see cref="IPublicAddress"/> and
/// <see cref="IConfiguredTemplates"/> are seams: the values live on
/// <see cref="Plugin.Instance"/>, which is a static the server sets, and the
/// load the server makes when it starts judges them. It reads and does not
/// write, so nothing handed this can move a number.
/// </remarks>
public interface IConfiguredNumbers
{
    /// <summary>
    /// Gets the retention period in days, or <c>null</c> where no configuration
    /// is loaded.
    /// </summary>
    int? RecordRetentionDays { get; }

    /// <summary>
    /// Gets how many presented codes one source address may have judged in an
    /// hour, or <c>null</c> where no configuration is loaded.
    /// </summary>
    int? RedemptionAttemptsPerAddressInAnHour { get; }

    /// <summary>
    /// Gets how many presented codes all sources together may have judged in a
    /// second, or <c>null</c> where no configuration is loaded.
    /// </summary>
    int? RedemptionAttemptsPerSecond { get; }
}
