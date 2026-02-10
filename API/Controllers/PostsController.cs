using System;
using API.Data;
using API.DTOs;
using API.Models;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/v1/posts")]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly ICloudinaryService _cloudinary;

    public PostsController(AppDbContext db, UserManager<AppUser> userManager, ICloudinaryService cloudinary)
    {
        _db = db;
        _userManager = userManager;
        _cloudinary = cloudinary;
    }

    // Public: list published
    [HttpGet("list")]
    public async Task<ActionResult<object>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == PostStatus.Published)
            .OrderByDescending(p => p.PublishedAt ?? DateTime.MinValue);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.Excerpt,
                p.PublishedAt,
                FeaturedImageUrl = p.FeaturedImageUrl
            })
            .ToListAsync();

        return Ok(new { items, page, pageSize, total });
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<object>> GetBySlug(string slug)
    {
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => !p.IsDeleted && p.Slug == slug && p.Status == PostStatus.Published);
        if (post is null) return NotFound();

        return Ok(new
        {
            post.Id, post.Title, post.Slug, post.Excerpt,
            post.ContentFormat, post.ContentBody,
            post.PublishedAt, post.UpdatedAt,
            featuredImage = post.FeaturedImageUrl is null ? null : new { url = post.FeaturedImageUrl, alt = post.FeaturedImageAlt }
        });
    }

    // Author/Admin: create post (multipart)
    [Authorize(Roles = "Admin,Author")]
    [HttpPost]
    [RequestSizeLimit(5_000_000)] // 5MB 
    public async Task<ActionResult<PostDto>> Create([FromForm] CreatePostFormDto dto)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(dto.Title)) return ValidationProblem("Title is required.");
        if (string.IsNullOrWhiteSpace(dto.Slug)) return ValidationProblem("Slug is required.");
        if (string.IsNullOrWhiteSpace(dto.ContentBody)) return ValidationProblem("ContentBody is required.");

        dto.ContentFormat = dto.ContentFormat?.ToLowerInvariant() ?? "markdown";
        if (dto.ContentFormat is not ("markdown" or "html"))
            return ValidationProblem("ContentFormat must be 'markdown' or 'html'.");

        // Unique slug
        var slugExists = await _db.Posts.AnyAsync(p => p.Slug == dto.Slug);
        if (slugExists) return Conflict(new { message = "Slug already exists." });

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        string? imageUrl = null;
        string? publicId = null;

        // Optional image upload
        if (dto.FeaturedImage is not null)
        {
            var ct = dto.FeaturedImage.ContentType.ToLowerInvariant();
            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowed.Contains(ct)) return ValidationProblem("FeaturedImage must be jpeg/png/webp.");

            var upload = await _cloudinary.UploadImageAsync(dto.FeaturedImage, "blog/posts");
            imageUrl = upload.Url;
            publicId = upload.PublicId;
        }

        var post = new Post
        {
            Title = dto.Title.Trim(),
            Slug = dto.Slug.Trim(),
            Excerpt = dto.Excerpt?.Trim(),
            ContentFormat = dto.ContentFormat,
            ContentBody = dto.ContentBody,
            Status = PostStatus.Draft,
            UpdatedAt = DateTime.UtcNow,
            AuthorId = user.Id,
            FeaturedImageUrl = imageUrl,
            FeaturedImagePublicId = publicId,
            FeaturedImageAlt = dto.FeaturedImageAlt?.Trim()
        };

        // Attach tags/categories
        foreach (var tagId in dto.TagIds.Distinct())
            post.PostTags.Add(new PostTag { PostId = post.Id, TagId = tagId });

        foreach (var catId in dto.CategoryIds.Distinct())
            post.PostCategories.Add(new PostCategory { PostId = post.Id, CategoryId = catId });

        _db.Posts.Add(post);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // If DB save fails after upload, consider deleting uploaded image
            if (!string.IsNullOrWhiteSpace(publicId))
                await _cloudinary.DeleteAsync(publicId);

            throw;
        }

        return Created($"/api/posts/{post.Id}", new PostDto(
            post.Id, post.Title, post.Slug, post.Excerpt,
            post.ContentFormat, post.ContentBody,
            post.Status.ToString().ToLowerInvariant(),
            post.PublishedAt,
            post.UpdatedAt,
            post.FeaturedImageUrl is null ? null : new
            {
                url = post.FeaturedImageUrl,
                publicId = post.FeaturedImagePublicId,
                alt = post.FeaturedImageAlt
            }
        ));
    }

    // Admin: publish a post
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

        if (post.Status == PostStatus.Published)
            return Conflict(new { message = "Post is already published." });

        post.Status = PostStatus.Published;
        post.PublishedAt = DateTime.UtcNow;
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            post.Id,
            Status = post.Status.ToString().ToLowerInvariant(),
            post.PublishedAt,
            post.UpdatedAt
        });
    }

    // Admin: unpublish a post (back to draft)
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

        if (post.Status == PostStatus.Draft)
            return Conflict(new { message = "Post is already a draft." });

        post.Status = PostStatus.Draft;
        post.UpdatedAt = DateTime.UtcNow;

        // Option A (default): keep PublishedAt for history
        // Option B: clear PublishedAt
        // post.PublishedAt = null;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            post.Id,
            Status = post.Status.ToString().ToLowerInvariant(),
            post.PublishedAt,
            post.UpdatedAt
        });
    }

    private async Task<bool> IsAdminAsync()
    => User.IsInRole("Admin");

    private async Task<bool> IsOwnerAsync(Models.Post post)
    {
        var user = await _userManager.GetUserAsync(User);
        return user != null && post.AuthorId == user.Id;
    }

    [Authorize(Roles = "Admin,Author")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var post = await _db.Posts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (post is null) return NotFound();

        // Admin can view all, Author can view only own
        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();
            if (post.AuthorId != user.Id) return Forbid();
        }

        return Ok(new
        {
            post.Id,
            post.Title,
            post.Slug,
            post.Excerpt,
            post.ContentFormat,
            post.ContentBody,
            status = post.Status.ToString().ToLowerInvariant(),
            post.PublishedAt,
            post.UpdatedAt,
            featuredImage = post.FeaturedImageUrl is null ? null : new
            {
                url = post.FeaturedImageUrl,
                publicId = post.FeaturedImagePublicId,
                alt = post.FeaturedImageAlt
            }
        });
    }



    [Authorize(Roles = "Admin,Author")]
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Update(Guid id, [FromForm] UpdatePostFormDto dto)
    {
        var post = await _db.Posts
            .Include(p => p.PostTags)
            .Include(p => p.PostCategories)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (post is null) return NotFound();

        // Admin can edit all. Author can edit only own.
        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();
            if (post.AuthorId != user.Id) return Forbid();

            // Author should not edit published posts (per our policy)
            if (post.Status == PostStatus.Published)
                return Conflict(new { message = "Authors cannot edit published posts. Ask an admin to unpublish first." });
        }

        // Validate
        if (string.IsNullOrWhiteSpace(dto.Title)) return ValidationProblem("Title is required.");
        if (string.IsNullOrWhiteSpace(dto.Slug)) return ValidationProblem("Slug is required.");
        if (string.IsNullOrWhiteSpace(dto.ContentBody)) return ValidationProblem("ContentBody is required.");

        dto.ContentFormat = dto.ContentFormat?.ToLowerInvariant() ?? "markdown";
        if (dto.ContentFormat is not ("markdown" or "html"))
            return ValidationProblem("ContentFormat must be 'markdown' or 'html'.");

        // Slug uniqueness (ignore current post)
        var slugExists = await _db.Posts.AnyAsync(p => p.Slug == dto.Slug && p.Id != id);
        if (slugExists) return Conflict(new { message = "Slug already exists." });

        // Image replacement (optional)
        string? oldPublicId = null;

        if (dto.FeaturedImage is not null)
        {
            var ct = dto.FeaturedImage.ContentType.ToLowerInvariant();
            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowed.Contains(ct)) return ValidationProblem("FeaturedImage must be jpeg/png/webp.");

            // Upload new first
            var upload = await _cloudinary.UploadImageAsync(dto.FeaturedImage, "blog/posts");

            // Remember old public id to delete after save
            oldPublicId = post.FeaturedImagePublicId;

            post.FeaturedImageUrl = upload.Url;
            post.FeaturedImagePublicId = upload.PublicId;
        }

        post.Title = dto.Title.Trim();
        post.Slug = dto.Slug.Trim();
        post.Excerpt = dto.Excerpt?.Trim();
        post.ContentFormat = dto.ContentFormat;
        post.ContentBody = dto.ContentBody;
        post.FeaturedImageAlt = dto.FeaturedImageAlt?.Trim();
        post.UpdatedAt = DateTime.UtcNow;

        // Replace tags/categories (simple approach)
        post.PostTags.Clear();
        foreach (var tagId in dto.TagIds.Distinct())
            post.PostTags.Add(new PostTag { PostId = post.Id, TagId = tagId });

        post.PostCategories.Clear();
        foreach (var catId in dto.CategoryIds.Distinct())
            post.PostCategories.Add(new PostCategory { PostId = post.Id, CategoryId = catId });

        await _db.SaveChangesAsync();

        // Best-effort delete old image AFTER DB update succeeds
        if (!string.IsNullOrWhiteSpace(oldPublicId))
        {
            try { await _cloudinary.DeleteAsync(oldPublicId); }
            catch { /* swallow best-effort */ }
        }

        return Ok(new
        {
            post.Id,
            post.Title,
            post.Slug,
            post.Excerpt,
            post.ContentFormat,
            post.ContentBody,
            status = post.Status.ToString().ToLowerInvariant(),
            post.PublishedAt,
            post.UpdatedAt,
            featuredImage = post.FeaturedImageUrl is null ? null : new
            {
                url = post.FeaturedImageUrl,
                publicId = post.FeaturedImagePublicId,
                alt = post.FeaturedImageAlt
            }
        });
    }

    [Authorize(Roles = "Admin,Author")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return NotFound();

        // Admin can delete any post (draft or published)
        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();
            if (post.AuthorId != user.Id) return Forbid();

            // Authors can only delete drafts (recommended)
            if (post.Status == PostStatus.Published)
                return Conflict(new { message = "Authors cannot delete published posts. Ask an admin to unpublish first." });
        }

        post.IsDeleted = true;
        post.DeletedAt = DateTime.UtcNow;
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }









}
