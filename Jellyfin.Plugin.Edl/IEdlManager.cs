using System;
using System.Collections.Generic;
using MediaBrowser.Model.MediaSegments;

namespace Jellyfin.Plugin.Edl;

/// <summary>
/// Interface for EDL management operations.
/// </summary>
public interface IEdlManager
{
    /// <summary>
    /// Logs the configuration that will be used during EDL file creation.
    /// </summary>
    void LogConfiguration();

    /// <summary>
    /// Update EDL file for the provided segments.
    /// </summary>
    /// <param name="psegment">Key value pair of segments dictionary.</param>
    /// <param name="forceOverwrite">Force the file overwrite.</param>
    void UpdateEDLFile(KeyValuePair<Guid, List<MediaSegmentDto>> psegment, bool forceOverwrite);

    /// <summary>
    /// Convert segments to a Kodi compatible EDL entry.
    /// </summary>
    /// <param name="segments">The Segments.</param>
    /// <returns>String content of EDL file.</returns>
    string ToEdl(IReadOnlyCollection<MediaSegmentDto> segments);
}
