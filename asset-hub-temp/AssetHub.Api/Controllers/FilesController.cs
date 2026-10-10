using System;
using System.IO;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssetHub.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;

    public FilesController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        // Basic security check to prevent uploading executables
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var blockedExtensions = new[] { ".exe", ".bat", ".cmd", ".ps1", ".sh" };
        if (Array.Exists(blockedExtensions, ext => ext == extension))
        {
            return BadRequest(new { error = "File type not allowed." });
        }

        using var stream = file.OpenReadStream();
        var url = await _fileStorageService.UploadFileAsync(stream, file.FileName, file.ContentType);

        return Ok(new { url, fileName = file.FileName, contentType = file.ContentType, sizeBytes = file.Length });
    }
}
