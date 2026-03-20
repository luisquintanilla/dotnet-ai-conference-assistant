using ConferenceAssistant.Ingestion.Models;
using ConferenceAssistant.Ingestion.Utilities;

namespace ConferenceAssistant.Ingestion.Enrichers;

/// <summary>
/// Enriches <see cref="ConferenceRecord"/> entries with metadata extracted from
/// YAML front matter. This is a purely metadata-based enricher — no AI/IChatClient required.
/// </summary>
public class FrontMatterEnricher
{
    private readonly Dictionary<string, FrontMatter> _frontMatterByKey;

    /// <summary>
    /// Creates a new enricher with a lookup of front matter keyed by file path or document ID.
    /// </summary>
    public FrontMatterEnricher(Dictionary<string, FrontMatter> frontMatterByKey)
    {
        _frontMatterByKey = frontMatterByKey ?? throw new ArgumentNullException(nameof(frontMatterByKey));
    }

    /// <summary>
    /// Creates an enricher with no pre-loaded front matter. Use <see cref="EnrichRecord"/> directly.
    /// </summary>
    public FrontMatterEnricher()
    {
        _frontMatterByKey = new Dictionary<string, FrontMatter>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds front matter for a given key (file path or document ID) to the lookup.
    /// </summary>
    public void AddFrontMatter(string key, FrontMatter frontMatter)
    {
        _frontMatterByKey[key] = frontMatter;
    }

    /// <summary>
    /// Looks up front matter by key and enriches the record if found.
    /// </summary>
    public void EnrichRecord(ConferenceRecord record, string key)
    {
        if (_frontMatterByKey.TryGetValue(key, out var frontMatter))
        {
            EnrichRecord(record, frontMatter);
        }
    }

    /// <summary>
    /// Enriches a <see cref="ConferenceRecord"/> with technology keywords, category,
    /// and job description from the provided front matter.
    /// </summary>
    public static void EnrichRecord(ConferenceRecord record, FrontMatter? frontMatter)
    {
        if (frontMatter is null)
        {
            return;
        }

        // Add technologies as keywords (avoid duplicates)
        foreach (var tech in frontMatter.Technologies)
        {
            if (!string.IsNullOrWhiteSpace(tech) && !record.Keywords.Contains(tech, StringComparer.OrdinalIgnoreCase))
            {
                record.Keywords.Add(tech);
            }
        }

        // Add category as a keyword
        if (!string.IsNullOrWhiteSpace(frontMatter.Category) &&
            !record.Keywords.Contains(frontMatter.Category, StringComparer.OrdinalIgnoreCase))
        {
            record.Keywords.Add(frontMatter.Category);
        }

        // Use the job description as a summary if none exists
        if (!string.IsNullOrWhiteSpace(frontMatter.Job))
        {
            record.Summary ??= frontMatter.Job;
        }
    }
}
