using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Edl.Managers;
using Jellyfin.Plugin.Edl.SheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.MediaSegments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Edl.Controllers;

/// <summary>
/// PluginEdl controller.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginEdlController"/> class.
/// </remarks>
/// <param name="loggerFactory">Logger factory.</param>
/// <param name="libraryManager">Library manager.</param>
/// <param name="mediaSegmentManager">MediaSegmentManager.</param>
/// <param name="edlManager">EdlManager.</param>
[Authorize(Policy = "RequiresElevation")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Route("PluginEdl")]
public class PluginEdlController(
    ILoggerFactory loggerFactory,
    ILibraryManager libraryManager,
    IMediaSegmentManager mediaSegmentManager,
    IEdlManager edlManager) : ControllerBase
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly IMediaSegmentManager _mediaSegmentManager = mediaSegmentManager;
    private readonly IEdlManager _edlManager = edlManager;

    /// <summary>
    /// Plugin meta endpoint.
    /// </summary>
    /// <returns>The version info.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public JsonResult GetPluginMetadata()
    {
        var json = new
        {
            version = Plugin.Instance!.Version.ToString(3),
        };

        return new JsonResult(json);
    }

    /// <summary>
    /// Get Edl data based on itemId.
    /// </summary>
    /// <param name="itemId">ItemId.</param>
    /// <returns>The edl data.</returns>
    [HttpGet("Edl/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<JsonResult> GetEdlData(
        [FromRoute, Required] Guid itemId)
    {
        var segmentsList = new List<MediaSegmentDto>();
        // get ItemIds
        var mediaItems = new QueueManager(_loggerFactory.CreateLogger<QueueManager>(), _libraryManager).GetMediaItemsById([itemId]);
        // get MediaSegments from itemIds
        foreach (var kvp in mediaItems)
        {
            foreach (var media in kvp.Value)
            {
                segmentsList.AddRange(await _mediaSegmentManager.GetSegmentsAsync(media.ItemId, null, true).ConfigureAwait(false));
            }
        }

        var rawstring = _edlManager.ToEdl(segmentsList);

        var json = new
        {
            itemId,
            edl = rawstring
        };

        return new JsonResult(json);
    }

    /// <summary>
    /// Force edl recreation for itemIds.
    /// </summary>
    /// <param name="itemIds">ItemIds.</param>
    /// <returns>Ok.</returns>
    [HttpPost("Edl")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<OkResult> GenerateData(
        [FromBody, Required] Guid[] itemIds)
    {
        var baseEdlTask = new BaseEdlTask(_edlManager);

        var segmentsList = new List<MediaSegmentDto>();
        // get ItemIds
        var mediaItems = new QueueManager(_loggerFactory.CreateLogger<QueueManager>(), _libraryManager).GetMediaItemsById(itemIds);
        // get MediaSegments from itemIds
        foreach (var kvp in mediaItems)
        {
            foreach (var media in kvp.Value)
            {
                segmentsList.AddRange(await _mediaSegmentManager.GetSegmentsAsync(media.ItemId, null, true).ConfigureAwait(false));
            }
        }

        IProgress<double> progress = new Progress<double>();
        CancellationToken cancellationToken = CancellationToken.None;

        // write edl files
        baseEdlTask.CreateEdls(progress, segmentsList, true, cancellationToken);

        return new OkResult();
    }
}
