namespace ServiceHub.Api.Models;

public sealed class BusinessWorkingHours
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Business Business { get; set; } = null!;
}
