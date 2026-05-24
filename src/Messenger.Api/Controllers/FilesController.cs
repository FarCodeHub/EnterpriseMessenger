using System.Security.Claims;
using Messenger.Application.Files.DownloadFile;
using Messenger.Application.Files.GetDownloadUrl;
using Messenger.Application.Files.UploadFile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Messenger.Contracts.Files;
using Messenger.Api.Common;

namespace Messenger.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class FilesController : ControllerBase
{
    private readonly IFileUploader _fileUploader;
    private readonly IAttachmentDownloader _attachmentDownloader;
    private readonly IAttachmentDownloadUrlGenerator _downloadUrlGenerator;

    public FilesController(
        IFileUploader fileUploader,
        IAttachmentDownloader attachmentDownloader,
        IAttachmentDownloadUrlGenerator downloadUrlGenerator)
    {
        _fileUploader = fileUploader;
        _attachmentDownloader = attachmentDownloader;
        _downloadUrlGenerator = downloadUrlGenerator;
    }




[ProducesResponseType(typeof(AttachmentDownloadUrlResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [Authorize]
    [HttpGet("{attachmentId:guid}/download-url")]
    public async Task<IActionResult> GetDownloadUrl(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var result = await _downloadUrlGenerator.GenerateAsync(
            new GetAttachmentDownloadUrlQuery(
                attachmentId,
                GetCurrentUserId()),
            cancellationToken);

        return Ok(new AttachmentDownloadUrlResponse(
         result.AttachmentId,
         result.FileName,
         result.ContentType,
         result.Url,
         result.ExpiresInSeconds));
    }


[ProducesResponseType(typeof(UploadFileResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("messages")]
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadFile(
            IFormFile file,
            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _fileUploader.UploadAsync(
                new UploadFileCommand(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    file.OpenReadStream()),
                cancellationToken);


            return Ok(new UploadFileResponse(
                result.FileId,
                result.OriginalFileName,
                result.ContentType,
                result.Length,
                result.StoragePath));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _attachmentDownloader.DownloadAsync(
                   new DownloadAttachmentQuery(
            attachmentId,
            GetCurrentUserId()),
                cancellationToken);

            return File(
                result.Content,
                result.ContentType,
                result.FileName);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }


    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue))
            throw new UnauthorizedAccessException("UserId not found in token.");

        return Guid.Parse(userIdValue);
    }


}