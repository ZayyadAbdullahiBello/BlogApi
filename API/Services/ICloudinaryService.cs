using System;
using Microsoft.AspNetCore.Http;

namespace API.Services;
public record CloudinaryUploadResult(string Url, string PublicId, long Bytes, int Width, int Height, string Format);

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder);
    Task DeleteAsync(string publicId);
}
