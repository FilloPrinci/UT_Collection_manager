using System.Text.Json.Serialization;

namespace UTLauncher.Core.Manifest;

public sealed record Manifest(
    [property: JsonPropertyName("manifestVersion")] int ManifestVersion,
    [property: JsonPropertyName("updated")] string? Updated,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("tools")] ToolsSection? Tools,
    [property: JsonPropertyName("games")] IReadOnlyList<GameEntry> Games);

public sealed record ToolsSection(
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
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("extraFiles")] IReadOnlyList<ToolExtraFile>? ExtraFiles = null);

// A file the tool's executable depends on at runtime (e.g. a DLL it dynamically links against),
// downloaded and hash-verified into the same tool directory alongside the executable.
public sealed record ToolExtraFile(
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("size")] long? Size);

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
    [property: JsonPropertyName("vcredist2013x64")] VcRedistDependency? Vcredist2013X64,
    [property: JsonPropertyName("vcRedistX86")] VcRedistInstaller? VcRedistX86,
    [property: JsonPropertyName("vcRedistX64")] VcRedistInstaller? VcRedistX64,
    [property: JsonPropertyName("directXWebSetup")] DirectXWebSetupInstaller? DirectXWebSetup,
    [property: JsonPropertyName("notes")] string? Notes = null);

// VC++ 14.x (VS2015-2022) redistributable: OldUnreal's Windows/Common.nsh checks a single
// "Installed"=1 DWORD under a per-architecture registry key before running the installer.
public sealed record VcRedistInstaller(
    [property: JsonPropertyName("registryKeyHKLM")] string? RegistryKeyHklm,
    [property: JsonPropertyName("registryValueName")] string? RegistryValueName,
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("installArgs")] string? InstallArgs,
    // The x86 redistributable is a 32-bit installer: on 64-bit Windows its registry key lives
    // under the WOW6432Node-redirected view, invisible to our 64-bit process unless it explicitly
    // opens the 32-bit registry view. Set true for the x86 dependency, leave false (native/64-bit
    // view) for x64.
    [property: JsonPropertyName("registryView32")] bool RegistryView32 = false);

// DirectX End-User Runtime web installer: OldUnreal's script has no presence check, it just runs
// dxwebsetup.exe /q unconditionally every time (a fast no-op when nothing needs updating).
public sealed record DirectXWebSetupInstaller(
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("installArgs")] string? InstallArgs);

// DirectX June 2010 offline redistributable (UT4 needs legacy components - XInput 1.3, XAudio
// 2.7, X3DAudio 1.7, XAPOFX 1.5 - the modern web installer no longer carries). It's a
// self-extracting archive, not a plain installer: unlike the other Windows dependencies here, it
// needs two steps - "directx_Jun2010_redist.exe /Q /T:<dir>" to unpack, then "<dir>/DXSETUP.exe
// /silent" to actually install - see WindowsDependencyInstaller.EnsureDirectXJune2010Async.
public sealed record DirectXDependency(
    [property: JsonPropertyName("checkFilesInSystem32")] IReadOnlyList<string>? CheckFilesInSystem32,
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("size")] long? Size);

// VC++ 2013 (VS2013/"12.0") redistributable: predates the unified "Installed"=1 registry flag
// VcRedistInstaller checks, so presence is instead inferred from a set of per-dependency GUID
// keys (any one missing means "not installed").
public sealed record VcRedistDependency(
    [property: JsonPropertyName("registryKeysHKLM")] IReadOnlyList<string>? RegistryKeysHklm,
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("installArgs")] string? InstallArgs);

public sealed record CdKeySection(
    [property: JsonPropertyName("required")] bool? Required,
    [property: JsonPropertyName("notes")] string? Notes);
