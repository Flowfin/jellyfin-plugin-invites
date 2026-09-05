using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// Where invitation records live, and what is done to the file that holds them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A file of the plugin's own, in the plugin's own data directory.</b> Three
/// places were open and two were ruled out by properties the store has to have
/// rather than by preference.
/// </para>
/// <para>
/// The server's own database is ruled out by having to be readable by a unit
/// test without a server. A store there can only be exercised by standing up
/// enough of a server to have one, which is the parallel apparatus nobody
/// maintains, and it couples this plugin to internals that move between server
/// lines.
/// </para>
/// <para>
/// The plugin configuration file is ruled out by having to be written
/// atomically. This plugin does not own that write: the dashboard rewrites the
/// configuration wholesale on save, so a save landing between a read and a
/// write-back discards the write, and no locking inside this plugin can prevent
/// it because the other writer is not this plugin. It is also the file an
/// operator pastes into a support thread, which is the last place keyed hashes
/// belong.
/// </para>
/// <para>
/// The other two properties the store has to have are satisfied by this choice
/// rather than deciding it. Holding a keyed hash and never the code is a
/// property of what is written, and it is <see cref="Invitation"/> that carries
/// it. Surviving a kill mid-write is a property of how it is written, and it is
/// this type's: see <see cref="Write"/>.
/// </para>
/// <para>
/// <b>The means.</b> A JSON document through <c>System.Text.Json</c>, which the
/// runtime already carries, so the store adds no dependency, no new format and
/// nothing the existing suite cannot read. The document is small by
/// construction, because the number of live invitations is bounded, so nothing
/// here needs a database engine's answers to questions this plugin does not
/// ask.
/// </para>
/// <para>
/// <b>The stored shape is this type's and not <see cref="Invitation"/>'s.</b>
/// The record type carries no serialization attributes and gains none from
/// here. What reaches disk is the private shape below, so the field names on
/// disk are a decision the store makes and can version, and a rename inside the
/// record type does not silently rewrite everybody's store file.
/// </para>
/// <para>
/// <b>What this type does not do, so a reader does not assume it.</b> Nothing
/// here serialises two writers. The lock covering read, decide and write is the
/// other half of the issue that made the write survive a kill, and it belongs
/// to whoever redeems rather than to the file. The document carries a version
/// and refuses one it does not know.
/// </para>
/// <para>
/// <b>Two shapes have existed and one migration is written, forward only.</b>
/// Version one carried the template's name and no grant; version two carries
/// the grant #61 copies at minting beside the name. A version one document is
/// read through the shape it was written in, kept here as its own class rather
/// than as the current one with a member left optional, and each record is
/// migrated into a record whose grant is absent. That is the strict answer
/// #92 asks a migration for: the label cannot be resolved into a grant at read
/// time without being the lookup #61 forbids, and a grant invented for it
/// would widen what a record minted under version one is worth from nothing
/// to something nobody decided. The migration writes nothing back; the next
/// write, whatever causes it, is in the current shape.
/// </para>
/// </remarks>
public sealed class InvitationStore
{
    /// <summary>
    /// The name of the file inside the directory this store was given.
    /// </summary>
    public const string FileName = "invitations.json";

    /// <summary>
    /// The name of the file a write is built up in before it becomes the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It sits in the same directory as the store rather than in a temporary
    /// one, because the last step is a rename over the store and a rename only
    /// replaces a file within one filesystem. A temporary directory elsewhere
    /// turns that step into a copy, which is the truncate-and-rewrite this
    /// exists to remove, moved one level down.
    /// </para>
    /// <para>
    /// It is a constant rather than a random name so a store left holding one
    /// is readable as a write that did not finish, and so the suite can arrange
    /// that failure.
    /// </para>
    /// </remarks>
    public const string WritingFileName = FileName + ".writing";

    /// <summary>
    /// The shape of the document this build writes and the newest one it reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One number for the whole document rather than one per record. What a
    /// migration has to reason about is the file it met, and a file whose
    /// records disagree about their shape is a state no writer here can produce
    /// and no migration could sensibly repair.
    /// </para>
    /// <para>
    /// It is read before any record is parsed, which is the ordering that makes
    /// it worth having: a version discovered after a parser has already read a
    /// newer file's records arrives too late to refuse anything, and by then the
    /// fields it did not understand are already defaults.
    /// </para>
    /// <para>
    /// Two is the shape that carries the copied grant on every record, under
    /// #61. One is the shape before it, which carried the template's name and
    /// nothing else, and is still read: see <see cref="StoredInvitationOfVersionOne"/>.
    /// </para>
    /// </remarks>
    public const int Version = 2;

    /// <summary>
    /// The version before <see cref="Version"/>, which this build reads and
    /// never writes.
    /// </summary>
    public const int VersionWithoutAGrant = 1;

    /// <summary>
    /// The mode the store file is created with on a platform that has file
    /// modes: read and write for the owner, nothing for the group and nothing
    /// for anybody else.
    /// </summary>
    /// <remarks>
    /// Every reader of this file learns who was invited and by whom, and the
    /// stored keyed hashes are the input to an offline guessing attack for
    /// anybody who also holds the key. Neither is worth a mode bit.
    /// </remarks>
    public const UnixFileMode CreatedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly JsonSerializerOptions _format = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitationStore"/> class
    /// over a directory.
    /// </summary>
    /// <param name="directory">
    /// The directory the store file sits in. It is created when it is missing,
    /// and it is a parameter rather than a lookup so the suite can point one at
    /// a directory it owns without a server anywhere near it.
    /// </param>
    /// <exception cref="ArgumentException">The directory is null or blank.</exception>
    public InvitationStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("A store needs a directory to sit in.", nameof(directory));
        }

        _directory = directory;
        Path = System.IO.Path.Combine(directory, FileName);
        WritingPath = System.IO.Path.Combine(directory, WritingFileName);
    }

    /// <summary>
    /// Gets the full path of the file this store reads and writes.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the full path of the file a write is built up in.
    /// </summary>
    /// <remarks>
    /// Nothing reads it. It is exposed so a caller inspecting a data directory,
    /// and the suite arranging a write that cannot finish, name the same path
    /// this class does rather than rebuilding it from the constant and hoping.
    /// </remarks>
    public string WritingPath { get; }

    /// <summary>
    /// The store belonging to a plugin, in that plugin's own data directory.
    /// </summary>
    /// <param name="plugin">The plugin whose data directory holds the store.</param>
    /// <returns>A store over that directory.</returns>
    /// <remarks>
    /// This is where the location decision above stops being prose. The
    /// directory is the one the server hands this plugin for its own data, so
    /// nothing here picks a path out of the air and nothing writes beside the
    /// configuration file.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The plugin is null.</exception>
    public static InvitationStore For(Plugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        return new InvitationStore(plugin.DataFolderPath);
    }

    /// <summary>
    /// Whether a mode carries anything beyond the ones the store creates its
    /// file with.
    /// </summary>
    /// <param name="found">The mode read off a file.</param>
    /// <returns><c>true</c> where at least one further bit is set.</returns>
    /// <remarks>
    /// It is wider rather than different, so a store an operator has tightened
    /// further, to read-only for the owner, is not reported at them as a
    /// problem they caused. Separated from the reading of the mode so that the
    /// comparison itself is exercised on every platform the suite runs on,
    /// including the one where no mode can be read at all.
    /// </remarks>
    public static bool WiderThanCreated(UnixFileMode found) => (found & ~CreatedMode) != 0;

    /// <summary>
    /// Reads the store, with what its file permissions were found to be.
    /// </summary>
    /// <returns>
    /// The invitations the file holds, and the permission finding. A store file
    /// that is not there yet reads as no invitations, because a server that has
    /// never minted anything and a server whose store was deleted are the same
    /// state to everything downstream and neither is an error.
    /// </returns>
    /// <exception cref="JsonException">
    /// The file is there and is not the document this store writes. It is
    /// raised rather than swallowed: an unreadable store answered as an empty
    /// one is a server that has quietly forgotten every live invitation. That
    /// covers a file that is not JSON, a file cut off part way through one, and
    /// a document that parses and carries no invitation list. A document
    /// carrying an empty list is a store holding nothing and reads as one.
    /// It also covers a record that leaves out a member whose absence would be
    /// read as a grant, which today is the revocation pair: see
    /// <see cref="StoredInvitation.RevokedAt"/> for which members those are and
    /// why the rest are not among them.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The document is this store's and one of the records in it is not an
    /// invitation. It reaches the caller rather than being caught here, so a
    /// store with one damaged record raises instead of returning the others: a
    /// partial load of invitations is a load where some revocations are
    /// missing, and nothing downstream can tell it from a complete one.
    /// </exception>
    public StoreContents Read()
    {
        var permissions = InspectPermissions();
        if (!File.Exists(Path))
        {
            return new StoreContents(ImmutableArray<Invitation>.Empty, permissions);
        }

        var text = File.ReadAllText(Path);
        var declared = RefuseAVersionThisBuildDoesNotRead(text);

        // The shape is chosen by the version the document declares and by
        // nothing in the records, because the version is the one field read
        // before any record is parsed. A document declaring the older shape
        // while carrying a member from the newer one is read as the shape it
        // declares, which loses the member rather than trusting it.
        if (declared > VersionWithoutAGrant)
        {
            return new StoreContents(ReadTheCurrentShape(text), permissions);
        }

        // Read forward, and the observation travels back with the records
        // rather than being written to a log here. #92 asks that a value which
        // cannot be mapped produce the strict option AND that the plugin say
        // what it did; the strict option is what MigrateFromVersionOne
        // produces, and the saying is the caller's, because nothing on this
        // path may hold a logger.
        var migrated = MigrateFromVersionOne(text);
        var withoutAGrant = migrated.Count(record => record.Template is null);

        return new StoreContents(
            migrated,
            permissions,
            new StoreMigration(declared, Version, withoutAGrant));
    }

    /// <summary>
    /// Reads a document in the shape this build writes.
    /// </summary>
    /// <param name="text">The document as it was read off disk.</param>
    /// <returns>The records it holds.</returns>
    private ImmutableArray<Invitation> ReadTheCurrentShape(string text)
    {
        var document = JsonSerializer.Deserialize<StoredDocument>(text, _format);
        var stored = document?.Invitations ?? throw NoInvitationList();

        var invitations = ImmutableArray.CreateBuilder<Invitation>(stored.Count);
        foreach (var record in stored)
        {
            invitations.Add(record.ToInvitation());
        }

        return invitations.ToImmutable();
    }

    /// <summary>
    /// Reads a version one document and migrates every record forward.
    /// </summary>
    /// <param name="text">The document as it was read off disk.</param>
    /// <returns>The records it holds, each with its grant absent.</returns>
    /// <remarks>
    /// <para>
    /// The one migration this store has. Version one carried the template's
    /// name and no grant, and there is no honest value to migrate that into:
    /// resolving the name against the configuration now would be the lookup at
    /// read time that #61 forbids, and any grant written in without a name to
    /// resolve would be one nobody decided, which #92 refuses a migration to
    /// do. So the record comes back with every field it had and
    /// <see cref="Invitation.Template"/> absent, which is what that member says
    /// an absence means: minted before the copy existed, and able to create
    /// nothing.
    /// </para>
    /// <para>
    /// The two revocation members are as required here as in the current
    /// shape, for #93's reason, and the same fixture proves it against this
    /// reader rather than only against the other.
    /// </para>
    /// </remarks>
    private ImmutableArray<Invitation> MigrateFromVersionOne(string text)
    {
        var document = JsonSerializer.Deserialize<StoredDocumentOfVersionOne>(text, _format);
        var stored = document?.Invitations ?? throw NoInvitationList();

        var invitations = ImmutableArray.CreateBuilder<Invitation>(stored.Count);
        foreach (var record in stored)
        {
            invitations.Add(record.ToInvitationWithNoGrant());
        }

        return invitations.ToImmutable();
    }

    /// <summary>
    /// The refusal for a file that parses as JSON and carries no invitation
    /// list.
    /// </summary>
    /// <returns>The exception to throw.</returns>
    /// <remarks>
    /// Such a file is not this store's document, whether it came out as a bare
    /// null or as an object whose one member is spelled some other way.
    /// Answering that as no invitations is the quiet form of the failure the
    /// read refuses loudly: nothing is missing from the store, the store is
    /// simply gone, and every redemption after it is a refusal nobody can
    /// explain. A store holding nothing is written as an empty list and still
    /// reads as one.
    /// </remarks>
    private JsonException NoInvitationList()
    {
        return new JsonException(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} is there and carries no invitation list, so it is not this store's document. A store that holds no invitations carries an empty list.",
                Path));
    }

    /// <summary>
    /// Writes the store, creating the directory and the file when they are not
    /// there.
    /// </summary>
    /// <param name="invitations">Every invitation the store is to hold.</param>
    /// <returns>
    /// What the file permissions were found to be once the write had finished,
    /// so a caller that only ever writes still learns about a store somebody
    /// else widened.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A whole store at a time, and never a field or a record at a time. The
    /// record type is immutable for this reason: a redemption, a revocation and
    /// the retention sweep each produce records and hand the whole set here.
    /// </para>
    /// <para>
    /// <b>What is claimed about the write, and what is not.</b> The store file
    /// is never opened for writing, so nothing that happens during a write can
    /// shorten or half-fill it, and what is on disk at any instant is either
    /// exactly what was there before or exactly what was handed in. What is not
    /// claimed is that the final move is atomic on every filesystem: on a POSIX
    /// filesystem it is a rename, on Windows it is a replace, and whether either
    /// survives a power loss indivisibly is a property of the filesystem
    /// underneath rather than of the call made here. That has not been measured.
    /// </para>
    /// <para>
    /// <b>One writer at a time is the caller's.</b> Nothing here serialises two
    /// writers, and two processes writing one store are two moves racing, where
    /// the loser's records are lost rather than mixed in. The lock covering
    /// read, decide and write belongs to whoever redeems, and that caller exists:
    /// <see cref="Invitations.InvitationOperations.Reserve"/> takes the monitor,
    /// reads, asks the decision and writes the use before letting go, which is
    /// #40 inside one process. Two servers over one store is #96 and is not
    /// covered by that monitor at all.
    /// </para>
    /// <para>
    /// A write that fails leaves its unfinished file behind under
    /// <see cref="WritingPath"/>. It is not cleaned up, because the failure that
    /// leaves one is the failure that also stops any cleanup from running, so
    /// tidying here would only handle the case nobody minds. Nothing reads it,
    /// the next write overwrites it, and a store directory holding one is
    /// readable as a write that did not finish.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The invitations are null.</exception>
    public StorePermissions Write(IReadOnlyCollection<Invitation> invitations)
    {
        ArgumentNullException.ThrowIfNull(invitations);

        CreateDirectory();

        var stored = new List<StoredInvitation>(invitations.Count);
        foreach (var invitation in invitations)
        {
            stored.Add(StoredInvitation.From(invitation));
        }

        var json = JsonSerializer.Serialize(
            new StoredDocument { Version = Version, Invitations = stored },
            _format);

        // The mode is set as the file is created rather than afterwards, so
        // there is no instant in which the store exists and is readable by
        // everybody. On a platform without file modes the option is not passed
        // at all: asking for one there throws rather than being ignored.
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = CreatedMode;
        }

        // The whole document is built up beside the store and then moved over
        // it. The store is never opened for writing, so there is no moment in
        // which it is shorter than it was: a process that dies anywhere in the
        // lines below leaves the old store exactly as it was, and one that dies
        // after the move leaves the new one. #40 is why, and what it prevents is
        // the truncate-then-rewrite this replaced, where dying between the two
        // leaves an empty file where every live invitation used to be.
        using (var file = new FileStream(WritingPath, options))
        using (var writer = new StreamWriter(file))
        {
            writer.Write(json);
            writer.Flush();

            // The bytes are pushed to the device before the move, so a machine
            // losing power just after it does not come back to a store entry
            // pointing at a file whose contents never arrived.
            file.Flush(true);
        }

        // A store that is already there keeps the mode it has, which is the rule
        // a read follows too: a mode an operator widened is reported and never
        // repaired, and one they tightened is theirs. Without this the move
        // would carry the created mode over the top of theirs and quietly
        // repair it, which is a decision this file already made the other way.
        // Only the first write of a store, where there is nothing to carry,
        // leaves the created mode in place.
        if (!OperatingSystem.IsWindows() && File.Exists(Path))
        {
            File.SetUnixFileMode(WritingPath, File.GetUnixFileMode(Path));
        }

        File.Move(WritingPath, Path, overwrite: true);

        return InspectPermissions();
    }

    /// <summary>
    /// Reads the version off the document and refuses one this build does not
    /// know.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The version is taken off the text before anything else is made of it, so
    /// the refusal happens before a record is parsed rather than after. That is
    /// a separate pass over the same string, which costs a parse of a file
    /// bounded by the number of live invitations and buys the ordering the
    /// refusal is worthless without.
    /// </para>
    /// <para>
    /// A document with no version at all is read as version 1. Exactly one
    /// shape had been written when the version arrived, because nothing had
    /// been released, so an absent version was not ambiguous and reading it as
    /// the shape it certainly was cost nobody a store. A second shape exists
    /// now, so that reading is a migration rather than a default, and it is
    /// the version one migration the caller makes: a document with no version
    /// carries no grant, whatever else it carries.
    /// </para>
    /// <para>
    /// It refuses and never writes. A plugin that will not load has to leave the
    /// file exactly as it found it, or a downgrade somebody could recover from
    /// by putting the newer plugin back becomes one they cannot.
    /// </para>
    /// <para>
    /// An older version is not refused. It is handed back so the caller reads
    /// the document through the shape it declares, and a version this build
    /// does not read is the only refusal.
    /// </para>
    /// </remarks>
    /// <param name="text">The document as it was read off disk.</param>
    /// <returns>
    /// The version the document declares, or <see cref="VersionWithoutAGrant"/>
    /// where it declares none this can read.
    /// </returns>
    /// <exception cref="StoreVersionRefusedException">
    /// The document declares a version newer than <see cref="Version"/>.
    /// </exception>
    private int RefuseAVersionThisBuildDoesNotRead(string text)
    {
        int declared;

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("version", out var version)
                || !version.TryGetInt32(out declared))
            {
                // No readable version. Whether the document is this store's at
                // all is the next step's question, and it says so with the file
                // named; answering it here would give two different messages for
                // one broken file depending on which check noticed first.
                return VersionWithoutAGrant;
            }
        }
        catch (JsonException)
        {
            // A file that is not JSON has no version to read and is not this
            // step's to report. Swallowed narrowly and only here: the next line
            // of the caller parses the same text and raises on it, so the file
            // is refused either way and with one message rather than two that
            // depend on which pass noticed first.
            return VersionWithoutAGrant;
        }

        if (declared > Version)
        {
            throw new StoreVersionRefusedException(Path, declared, Version);
        }

        return declared;
    }

    private void CreateDirectory()
    {
        if (Directory.Exists(_directory))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directory);
            return;
        }

        // The owner also needs to enter the directory, which read and write
        // alone do not allow, so the execute bit is the difference between this
        // mode and the file's.
        Directory.CreateDirectory(_directory, CreatedMode | UnixFileMode.UserExecute);
    }

    private StorePermissions InspectPermissions()
    {
        // Whether the file is there is asked before which platform this is,
        // because it is a fact about the file rather than about file modes and
        // it is the same fact everywhere. Asking the platform first would
        // answer "not checked here" for a store that does not exist, which
        // reads as a check that was prevented rather than one with nothing to
        // look at.
        if (!File.Exists(Path))
        {
            return new StorePermissions(
                StorePermissionState.NoStoreFile,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "There is no {0} yet, so there is no mode to read.",
                    Path));
        }

        if (OperatingSystem.IsWindows())
        {
            return new StorePermissions(
                StorePermissionState.NotCheckedOnThisPlatform,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The mode of {0} was not read. File modes are a POSIX concept and this platform has none; what protects the file here is the access control the data directory already carries, which this plugin does not set and does not understand well enough to judge.",
                    Path));
        }

        var found = File.GetUnixFileMode(Path);
        if (!WiderThanCreated(found))
        {
            return new StorePermissions(
                StorePermissionState.AsWritten,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is {1}, which is no wider than the {2} it is created with.",
                    Path,
                    found,
                    CreatedMode));
        }

        // Reported and not repaired. Tightening a mode silently changes
        // something an operator may have set deliberately, and refusing to
        // start turns a permissions nit into an outage of the redemption path,
        // which is the failure an operator actually feels. What is left is
        // saying so, once, with the file and the mode in the sentence.
        return new StorePermissions(
            StorePermissionState.WiderThanWritten,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} is {1}, which is wider than the {2} it is created with. Every reader of it learns who was invited and by whom. This is reported and not repaired: the mode is left as it was found.",
                Path,
                found,
                CreatedMode));
    }

    /// <summary>
    /// The document as it sits on disk.
    /// </summary>
    private sealed class StoredDocument
    {
        /// <summary>
        /// Gets or sets the shape this document is in.
        /// </summary>
        /// <remarks>
        /// It is nullable so a document written before the version existed
        /// parses rather than raising here. What an absent version means is
        /// decided in one place, and it is not this one.
        /// </remarks>
        public int? Version { get; set; }

        /// <summary>
        /// Gets or sets the invitations the file holds.
        /// </summary>
        public List<StoredInvitation>? Invitations { get; set; }
    }

    /// <summary>
    /// One invitation as it sits on disk.
    /// </summary>
    /// <remarks>
    /// The bytes of the keyed hash travel as base64 rather than as a JSON array
    /// of numbers, which is shorter and is what a person reading the file
    /// expects an opaque value to look like. There is deliberately no member
    /// here that the code could be recovered from, and the record type this is
    /// built from has none either.
    /// </remarks>
    private sealed class StoredInvitation
    {
        /// <summary>Gets or sets the identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the keyed hash, base64.</summary>
        public string? CodeHash { get; set; }

        /// <summary>Gets or sets the operator who minted it.</summary>
        public Guid MintedBy { get; set; }

        /// <summary>Gets or sets when it was minted.</summary>
        public DateTimeOffset MintedAt { get; set; }

        /// <summary>Gets or sets when it stops being usable.</summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>Gets or sets how many accounts it was good for.</summary>
        public int UsesGranted { get; set; }

        /// <summary>Gets or sets how many are left.</summary>
        public int UsesRemaining { get; set; }

        /// <summary>Gets or sets when it was revoked, or null.</summary>
        /// <remarks>
        /// <para>
        /// <b>Required, and it is the one member here whose absence would be
        /// read as a grant.</b> A document that does not carry this member
        /// parses with the member at its default, which is null, which is an
        /// invitation nobody revoked. So a store written by a build that spelled
        /// revocation some other way, or a file an editor mangled, would come
        /// back with every revocation silently undone and the redemption path
        /// would honour a link an operator had taken away. Required here means
        /// the member has to be present in the document; carrying it as null is
        /// how a live invitation says it was never revoked, and that still
        /// reads.
        /// </para>
        /// <para>
        /// This is #93's rule and it is per member rather than per document:
        /// absence is an error wherever the default would be more permissive
        /// than the value it replaces could have been. The other members here
        /// are not required, and that is a decision rather than an omission. An
        /// absent expiry defaults to the start of the calendar, which is
        /// expired; an absent count of uses remaining defaults to zero, which is
        /// spent; an absent keyed hash reads as no bytes, which no presented
        /// code matches. Each of those loses an invitation rather than granting
        /// an account, which is the direction #53 asks for.
        /// </para>
        /// <para>
        /// A member added to this shape later gets the same question asked of
        /// it by <c>StoreFieldAbsenceTests</c> rather than by somebody
        /// remembering this paragraph: the property there removes each member
        /// of a written document in turn and refuses any removal that turns an
        /// unusable invitation into a usable one.
        /// </para>
        /// </remarks>
        [JsonRequired]
        public DateTimeOffset? RevokedAt { get; set; }

        /// <summary>Gets or sets the operator who revoked it, or null.</summary>
        /// <remarks>
        /// Written and read beside <see cref="RevokedAt"/> rather than
        /// separately. A file carrying one of the two is refused by the record
        /// type as it is built, which is where that rule lives. Required for the
        /// same reason as its pair: a document carrying the instant and not the
        /// operator would be refused by the record type, and one carrying
        /// neither would read as an invitation nobody revoked.
        /// </remarks>
        [JsonRequired]
        public Guid? RevokedBy { get; set; }

        /// <summary>Gets or sets the template the operator picked.</summary>
        public string? TemplateLabel { get; set; }

        /// <summary>Gets or sets the grant copied at minting, or null.</summary>
        /// <remarks>
        /// <para>
        /// Required to be present, and allowed to be null. A document written
        /// by this build always carries the member: the mint refuses to write
        /// a record without a grant, and a record migrated out of version one
        /// carries the member as null, which is what an absent grant looks like
        /// once such a record is written back by a revocation or a sweep.
        /// </para>
        /// <para>
        /// Absent rather than null is refused, by #93's per-member rule read
        /// from the other side. An absent grant is not more permissive than the
        /// value it stands in for, because nothing is created from a record
        /// with none; what it is, is a document this build did not write, and
        /// a document in the current shape that leaves out a member the writer
        /// always emits is one somebody edited.
        /// </para>
        /// </remarks>
        [JsonRequired]
        public StoredTemplate? Template { get; set; }

        /// <summary>Gets or sets the accounts it created.</summary>
        public List<Guid>? AccountsProduced { get; set; }

        public static StoredInvitation From(Invitation invitation)
        {
            return new StoredInvitation
            {
                Id = invitation.Id,
                CodeHash = Convert.ToBase64String(invitation.CodeHash.AsSpan()),
                MintedBy = invitation.MintedBy,
                MintedAt = invitation.MintedAt,
                ExpiresAt = invitation.ExpiresAt,
                UsesGranted = invitation.UsesGranted,
                UsesRemaining = invitation.UsesRemaining,
                RevokedAt = invitation.RevokedAt,
                RevokedBy = invitation.RevokedBy,
                TemplateLabel = invitation.TemplateLabel,
                Template = invitation.Template is null ? null : StoredTemplate.From(invitation.Template),
                AccountsProduced = new List<Guid>(invitation.AccountsProduced),
            };
        }

        public Invitation ToInvitation()
        {
            return new Invitation(
                Id,
                ImmutableArray.Create(Convert.FromBase64String(CodeHash ?? string.Empty)),
                MintedBy,
                MintedAt,
                ExpiresAt,
                UsesGranted,
                UsesRemaining,
                RevokedAt,
                RevokedBy,
                TemplateLabel ?? string.Empty,
                Template?.ToTemplate(),
                AccountsProduced is null ? ImmutableArray<Guid>.Empty : ImmutableArray.CreateRange(AccountsProduced));
        }
    }

    /// <summary>
    /// The grant an invitation carries, as it sits on disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One member per grant of <see cref="AccountTemplate"/>, under the store's
    /// own names, so the record type's spelling can move without rewriting
    /// every file on every server.
    /// </para>
    /// <para>
    /// <b>Every member is required, and that is #93's rule applied to a nested
    /// shape.</b> A permission left out of the file would read as closed, which
    /// loses nothing, and a ceiling left out would read as no ceiling, which
    /// grants an account more than the template it was minted from allowed.
    /// Rather than decide member by member which absences widen, the whole
    /// grant is read whole or refused: this build writes every member on every
    /// record, so a document missing one is a document this build did not
    /// write, and a grant read out of such a document is a grant somebody
    /// else wrote.
    /// </para>
    /// </remarks>
    private sealed class StoredTemplate
    {
        /// <summary>Gets or sets the libraries the account may see.</summary>
        [JsonRequired]
        public List<Guid>? Libraries { get; set; }

        /// <summary>Gets or sets a value indicating whether it may download.</summary>
        [JsonRequired]
        public bool MayDownload { get; set; }

        /// <summary>Gets or sets a value indicating whether it may play from outside the network.</summary>
        [JsonRequired]
        public bool MayPlayFromOutsideTheNetwork { get; set; }

        /// <summary>Gets or sets a value indicating whether it may manage anything at all.</summary>
        [JsonRequired]
        public bool MayManage { get; set; }

        /// <summary>Gets or sets a value indicating whether it may drive somebody else's session.</summary>
        [JsonRequired]
        public bool MayControlOtherSessions { get; set; }

        /// <summary>Gets or sets a value indicating whether it may watch live television.</summary>
        [JsonRequired]
        public bool MayWatchLiveTelevision { get; set; }

        /// <summary>Gets or sets a value indicating whether it may schedule and remove recordings.</summary>
        [JsonRequired]
        public bool MayManageLiveTelevision { get; set; }

        /// <summary>Gets or sets a value indicating whether it may delete media from the server.</summary>
        [JsonRequired]
        public bool MayDeleteContent { get; set; }

        /// <summary>Gets or sets a value indicating whether it may edit collections.</summary>
        [JsonRequired]
        public bool MayManageCollections { get; set; }

        /// <summary>Gets or sets a value indicating whether it may fetch and remove subtitles.</summary>
        [JsonRequired]
        public bool MayManageSubtitles { get; set; }

        /// <summary>Gets or sets a value indicating whether it may fetch and remove lyrics.</summary>
        [JsonRequired]
        public bool MayManageLyrics { get; set; }

        /// <summary>Gets or sets a value indicating whether it may change its own display preferences.</summary>
        [JsonRequired]
        public bool MayChangeItsOwnPreferences { get; set; }

        /// <summary>Gets or sets the remote bitrate ceiling, or null for none.</summary>
        [JsonRequired]
        public int? RemoteBitrateCeiling { get; set; }

        /// <summary>Gets or sets how many streams at once, or null for no ceiling.</summary>
        [JsonRequired]
        public int? SimultaneousStreamCeiling { get; set; }

        /// <summary>Gets or sets the parental rating ceiling, or null for none.</summary>
        [JsonRequired]
        public int? ParentalRatingCeiling { get; set; }

        /// <summary>Gets or sets the server's policy fields the template leaves alone.</summary>
        [JsonRequired]
        public List<string>? ServerDefaultsLeftAlone { get; set; }

        public static StoredTemplate From(AccountTemplate template)
        {
            return new StoredTemplate
            {
                Libraries = new List<Guid>(template.Libraries),
                MayDownload = template.MayDownload,
                MayPlayFromOutsideTheNetwork = template.MayPlayFromOutsideTheNetwork,
                MayManage = template.MayManage,
                MayControlOtherSessions = template.MayControlOtherSessions,
                MayWatchLiveTelevision = template.MayWatchLiveTelevision,
                MayManageLiveTelevision = template.MayManageLiveTelevision,
                MayDeleteContent = template.MayDeleteContent,
                MayManageCollections = template.MayManageCollections,
                MayManageSubtitles = template.MayManageSubtitles,
                MayManageLyrics = template.MayManageLyrics,
                MayChangeItsOwnPreferences = template.MayChangeItsOwnPreferences,
                RemoteBitrateCeiling = template.RemoteBitrateCeiling,
                SimultaneousStreamCeiling = template.SimultaneousStreamCeiling,
                ParentalRatingCeiling = template.ParentalRatingCeiling,
                ServerDefaultsLeftAlone = new List<string>(template.ServerDefaultsLeftAlone),
            };
        }

        public AccountTemplate ToTemplate()
        {
            // The two lists are required members, so a null here is a member
            // that was present and carried null, which the record type refuses
            // as nobody having decided. It is handed on as the uninitialized
            // list for exactly that refusal rather than being read as empty.
            return new AccountTemplate(
                libraries: Libraries is null ? default : ImmutableArray.CreateRange(Libraries),
                mayDownload: MayDownload,
                mayPlayFromOutsideTheNetwork: MayPlayFromOutsideTheNetwork,
                mayManage: MayManage,
                mayControlOtherSessions: MayControlOtherSessions,
                mayWatchLiveTelevision: MayWatchLiveTelevision,
                mayManageLiveTelevision: MayManageLiveTelevision,
                mayDeleteContent: MayDeleteContent,
                mayManageCollections: MayManageCollections,
                mayManageSubtitles: MayManageSubtitles,
                mayManageLyrics: MayManageLyrics,
                mayChangeItsOwnPreferences: MayChangeItsOwnPreferences,
                remoteBitrateCeiling: RemoteBitrateCeiling,
                simultaneousStreamCeiling: SimultaneousStreamCeiling,
                parentalRatingCeiling: ParentalRatingCeiling,
                serverDefaultsLeftAlone: ServerDefaultsLeftAlone is null ? default : ImmutableArray.CreateRange(ServerDefaultsLeftAlone));
        }
    }

    /// <summary>
    /// The document as version one wrote it.
    /// </summary>
    /// <remarks>
    /// Kept as its own class rather than read through the current one with the
    /// grant left optional, so the current shape can require the member and
    /// this one can lack it, and so a member added to the current shape later
    /// does not silently become readable out of a version one file.
    /// </remarks>
    private sealed class StoredDocumentOfVersionOne
    {
        /// <summary>Gets or sets the shape this document is in.</summary>
        public int? Version { get; set; }

        /// <summary>Gets or sets the invitations the file holds.</summary>
        public List<StoredInvitationOfVersionOne>? Invitations { get; set; }
    }

    /// <summary>
    /// One invitation as version one wrote it: the template's name and no
    /// grant.
    /// </summary>
    /// <remarks>
    /// The members are the ones that shape carried and no other. The two
    /// revocation members are required for the reason
    /// <see cref="StoredInvitation.RevokedAt"/> gives, which held for this
    /// shape before it held for the current one.
    /// </remarks>
    private sealed class StoredInvitationOfVersionOne
    {
        /// <summary>Gets or sets the identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the keyed hash, base64.</summary>
        public string? CodeHash { get; set; }

        /// <summary>Gets or sets the operator who minted it.</summary>
        public Guid MintedBy { get; set; }

        /// <summary>Gets or sets when it was minted.</summary>
        public DateTimeOffset MintedAt { get; set; }

        /// <summary>Gets or sets when it stops being usable.</summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>Gets or sets how many accounts it was good for.</summary>
        public int UsesGranted { get; set; }

        /// <summary>Gets or sets how many are left.</summary>
        public int UsesRemaining { get; set; }

        /// <summary>Gets or sets when it was revoked, or null.</summary>
        [JsonRequired]
        public DateTimeOffset? RevokedAt { get; set; }

        /// <summary>Gets or sets the operator who revoked it, or null.</summary>
        [JsonRequired]
        public Guid? RevokedBy { get; set; }

        /// <summary>Gets or sets the template the operator picked.</summary>
        public string? TemplateLabel { get; set; }

        /// <summary>Gets or sets the accounts it created.</summary>
        public List<Guid>? AccountsProduced { get; set; }

        public Invitation ToInvitationWithNoGrant()
        {
            return new Invitation(
                Id,
                ImmutableArray.Create(Convert.FromBase64String(CodeHash ?? string.Empty)),
                MintedBy,
                MintedAt,
                ExpiresAt,
                UsesGranted,
                UsesRemaining,
                RevokedAt,
                RevokedBy,
                TemplateLabel ?? string.Empty,
                template: null,
                AccountsProduced is null ? ImmutableArray<Guid>.Empty : ImmutableArray.CreateRange(AccountsProduced));
        }
    }
}
