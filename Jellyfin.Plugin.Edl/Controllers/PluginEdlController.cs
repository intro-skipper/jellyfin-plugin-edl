using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Edl.Managers;
using Jellyfin.Plugin.Edl.SheduledTasks;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.MediaSegments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Edl.Controllers;

/// <summary>
/// PluginEdl controller.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginEdlController"/> class.
/// </remarks>
/// <param name="mediaSegmentManager">MediaSegmentManager.</param>
/// <param name="edlManager">EdlManager.</param>
[Authorize(Policy = "RequiresElevation")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Route("PluginEdl")]
public class PluginEdlController(
    IMediaSegmentManager mediaSegmentManager,
    IEdlManager edlManager) : ControllerBase
{
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

        var item = Plugin.Instance!.GetItem(itemId) ?? throw new ArgumentNullException(nameof(itemId), "Item not found");
        segmentsList.AddRange(await _mediaSegmentManager.GetSegmentsAsync(item, null, new LibraryOptions()).ConfigureAwait(false));

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
        if (itemIds is null || itemIds.Length == 0)
        {
            throw new ArgumentNullException(nameof(itemIds));
        }

        var baseEdlTask = new BaseEdlTask(_edlManager);

        var segmentsList = new List<MediaSegmentDto>();

        foreach (var id in itemIds)
        {
            var item = Plugin.Instance!.GetItem(id);
            if (item is null)
            {
                continue;
            }

            segmentsList.AddRange(await _mediaSegmentManager.GetSegmentsAsync(item, null, new LibraryOptions()).ConfigureAwait(false));
        }

        IProgress<double> progress = new Progress<double>();
        CancellationToken cancellationToken = CancellationToken.None;

        // write edl files
        baseEdlTask.CreateEdls(progress, segmentsList, true, cancellationToken);

        return new OkResult();
    }
}
