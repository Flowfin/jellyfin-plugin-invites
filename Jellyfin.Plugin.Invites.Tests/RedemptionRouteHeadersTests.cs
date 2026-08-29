using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Every action of the redemption route answers with the headers that route
/// owes, and not only the one somebody named.
/// </summary>
/// <remarks>
/// <para>
/// <b>The headers are set inside an action rather than in one place the route
/// passes through.</b> That is a fact about <see cref="RedeemController"/> and
/// not a complaint about it: there is one action, so there is nowhere else for
/// them to be yet. What it costs is that the protection is a thing each action
/// carries, and an action added beside the first carries none of it unless
/// whoever wrote it remembered.
/// </para>
/// <para>
/// <b>The action that would forget is the next one to be written.</b>
/// <c>POST /redeem/{code}</c> is in the register of routes docs/api.md says
/// nothing serves. It is the action that takes a password, on an address that
/// carries a credential in its path, and it is the one of the two that most
/// needs a browser told not to frame it, not to store it, not to guess its type
/// and not to hand the code on in a referrer. Until it exists the property here
/// is held by there being one action; after it exists it is held by this.
/// </para>
/// <para>
/// <b>What is asserted, and what is asserted elsewhere.</b> This asks that each
/// of the five headers is set at all, on every action this leg can drive.
/// <see cref="SetupPageTests"/> holds what each one is set to, for the action
/// that serves the page. Splitting them that way is deliberate: two lists of
/// values would drift against each other, and a header dropped from a response
/// reds both while a value that changed reds only the one that decides it.
/// </para>
/// <para>
/// <b>It fails closed on an action it cannot drive.</b> An action taking
/// parameters, or a controller that has grown a constructor argument, is
/// reported by name rather than passed over, because a leg that quietly stops
/// covering the action it was written for reports the same green as one that
/// covered it. The repair is to assert the headers on that action here or to
/// widen this leg so it can drive one, and either way somebody has weighed the
/// question the leg exists to ask.
/// </para>
/// <para>
/// <b>No web host.</b> The controller is an ordinary object and the response is
/// a <see cref="DefaultHttpContext"/> this test owns, which is the headless
/// rule rather than a shortcut. What that bounds is the same bound
/// <see cref="SetupPageTests"/> states: nothing here says what a server's own
/// pipeline does with these headers on a plugin route, and nothing here has run
/// against one.
/// </para>
/// </remarks>
public class RedemptionRouteHeadersTests
{
    /// <summary>
    /// The headers this route owes, by name.
    /// </summary>
    /// <remarks>
    /// Names rather than values, for the reason stated at the class. The five
    /// are the policy derived from the page, the two that stop a browser
    /// framing the form or guessing its type, the one that keeps the response
    /// out of a cache, and the one that keeps the code in the path out of a
    /// referrer.
    /// </remarks>
    private static readonly string[] Owed =
    [
        "Content-Security-Policy",
        "X-Frame-Options",
        "X-Content-Type-Options",
        "Cache-Control",
        "Referrer-Policy",
    ];

    /// <summary>
    /// The route declares actions at all. Without this, a controller whose
    /// actions had stopped being found would report the same green as one whose
    /// actions all set their headers.
    /// </summary>
    [Fact]
    public void TheScanFindsTheActionsOfTheRedemptionRoute()
    {
        Assert.NotEmpty(Actions());
    }

    /// <summary>
    /// Every action of the redemption route sets every header the route owes.
    /// </summary>
    [Fact]
    public void EveryActionOfTheRedemptionRouteAnswersWithTheHeadersItOwes()
    {
        var missing = new List<string>();

        foreach (var action in Actions())
        {
            if (action.GetParameters().Length != 0)
            {
                missing.Add(
                    action.Name
                    + " takes parameters, so this leg cannot drive it and does not know what it answers with");
                continue;
            }

            var headers = Answer(action, missing);
            if (headers is null)
            {
                continue;
            }

            missing.AddRange(
                Owed.Where(header => string.IsNullOrEmpty(headers[header].ToString()))
                    .Select(header => action.Name + " answers without " + header));
        }

        Assert.True(
            missing.Count == 0,
            "The redemption route is public, takes a password and carries a credential in its path, and every action on it owes the five headers that say a browser may not frame it, store it, guess its type or hand the code on in a referrer. These do not: "
            + string.Join("; ", missing)
            + ". They are set inside an action rather than in one place the route passes through, so an action added beside the first carries none of them. Set them on that action, or move them somewhere every action goes through and widen this leg to read that.");
    }

    /// <summary>
    /// The actions the redemption controller declares.
    /// </summary>
    /// <returns>The methods carrying an HTTP verb of their own.</returns>
    private static IReadOnlyList<MethodInfo> Actions() =>
        typeof(RedeemController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<IActionHttpMethodProvider>().Any())
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Drives one action and hands back the headers it answered with.
    /// </summary>
    /// <param name="action">The action to drive.</param>
    /// <param name="missing">Where an action that cannot be driven is recorded.</param>
    /// <returns>The response headers, or <c>null</c> where the action could not be driven.</returns>
    private static IHeaderDictionary? Answer(MethodInfo action, List<string> missing)
    {
        var constructors = typeof(RedeemController).GetConstructors();
        if (constructors.Length != 1 || constructors[0].GetParameters().Length != 0)
        {
            missing.Add(
                action.Name
                + " sits on a controller this leg can no longer construct, so nothing here reads what it answers with");
            return null;
        }

        var context = new DefaultHttpContext();
        var controller = (RedeemController)constructors[0].Invoke([]);
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var returned = action.Invoke(controller, []);
        if (returned is Task task)
        {
            task.GetAwaiter().GetResult();
        }

        return context.Response.Headers;
    }
}
