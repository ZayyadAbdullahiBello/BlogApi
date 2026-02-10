using System;

namespace API.Models;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ICollection<PostCategory> PostCategories { get; set; } = new List<PostCategory>();
}
