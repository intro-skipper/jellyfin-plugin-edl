using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Edl;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.MediaSegments;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EdlManager;

/// <summary>
/// Create edl files task.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CreateEdlTask"/> class.
/// </remarks>
/// <param name="loggerFactory">Logger factory.</param>
/// <param name="libraryManager">Library manager.</param>
/// <param name="mediaSegmentManager">MediaSegment manager.</param>
public class CreateEdlTask(
    ILoggerFactory loggerFactory,
    ILibraryManager libraryManager,
    IMediaSegmentManager mediaSegmentManager) : IScheduledTask
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    private readonly ILibraryManager _libraryManager = libraryManager;

    private readonly IMediaSegmentManager _mediaSegmentManager = mediaSegmentManager;

    /// <summary>
    /// Gets the task name.
    /// </summary>
    public string Name => "Create EDL";

    /// <summary>
    /// Gets the task category.
    /// </summary>
    public string Category => "Media Analyzer";

    /// <summary>
    /// Gets the task description.
    /// </summary>
    public string Description => "Create .edl files from Media Segments.";

    /// <summary>
    /// Gets the task key.
    /// </summary>
    public string Key => "JFPEdlCreate";

    /// <summary>
    /// Create all .edl files which are not yet created but a media segments is available.
    /// </summary>
    /// <param name="progress">Task progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task.</returns>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_libraryManager is null)
        {
            throw new InvalidOperationException("Library manager was null");
        }

        var baseEdlTask = new BaseEdlTask(
            _loggerFactory.CreateLogger<CreateEdlTask>());

        var queueManager = new QueueManager(_loggerFactory.CreateLogger<QueueManager>(), _libraryManager);

        var segmentsList = new List<MediaSegmentDto>();
        // get ItemIds
        var mediaItems = queueManager.GetMediaItems();
        // get MediaSegments from itemIds
        foreach (var kvp in mediaItems)
        {
            foreach (var media in kvp.Value)
            {
                segmentsList.AddRange(await _mediaSegmentManager.GetSegmentsAsync(media.ItemId, null, true).ConfigureAwait(false));
            }
        }

        // write edl files
        baseEdlTask.CreateEdls(progress, segmentsList, false, cancellationToken);

        return;
    }

    /// <summary>
    /// Get task triggers.
    /// </summary>
    /// <returns>Task triggers.</returns>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }
}
