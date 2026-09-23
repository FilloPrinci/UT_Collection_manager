using System.Text;
using UTLauncher.Core.Manifest;

namespace UTLauncher.Core.Games;

/// <summary>
/// Builds/merges UT4's Engine.ini master-server sections (SPEC.md §6.3 step 4): if the file
/// doesn't exist yet, create it with all of the manifest's sections; if it already exists, add
/// only whichever of those sections are missing, leaving everything else byte-for-byte untouched.
/// </summary>
public static class EngineIniMerger
{
    public static string Merge(string? existingContent, MasterServerSection masterServer)
    {
        var sections = masterServer.EngineIniSections ?? [];
        if (string.IsNullOrEmpty(existingContent))
        {
            var builder = new StringBuilder();
            for (var i = 0; i < sections.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                AppendSection(builder, sections[i], masterServer);
            }

            return builder.ToString();
        }

        var existingSectionNames = GetSectionNames(existingContent);
        var missingSections = sections.Where(name => !existingSectionNames.Contains(name)).ToList();
        if (missingSections.Count == 0)
        {
            return existingContent;
        }

        var result = new StringBuilder(existingContent);
        if (result.Length > 0 && result[^1] != '\n')
        {
            result.Append('\n');
        }

        foreach (var sectionName in missingSections)
        {
            result.Append('\n');
            AppendSection(result, sectionName, masterServer);
        }

        return result.ToString();
    }

    private static void AppendSection(StringBuilder builder, string sectionName, MasterServerSection masterServer)
    {
        builder.Append('[').Append(sectionName).Append(']').Append('\n');
        builder.Append("Domain=").Append(masterServer.Domain).Append('\n');
        builder.Append("Protocol=").Append(masterServer.Protocol).Append('\n');
    }

    private static HashSet<string> GetSectionNames(string content)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length >= 2 && line[0] == '[' && line[^1] == ']')
            {
                names.Add(line[1..^1]);
            }
        }

        return names;
    }
}
