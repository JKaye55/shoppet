namespace ShoppetApp.Models;

public class NotificationItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TimeDisplay => CreatedAt.ToLocalTime().ToString("MMM d, h:mm tt");
}
