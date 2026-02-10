using System;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace API.Services;


public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        var cloudName = config["Cloudinary:CloudName"];
        var apiKey = config["Cloudinary:ApiKey"];
        var apiSecret = config["Cloudinary:ApiSecret"];

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder)
    {
        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException(result.Error.Message);

        return new CloudinaryUploadResult(
            Url: result.SecureUrl.ToString(),
            PublicId: result.PublicId,
            Bytes: result.Bytes,
            Width: result.Width,
            Height: result.Height,
            Format: result.Format
        );
    }

    public async Task DeleteAsync(string publicId)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return;

        var del = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
        await _cloudinary.DestroyAsync(del);
    }
}
