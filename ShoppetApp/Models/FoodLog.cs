using SQLite;

namespace ShoppetApp.Models;

public class FoodLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int PetId { get; set; }

    public string FoodName { get; set; } = string.Empty;
    public double AmountGrams { get; set; }
    public int IntervalHours { get; set; }
    public int IntervalMinutes { get; set; }
    public string StartTimestamp { get; set; } = string.Empty;
    public string LastFedTimestamp { get; set; } = string.Empty;
    public string FedDate { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    [Ignore]
    public bool HasLastFed => !string.IsNullOrEmpty(LastFedTimestamp);

    [Ignore]
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    [Ignore]
    public bool HasNextFeeding => NextFeedingAt is not null;

    [Ignore]
    public double TotalIntervalMinutes => IntervalHours * 60.0 + IntervalMinutes;

    [Ignore]
    public DateTime? NextFeedingAt
    {
        get
        {
            var baseTime = string.IsNullOrEmpty(LastFedTimestamp)
                ? (string.IsNullOrEmpty(StartTimestamp) ? (DateTime?)null : ParseIso(StartTimestamp))
                : ParseIso(LastFedTimestamp);

            if (baseTime is null || TotalIntervalMinutes <= 0)
                return null;

            return baseTime.Value.AddMinutes(TotalIntervalMinutes);
        }
    }

    private static DateTime? ParseIso(string iso) =>
        DateTime.TryParse(iso, out var dt)
            ? (dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt)
            : null;
}

