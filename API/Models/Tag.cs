using System;

namespace API.Models;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
}
