using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FriWorld.Launcher.Core.Manifest;

/// <summary>
/// Single place where manifest JSON is read and written, so the casing and enum conventions
/// cannot drift between the launcher and whatever generates the file.
/// </summary>
public static class ManifestJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },

        // The default encoder escapes '+' as +, which turns a timestamp offset or a version
        // with build metadata into something nobody can read in a diff. This file is fetched as
        // JSON and never embedded in a page, so the escaping buys nothing and costs legibility.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static ReleaseManifest Parse(string json)
    {
        ReleaseManifest? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<ReleaseManifest>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ManifestException($"Manifest is not valid JSON: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new ManifestException("Manifest parsed to null.");
        }

        manifest.Validate();
        return manifest;
    }

    public static string Write(ReleaseManifest manifest) =>
        JsonSerializer.Serialize(manifest, Options);
}
