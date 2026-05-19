using Microsoft.AspNetCore.Mvc;
using Messenger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace Messenger.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class FilesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly MessengerDbContext _dbContext;

    public FilesController(
        IConfiguration configuration,
        MessengerDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        var rootPath = _configuration["FileStorage:RootPath"];

        if (string.IsNullOrWhiteSpace(rootPath))
            return BadRequest("File storage root path is not configured.");

        Directory.CreateDirectory(rootPath);

        var fileId = Guid.NewGuid();

        var extension = Path.GetExtension(file.FileName);

        var storedFileName = $"{fileId}{extension}";

        var storagePath = Path.Combine(rootPath, storedFileName);

        await using var stream = System.IO.File.Create(storagePath);

        await file.CopyToAsync(stream, cancellationToken);

        return Ok(new
        {
            FileId = fileId,
            OriginalFileName = file.FileName,
            file.ContentType,
            file.Length,
            StoragePath = storagePath
        });
    }

    [Obsolete("This endpoint is for testing purposes only. Use the /api/v1/files/{attachmentId}/download endpoint to download attachments.")]
    [HttpGet("download")]
    public IActionResult DownloadFile(string storagePath, string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return BadRequest("Storage path is required.");

        if (!System.IO.File.Exists(storagePath))
            return NotFound("File not found.");

        var fileStream = System.IO.File.OpenRead(storagePath);

        return File(fileStream, contentType, fileName);
    }

    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await _dbContext.Attachments
            .FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);

        if (attachment is null)
            return NotFound("Attachment not found.");

        if (!System.IO.File.Exists(attachment.StoragePath))
            return NotFound("File not found on storage.");

        var fileStream = System.IO.File.OpenRead(attachment.StoragePath);

        return File(
            fileStream,
            attachment.ContentType,
            attachment.FileName);
    }

}