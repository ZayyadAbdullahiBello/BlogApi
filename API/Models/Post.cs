using System;

namespace API.Models;

public class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Excerpt { get; set; }

    public string ContentFormat { get; set; } = "markdown"; // markdown|html
    public string ContentBody { get; set; } = "";

    public PostStatus Status { get; set; } = PostStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Author
    public string AuthorId { get; set; } = "";
    public AppUser? Author { get; set; }

    // Featured image stored in Cloudinary
    public string? FeaturedImageUrl { get; set; }
    public string? FeaturedImagePublicId { get; set; }
    public string? FeaturedImageAlt { get; set; }

    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    public ICollection<PostCategory> PostCategories { get; set; } = new List<PostCategory>();

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }


}

