using System.Text.Json;

namespace UTLauncher.Core.InstallRegistry;

public sealed class InstallationRegistry(string registryFilePath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<InstallationRecord>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(registryFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(registryFilePath);
        var records = await JsonSerializer.DeserializeAsync<List<InstallationRecord>>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return records ?? [];
    }

    public async Task SaveAsync(IReadOnlyList<InstallationRecord> records, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(registryFilePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = registryFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, records, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, registryFilePath, overwrite: true);
    }

    public async Task<InstallationRecord?> GetAsync(string gameId, CancellationToken cancellationToken)
    {
        var records = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return records.FirstOrDefault(r => string.Equals(r.GameId, gameId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertAsync(InstallationRecord record, CancellationToken cancellationToken)
    {
        var records = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var index = records.FindIndex(r => string.Equals(r.GameId, record.GameId, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            records[index] = record;
        }
        else
        {
            records.Add(record);
        }

        await SaveAsync(records, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string gameId, CancellationToken cancellationToken)
    {
        var records = (await LoadAsync(cancellationToken).ConfigureAwait(false))
            .Where(r => !string.Equals(r.GameId, gameId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await SaveAsync(records, cancellationToken).ConfigureAwait(false);
    }
}
