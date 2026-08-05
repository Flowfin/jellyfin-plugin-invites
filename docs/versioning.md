# Versioning

The plugin has one version number, it is written in one place, and everything else
is derived from it.

## Where the number lives

`build.yaml` holds it, in the `version` key. That file is the packaging manifest a
catalogue reads, so it is the copy that has to be right.

`Directory.Build.props` reads that key while the project is being built and sets
`Version`, `AssemblyVersion` and `FileVersion` from what it read. Nothing types the
number a second time, so the assembly and the manifest cannot drift apart. A
`build.yaml` whose `version` key is missing, misspelled or not four parts fails the
build with the file named, rather than letting the build stamp the SDK default of
`1.0.0.0` into an assembly whose manifest says something else.

## Which part moves

The number is `major.minor.patch.revision`, which is the four part shape a Jellyfin
plugin manifest wants.

`major` moves when an operator has to do something by hand before or after
upgrading, or when behaviour they already depend on changes. Removing a
configuration setting, changing what an existing setting means, or changing what an
invitation does when it is redeemed are all major.

`minor` moves for a feature that an operator can ignore. An added setting with a
default that keeps the old behaviour is minor.

`patch` moves for a fix that changes no interface, including a security fix.

`revision` stays at `0`. It is reserved for republishing the same source under a new
artefact, such as a packaging correction, so it never means a code change.

While `major` is `0` nothing about the interface is promised, and `minor` carries
the breaking changes. The move to `1.0.0.0` is a release decision and belongs with
the release process rather than here.

## Rebuilding an old version

`packages.lock.json` is committed next to each project file, and it records the exact
dependency graph a build resolved, not the ranges the project asked for. The build
workflow restores with `--locked-mode`, which refuses to resolve anything the lock
file does not already name, so rebuilding an old tag pulls the versions that tag
shipped with rather than whatever is newest on the feed that day. Locally restore
may still update the lock file, which is what makes a dependency change visible as a
diff rather than as a silent resolution.

Adding or changing a package reference therefore changes two files, and a pull
request that changes one without the other fails restore on the runner.

## Building the same commit twice

Two clean builds of one commit produce byte identical output. Measured by removing
`obj/` and `bin/`, building, hashing, and doing it again:

```
$ dotnet build Jellyfin.Plugin.Template.sln -c Release --no-incremental
$ python -c "import hashlib,pathlib;print(hashlib.sha256(pathlib.Path('Jellyfin.Plugin.Template/bin/Release/net9.0/Jellyfin.Plugin.Template.dll').read_bytes()).hexdigest())"
9cd4dcb77f36d72723bee4f72675858da8100722659276a81e548a206c3f346f
```

Removing `obj/` and `bin/` and doing it again prints the same digest.

`Deterministic` is stated in `Directory.Build.props` rather than left to its default
so that turning it off is a visible change. `ContinuousIntegrationBuild` is set on a
build runner, which replaces the absolute paths of the machine that built with
relative ones. It is deliberately not set for a local build, because those paths are
what a debugger needs to find the source on the machine that produced the assembly.

This is measured on one machine building one commit twice. Two different machines
producing the same bytes is a stronger claim and it has not been measured here.
