using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.MediaSegments;

namespace Jellyfin.Plugin.Edl.SheduledTasks;

/// <summary>
/// Common code shared by all edl creator tasks.
/// </summary>
public class BaseEdlTask(IEdlManager edlManager)
{
    private readonly IEdlManager _edlManager = edlManager;

    /// <summary>
    /// Create edls for all Segments on the server.
    /// </summary>
    /// <param name="progress">Progress.</param>
    /// <param name="segmentsQueue">Media segments.</param>
    /// <param name="forceOverwrite">Force the file overwrite.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public void CreateEdls(
        IProgress<double> progress,
        IReadOnlyCollection<MediaSegmentDto> segmentsQueue,
        bool forceOverwrite,
        CancellationToken cancellationToken)
    {
        var sortedSegments = segmentsQueue
            .GroupBy(s => s.ItemId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.StartTicks).ToList());

        var totalQueued = sortedSegments.Count;

        _edlManager.LogConfiguration();

        var totalProcessed = 0;
        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Plugin.Instance!.Configuration.MaxParallelism
        };

        Parallel.ForEach(sortedSegments, options, (segment) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            _edlManager.UpdateEDLFile(segment, forceOverwrite);
            Interlocked.Add(ref totalProcessed, 1);

            progress.Report(totalProcessed * 100 / totalQueued);
        });
    }
}
