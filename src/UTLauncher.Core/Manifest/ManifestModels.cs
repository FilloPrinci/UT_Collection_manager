using System.Text.Json.Serialization;

namespace UTLauncher.Core.Manifest;

public sealed record Manifest(
    [property: JsonPropertyName("manifestVersion")] int ManifestVersion,
    [property: JsonPropertyName("updated")] string? Updated,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("tools")] ToolsSection? Tools,
    [property: JsonPropertyName("games")] IReadOnlyList<GameEntry> Games);

public sealed record ToolsSection(
    [property: JsonPropertyName("sevenZip")] ToolEntry? SevenZip,
    [property: JsonPropertyName("unshield")] ToolEntry? Unshield,
    [property: JsonPropertyName("umu")] ToolEntry? Umu,
    [property: JsonPropertyName("proton")] ToolEntry? Proton);

public sealed record ToolEntry(
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("windows")] ToolPlatformFile? Windows,
    [property: JsonPropertyName("linux")] ToolPlatformFile? Linux);

public sealed record ToolPlatformFile(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("exe")] string? Exe,
    [property: JsonPropertyName("name")] string? Name);

public sealed record GameEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("versionCode")] string VersionCode,
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("sources")] IReadOnlyDictionary<string, SourceFile> Sources,
    [property: JsonPropertyName("patch")] PatchSection? Patch,
    [property: JsonPropertyName("launch")] IReadOnlyDictionary<string, LaunchEntry>? Launch,
    [property: JsonPropertyName("network")] NetworkSection? Network,
    [property: JsonPropertyName("diskSpaceRequiredBytes")] long? DiskSpaceRequiredBytes,
    [property: JsonPropertyName("masterServer")] MasterServerSection? MasterServer,
    [property: JsonPropertyName("ut4uuInstallInfo")] IReadOnlyList<string>? Ut4uuInstallInfo,
    [property: JsonPropertyName("accountRegistrationUrl")] string? AccountRegistrationUrl,
    [property: JsonPropertyName("dependencies")] DependenciesSection? Dependencies,
    [property: JsonPropertyName("cdKey")] CdKeySection? CdKey);

public sealed record SourceFile(
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("urls")] IReadOnlyList<string>? Urls,
    [property: JsonPropertyName("alternatives")] IReadOnlyList<SourceFile>? Alternatives,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("innerEntry")] string? InnerEntry);

public sealed record PatchSection(
    [property: JsonPropertyName("tag")] string? Tag,
    [property: JsonPropertyName("windows")] PatchPlatformFile? Windows,
    [property: JsonPropertyName("linux-x64")] PatchPlatformFile? LinuxX64);

public sealed record PatchPlatformFile(
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("sha256")] string? Sha256);

public sealed record LaunchEntry(
    [property: JsonPropertyName("exe")] string? Exe,
    [property: JsonPropertyName("args")] string? Args,
    [property: JsonPropertyName("workingDir")] string? WorkingDir,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("runner")] string? Runner,
    [property: JsonPropertyName("windowsInstallPath")] string? WindowsInstallPath,
    [property: JsonPropertyName("winetricks")] IReadOnlyList<string>? Winetricks);

public sealed record NetworkSection(
    [property: JsonPropertyName("defaultPortUdp")] int? DefaultPortUdp);

public sealed record MasterServerSection(
    [property: JsonPropertyName("domain")] string? Domain,
    [property: JsonPropertyName("protocol")] string? Protocol,
    [property: JsonPropertyName("engineIniSections")] IReadOnlyList<string>? EngineIniSections);

public sealed record DependenciesSection(
    [property: JsonPropertyName("windows")] WindowsDependencies? Windows);

public sealed record WindowsDependencies(
    [property: JsonPropertyName("directxJune2010")] DirectXDependency? DirectxJune2010,
    [property: JsonPropertyName("vcredist2013x64")] VcRedistDependency? Vcredist2013X64);

public sealed record DirectXDependency(
    [property: JsonPropertyName("checkFilesInSystem32")] IReadOnlyList<string>? CheckFilesInSystem32);

public sealed record VcRedistDependency(
    [property: JsonPropertyName("registryKeysHKLM")] IReadOnlyList<string>? RegistryKeysHklm,
    [property: JsonPropertyName("installArgs")] string? InstallArgs);

public sealed record CdKeySection(
    [property: JsonPropertyName("required")] bool? Required,
    [property: JsonPropertyName("notes")] string? Notes);
