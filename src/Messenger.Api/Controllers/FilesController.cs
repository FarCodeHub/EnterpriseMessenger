using System.Security.Claims;
using Messenger.Application.Files.DownloadFile;
using Messenger.Application.Files.UploadFile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Controllers;
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class FilesController : ControllerBase
{
    private readonly IFileUploader _fileUploader;
    private readonly IAttachmentDownloader _attachmentDownloader;

    public FilesController(
        IFileUploader fileUploader,
        IAttachmentDownloader attachmentDownloader)
    {
        _fileUploader = fileUploader;
        _attachmentDownloader = attachmentDownloader;
    }

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

            return Ok(result);
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