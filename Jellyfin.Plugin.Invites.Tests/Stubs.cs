using System;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Application paths rooted in a directory the test owns and deletes, so the
/// suite writes nothing outside its own temporary directory.
/// </summary>
internal sealed class StubApplicationPaths : IApplicationPaths, IDisposable
{
    private readonly string _root;

    public StubApplicationPaths()
    {
        _root = Path.Combine(Path.GetTempPath(), "jellyfin-plugin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public string ProgramDataPath => _root;

    public string WebPath => Path.Combine(_root, "web");

    public string ProgramSystemPath => _root;

    public string DataPath => Path.Combine(_root, "data");

    public string VirtualDataPath => Path.Combine(_root, "virtual-data");

    public string ImageCachePath => Path.Combine(_root, "image-cache");

    public string PluginsPath => Path.Combine(_root, "plugins");

    public string PluginConfigurationsPath => Path.Combine(_root, "plugin-configurations");

    public string LogDirectoryPath => Path.Combine(_root, "logs");

    public string ConfigurationDirectoryPath => Path.Combine(_root, "config");

    public string SystemConfigurationFilePath => Path.Combine(_root, "config", "system.xml");

    public string CachePath => Path.Combine(_root, "cache");

    public string TempDirectory => Path.Combine(_root, "temp");

    public string TrickplayPath => Path.Combine(_root, "trickplay");

    public string BackupPath => Path.Combine(_root, "backup");

    public void MakeSanityCheckOrThrow()
    {
        // The paths above are all inside a directory this instance created, so
        // there is nothing to check and nothing to refuse.
    }

    public void CreateAndCheckMarker(string path, string markerName, bool recursive = false)
    {
        // Nothing in the suite reads a marker, and writing one would put a file
        // outside the directory this instance owns.
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover directory under the system temporary path is not worth
            // failing a test over, and nothing outside that path was touched.
        }
    }
}

/// <summary>
/// A serializer that refuses every call. The plugin under test is constructed
/// but its configuration is never read, so any call here is the test reaching
/// somewhere it did not mean to and should be loud rather than silent.
/// </summary>
internal sealed class StubXmlSerializer : IXmlSerializer
{
    public object? DeserializeFromBytes(Type type, byte[] buffer)
        => throw new NotSupportedException("The suite does not deserialize plugin configuration.");

    public object? DeserializeFromFile(Type type, string file)
        => throw new NotSupportedException("The suite does not deserialize plugin configuration.");

    public object? DeserializeFromStream(Type type, Stream stream)
        => throw new NotSupportedException("The suite does not deserialize plugin configuration.");

    public void SerializeToFile(object obj, string file)
        => throw new NotSupportedException("The suite does not write plugin configuration.");

    public void SerializeToStream(object obj, Stream stream)
        => throw new NotSupportedException("The suite does not write plugin configuration.");
}
