using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The four texts a person reads before installing this plugin say that a
/// person following an invitation link ends up with an account, for as long as
/// the public route carries a post that creates one.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this exists against.</b> All four said the opposite, in the same
/// words, for a day: no link can be redeemed and no account is created by this
/// plugin. That was true until the redemption post landed on 2026-09-04, and
/// nothing in this tree read any of the four, so it stayed. #484 is where that
/// was found and repaired.
/// </para>
/// <para>
/// <b>Why these four and not the documents in general.</b> Three of them are
/// read by somebody deciding whether to install, and the fourth is
/// <c>build.yaml</c>, whose <c>description</c> and <c>changelog</c> the
/// packaging job puts in the manifest a catalogue shows beside an Install
/// button. A person reading that has no way to check it against the tree. Every
/// other document is read by somebody who already has the repository open.
/// </para>
/// <para>
/// <b>The direction that makes it worth a check.</b> A page understating a
/// defence costs a reader a wrong search. These four understated the RISK: they
/// told somebody the half a stranger drives is inert while it was not, which is
/// the one direction this plugin's own security page is written against.
/// </para>
/// <para>
/// <b>What is refused.</b> A named text that does not say the invited half
/// creates an account, while the assembly carries a public post; and a named
/// path that is not in the tree, so a rename cannot empty this comparison
/// silently.
/// </para>
/// <para>
/// <b>What it cannot do.</b> It matches a phrase and never a meaning. A text
/// carrying the phrase in a sentence denying it passes, and so does one that
/// says the right thing in other words after somebody rewrites it - the second
/// is the cost of a checked agreement and it is paid deliberately, because the
/// alternative is a vocabulary of denials, which the repairs themselves have to
/// quote in order to say what they corrected.
/// </para>
/// <para>
/// The population is declared rather than derived, and that is the bound to read
/// before treating a green run as coverage: a fifth such text added tomorrow is
/// outside it until somebody names it here.
/// </para>
/// </remarks>
public class WhatAnInstallerIsToldTests
{
    /// <summary>
    /// The texts this holds, each with what it is read for.
    /// </summary>
    private static readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal)
    {
        ["README.md"] = "read by somebody deciding whether to install this",
        ["CHANGELOG.md"] = "read by somebody deciding whether to upgrade",
        ["build.yaml"] = "the description and changelog a catalogue shows beside an Install button",
        [Path.Combine("docs", "operator-guide.md")] = "read by somebody using it",
    };

    /// <summary>
    /// What each of them has to say. It is the shortest phrase that cannot be
    /// written by a text claiming the invited half is inert.
    /// </summary>
    private const string Says = "creates the account";

    /// <summary>
    /// The antecedent, read off the assembly rather than assumed: the plugin
    /// serves a post that a person following a link submits.
    /// </summary>
    /// <remarks>
    /// This leg is not scenery. The comparison below is conditional on the post
    /// existing, so a run in which the post had been removed and the comparison
    /// passed anyway would be a green run over a condition nobody checked. If
    /// this reddens, the four texts are what to re-read.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public void ThePluginServesAPostAPersonFollowingALinkSubmits()
    {
        Assert.True(
            PostingActions().Count > 0,
            "No action in the plugin assembly carries an HTTP POST that a person without an account could submit, "
            + "so the condition the comparison below rests on is not met. Either the redemption post has been removed, "
            + "in which case the four texts this class names have to be re-read and say so again, or this enumeration "
            + "has stopped seeing what the server sees.");
    }

    /// <summary>
    /// Every text an installer reads says that following a link creates an
    /// account.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public void EveryTextAnInstallerReadsSaysTheInvitedHalfCreatesAnAccount()
    {
        if (PostingActions().Count == 0)
        {
            return;
        }

        var silent = _texts
            .Where(text => !File.ReadAllText(Path.Combine(Root(), text.Key))
                .Contains(Says, StringComparison.OrdinalIgnoreCase))
            .Select(text => text.Key + " (" + text.Value + ")")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            silent.Count == 0,
            "These texts do not say that following an invitation link creates an account, and the plugin serves the post that does: "
            + string.Join(", ", silent)
            + ". Each of them once said the opposite and nothing read them, which is #484. The phrase looked for is \""
            + Says
            + "\".");
    }

    /// <summary>
    /// And every path this class names is in the tree, so a rename empties the
    /// comparison loudly rather than quietly.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public void EveryTextThisClassNamesIsInTheTree()
    {
        var missing = _texts.Keys
            .Where(name => !File.Exists(Path.Combine(Root(), name)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "These paths are named here and are not in the tree: "
            + string.Join(", ", missing)
            + ". A comparison over a path that has moved reads nothing and passes, which is the failure this leg exists against.");
    }

    /// <summary>
    /// The actions of the plugin's controllers that a POST reaches and that a
    /// caller with no account may reach.
    /// </summary>
    /// <remarks>
    /// Both halves are required, and the second is what makes the count mean
    /// what this class says it means. The administrator side of this plugin has
    /// carried POST actions since long before the redemption post existed -
    /// minting, revoking and rotating are three of them - so a count of POSTs
    /// alone would have been above zero on the day all four texts were correct.
    /// Anonymity is read off the action and off its declaring type, because a
    /// controller can carry the declaration for every action it holds.
    /// </remarks>
    /// <returns>Their names, which is a count rather than a list a caller uses.</returns>
    private static IReadOnlyList<string> PostingActions() =>
        typeof(Plugin).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(action => action.GetCustomAttributes<HttpPostAttribute>().Any())
            .Where(action => action.GetCustomAttributes<AllowAnonymousAttribute>().Any()
                || (action.DeclaringType?.GetCustomAttributes<AllowAnonymousAttribute>().Any() ?? false))
            .Select(action => (action.DeclaringType?.FullName ?? "?") + "." + action.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The directory holding the solution and the texts, found by walking up
    /// from the test binary the way every other leg over tracked text does.
    /// </summary>
    /// <returns>The repository root.</returns>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            var readme = Path.Combine(directory.FullName, "README.md");
            if (File.Exists(solution) && File.Exists(readme))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds Jellyfin.Plugin.Invites.sln and README.md, so these legs read nothing. Failing rather than reporting a comparison that ran over an empty set.");
    }
}
