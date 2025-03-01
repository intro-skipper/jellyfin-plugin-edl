using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Edl.Data;
using MediaBrowser.Model.MediaSegments;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Edl.Managers;

/// <summary>
/// Update EDL files associated with a list of episodes.
/// </summary>
/// <param name="logger">Logger.</param>
public class EdlManager(ILogger<EdlManager> logger) : IEdlManager
{
    private readonly ILogger<EdlManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Logs the configuration that will be used during EDL file creation.
    /// </summary>
    public void LogConfiguration()
    {
        var config = Plugin.Instance!.Configuration;

        _logger.LogDebug("Overwrite EDL files: {Regenerate}", config.OverwriteEdlFiles);
        _logger.LogDebug("Intro EdlAction: {Action}", config.IntroEdlAction);
        _logger.LogDebug("Outro EdlAction: {Action}", config.OutroEdlAction);
        _logger.LogDebug("Preview EdlAction: {Action}", config.PreviewEdlAction);
        _logger.LogDebug("Recap EdlAction: {Action}", config.RecapEdlAction);
        _logger.LogDebug("Unknown EdlAction: {Action}", config.UnknownEdlAction);
        _logger.LogDebug("Commercial EdlAction: {Action}", config.CommercialEdlAction);
        _logger.LogDebug("Max Parallelism: {Action}", config.MaxParallelism);
    }

    /// <summary>
    /// Update EDL file for the provided segments.
    /// </summary>
    /// <param name="psegment">Key value pair of segments dictionary.</param>
    /// <param name="forceOverwrite">Force the file overwrite.</param>
    public void UpdateEDLFile(KeyValuePair<Guid, List<MediaSegmentDto>> psegment, bool forceOverwrite)
    {
        var id = psegment.Key;
        var segments = psegment.Value;
        var config = Plugin.Instance!.Configuration;
        var overwrite = config.OverwriteEdlFiles || forceOverwrite;

        _logger.LogDebug("Update EDL file for itemId {ItemId} with {Segments} segments", id, segments.Count);

        try
        {
            var filePath = Plugin.Instance!.GetItemPath(id);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _logger.LogWarning("Skip id ({Id}): unable to get item path or file not found", id);
                return;
            }

            var edlPath = GetEdlPath(filePath);
            if (File.Exists(edlPath) && !overwrite)
            {
                _logger.LogDebug("EDL file exists, but overwrite is disabled: '{File}'", edlPath);
                return;
            }

            var edlContent = ToEdl(segments);

            if (string.IsNullOrEmpty(edlContent))
            {
                _logger.LogDebug("Skip id ({Id}): no EDL data generated", id);
                return;
            }

            _logger.LogDebug("Writing EDL to {Path}", edlPath);

            File.WriteAllText(edlPath, edlContent);
            _logger.LogDebug("Successfully created EDL file for {Id} at {Path}", id, edlPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create EDL file for item {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Convert segments to a Kodi compatible EDL entry.
    /// </summary>
    /// <param name="segments">The Segments.</param>
    /// <returns>String content of edl file.</returns>
    public string ToEdl(IReadOnlyCollection<MediaSegmentDto> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var fstring = string.Empty;
        foreach (var segment in segments)
        {
            var action = GetActionforType(segment.Type);

            // Skip None actions
            if (action != EdlAction.None)
            {
                fstring += ToEdlString(segment.StartTicks, segment.EndTicks, action);
            }
        }

        // remove last newline
        var newlineInd = fstring.LastIndexOf('\n');
        return newlineInd > 0 ? fstring[..newlineInd] : fstring;
    }

    /// <summary>
    /// Create EDL string based on Action with newline. Public for tests.
    /// </summary>
    /// <param name="start">Start position.</param>
    /// <param name="end">End position.</param>
    /// <param name="action">The Action.</param>
    /// <returns>String content of edl file.</returns>
    public static string ToEdlString(long start, long end, EdlAction action)
    {
        var rstart = Math.Round((double)start / 10_000_000, 3);
        var rend = Math.Round((double)end / 10_000_000, 3);

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2} \n", rstart, rend, (int)action);
    }

    /// <summary>
    /// Convert a segments Type to an edl Action based on user settings.
    /// </summary>
    /// <param name="type">The Segments type.</param>
    /// <returns>String content of edl file.</returns>
    private EdlAction GetActionforType(MediaSegmentType type)
    {
        var config = Plugin.Instance?.Configuration;
        ArgumentNullException.ThrowIfNull(config);
        _logger.LogDebug("GetActionforType called with type: {Type}", type);

        return type switch
        {
            MediaSegmentType.Unknown => config.UnknownEdlAction,
            MediaSegmentType.Intro => config.IntroEdlAction,
            MediaSegmentType.Outro => config.OutroEdlAction,
            MediaSegmentType.Recap => config.RecapEdlAction,
            MediaSegmentType.Preview => config.PreviewEdlAction,
            MediaSegmentType.Commercial => config.CommercialEdlAction,
            _ => EdlAction.None
        };
    }

    /// <summary>
    /// Given the path to an episode, return the path to the associated EDL file.
    /// </summary>
    /// <param name="mediaPath">Full path to episode.</param>
    /// <returns>Full path to EDL file.</returns>
    private static string GetEdlPath(string mediaPath) => Path.ChangeExtension(mediaPath, "edl");
}
