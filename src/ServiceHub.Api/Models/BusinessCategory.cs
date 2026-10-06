namespace ServiceHub.Api.Models;

public sealed class BusinessCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public ICollection<Business> Businesses { get; set; } = new List<Business>();
}
