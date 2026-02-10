using System;
using Microsoft.AspNetCore.Http;

namespace API.DTOs;

public class CreatePostFormDto
{
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Excerpt { get; set; }
    public string ContentFormat { get; set; } = "markdown";
    public string ContentBody { get; set; } = "";

    public List<Guid> TagIds { get; set; } = new();
    public List<Guid> CategoryIds { get; set; } = new();

    public IFormFile? FeaturedImage { get; set; }
    public string? FeaturedImageAlt { get; set; }
}

public record PostDto(
    Guid Id,
    string Title,
    string Slug,
    string? Excerpt,
    string ContentFormat,
    string ContentBody,
    string Status,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    object? FeaturedImage
);
