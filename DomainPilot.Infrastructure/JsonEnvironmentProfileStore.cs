using System.Text.Json;
using DomainPilot.App;
using DomainPilot.Core;

namespace DomainPilot.Infrastructure;

/// <summary>
/// Stores one credential-free environment profile under the current user's local application data.
/// </summary>
public sealed class JsonEnvironmentProfileStore : IEnvironmentProfileStore
{
    private const int MaximumProfileBytes = 64 * 1024;
    private readonly string _filePath;
    private readonly EnvironmentProfileValidator _validator = new();
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public JsonEnvironmentProfileStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DomainPilot",
            "environment-profile.json");
    }

    public EnvironmentProfile? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var fileInfo = new FileInfo(_filePath);
        if (fileInfo.Length > MaximumProfileBytes)
        {
            throw new InvalidDataException("The environment profile exceeds the supported size.");
        }

        EnvironmentProfile profile;
        try
        {
            profile = JsonSerializer.Deserialize<EnvironmentProfile>(
                File.ReadAllText(_filePath),
                _options) ?? throw new InvalidDataException("The environment profile is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The environment profile is not valid JSON.", exception);
        }
        var issues = _validator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new InvalidDataException(string.Join(" ", issues));
        }

        return profile;
    }

    public void Save(EnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = _validator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", issues), nameof(profile));
        }

        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The environment profile path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(profile, _options));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
