using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// Attaches <see cref="RefuseOnAServerLineMismatch"/> to every controller this
/// plugin's assembly holds, and to no other.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is scoped by assembly rather than by name or by namespace.</b> The
/// model this walks is the server's whole application model, every controller
/// of the server and of every other plugin loaded beside this one included. A
/// filter attached to one of those would be this plugin deciding whether
/// somebody else's route answers, which is a reach far outside anything #97
/// asks for. The assembly a controller's type was declared in is the one test
/// that cannot be satisfied by accident.
/// </para>
/// <para>
/// <b>It attaches a service filter rather than an instance.</b> The refusal
/// needs the verdict taken at start-up, which is a registered singleton, and a
/// convention runs while the model is built rather than while a request is
/// served. <see cref="ServiceFilterAttribute"/> is the shape that defers the
/// resolution to the request, so the filter is constructed by the server's own
/// container with the one gate in it.
/// </para>
/// <para>
/// <b>What is claimed for it and what is not.</b> That it attaches the filter to
/// exactly this plugin's controllers is asserted against a model a test builds.
/// That the server applies a convention a plugin adds to <c>MvcOptions</c> is
/// not asserted anywhere here: no server is started by this suite, and the
/// manual register in docs/manual-checks.md is where a reading against a running
/// server belongs.
/// </para>
/// </remarks>
public sealed class ThisPluginsControllers : IApplicationModelConvention
{
    private readonly Assembly _plugin;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThisPluginsControllers"/> class.
    /// </summary>
    public ThisPluginsControllers()
        : this(typeof(ThisPluginsControllers).Assembly)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThisPluginsControllers"/> class
    /// for a stated assembly.
    /// </summary>
    /// <param name="plugin">The assembly whose controllers are this plugin's.</param>
    /// <remarks>
    /// The assembly is a parameter so a test can point it at a foreign one and
    /// show that the controllers of this plugin are then left alone, which is
    /// the direction a scope rule is got wrong in.
    /// </remarks>
    public ThisPluginsControllers(Assembly plugin)
    {
        _plugin = plugin;
    }

    /// <inheritdoc />
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (var controller in application.Controllers)
        {
            if (controller.ControllerType.Assembly == _plugin)
            {
                controller.Filters.Add(new ServiceFilterAttribute(typeof(RefuseOnAServerLineMismatch)));
            }
        }
    }
}
