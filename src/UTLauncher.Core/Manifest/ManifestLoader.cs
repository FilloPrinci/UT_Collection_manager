using System.Reflection;
using System.Text.Json;

namespace UTLauncher.Core.Manifest;

public sealed class ManifestLoader
{
    private const string BundledResourceName = "UTLauncher.Core.manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<Manifest> LoadBundledAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(BundledResourceName)
            ?? throw new ManifestLoadException($"Risorsa manifest incorporata non trovata: '{BundledResourceName}'.");

        return await LoadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Manifest> LoadFromFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new ManifestLoadException($"File manifest non trovato: '{path}'.");
        }

        await using var stream = File.OpenRead(path);
        return await LoadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Manifest> LoadFromStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await JsonSerializer.DeserializeAsync<Manifest>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return manifest ?? throw new ManifestLoadException("Il manifest è vuoto o non valido (JSON null).");
        }
        catch (JsonException ex)
        {
            throw new ManifestLoadException($"Manifest non valido: JSON malformato ({ex.Message}).", ex);
        }
    }
}
