using FriWorld.Launcher.Core.Manifest;

namespace FriWorld.Launcher.Core.Sources;

/// <summary>A source that hands back a manifest it was given. For tests.</summary>
public sealed class InMemoryReleaseSource(ReleaseManifest manifest) : IReleaseSource
{
    public string Description => "in-memory";

    public Task<ReleaseManifest> GetLatestAsync(CancellationToken ct) =>
        Task.FromResult(manifest);
}
