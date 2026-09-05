using System;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// What one read had to do to bring an older document forward, and what that
/// cost the records it returned.
/// </summary>
/// <remarks>
/// <para>
/// <b>The message #92 asks for is what this type exists to make possible.</b>
/// That issue requires an unmappable value to produce the strict option and for
/// the plugin to say what it did rather than silently choosing. The strict
/// option is already what the read produces - a record whose grant is absent
/// rather than one holding a grant nobody decided - and the saying was missing,
/// because the store carries no logger and must not: it is reached on the
/// redemption path, and docs/logging.md holds every line in this plugin to the
/// inventory in docs/personal-data.md.
/// </para>
/// <para>
/// So the observation travels with the read, the way
/// <see cref="StorePermissions"/> already does and for the same reason: it is
/// one observation of one file, and a caller that has read the invitations has
/// already been handed it. Who says it out loud is
/// <see cref="Startup.LoadOnStart"/>, which has a logger and runs once.
/// </para>
/// <para>
/// <b>It carries no record and no field of one.</b> Two counts and two version
/// numbers, so a line written from it can name what happened and nothing about
/// who was invited.
/// </para>
/// </remarks>
public sealed class StoreMigration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreMigration"/> class.
    /// </summary>
    /// <param name="from">The version the document on disk declared.</param>
    /// <param name="to">The version this build reads and writes.</param>
    /// <param name="recordsWithoutAGrant">
    /// How many records came forward with no grant, because version one held a
    /// template name and no copy of what it granted.
    /// </param>
    /// <param name="accountsWithoutAnExpiry">
    /// How many account claims came forward with no expiry, because no shape
    /// before version three had anywhere to put one.
    /// </param>
    public StoreMigration(int from, int to, int recordsWithoutAGrant, int accountsWithoutAnExpiry)
    {
        From = from;
        To = to;
        RecordsWithoutAGrant = recordsWithoutAGrant;
        AccountsWithoutAnExpiry = accountsWithoutAnExpiry;
    }

    /// <summary>
    /// Gets the version the document on disk declared.
    /// </summary>
    public int From { get; }

    /// <summary>
    /// Gets the version this build reads and writes.
    /// </summary>
    public int To { get; }

    /// <summary>
    /// Gets how many records came forward carrying no grant.
    /// </summary>
    public int RecordsWithoutAGrant { get; }

    /// <summary>
    /// Gets how many account claims came forward carrying no expiry.
    /// </summary>
    /// <remarks>
    /// A count of claims rather than of records, because the expiry belongs to
    /// an account and one record can claim several. It is what an operator
    /// needs to read the second sentence of <see cref="Summary"/> as a number
    /// of accounts rather than as a number of links.
    /// </remarks>
    public int AccountsWithoutAnExpiry { get; }

    /// <summary>
    /// Gets the sentence an operator is shown, naming what was read forward and
    /// what those records can and cannot do.
    /// </summary>
    /// <remarks>
    /// It says what was done rather than that something was wrong, because
    /// nothing is: a store written by an older build is exactly the case a
    /// forward migration exists for. What it must not leave unsaid is the cost,
    /// which is that a record without a grant creates no account, so an operator
    /// meeting a refusal on an old invitation has the reason here rather than in
    /// a support thread.
    /// </remarks>
    public string Summary => string.Format(
        CultureInfo.InvariantCulture,
        "The invitation store was written by an older version of this plugin, declaring store version {0}, and was read forward to version {1}. {2} record(s) came forward with no grant, because a store older than version 2 kept a template name and no copy of what the template granted, and a grant is never guessed at. Those invitations cannot create an account and are refused if presented; mint a new invitation to replace one. {3} account claim(s) came forward with no expiry, because a store older than version 3 had nowhere on a record to keep one, and an expiry is never worked out from the invitation. Those accounts are left alone by this plugin until an operator gives one an expiry. Nothing was written to the file by this read.",
        From.ToString(CultureInfo.InvariantCulture),
        To.ToString(CultureInfo.InvariantCulture),
        RecordsWithoutAGrant.ToString(CultureInfo.InvariantCulture),
        AccountsWithoutAnExpiry.ToString(CultureInfo.InvariantCulture));
}
