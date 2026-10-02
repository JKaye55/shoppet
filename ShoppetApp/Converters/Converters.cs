using System.Globalization;

namespace ShoppetApp.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 1.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class IsNotNullOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StringEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class IsoDateFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && DateTime.TryParse(s, out var dt))
        {
            if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
            return dt.ToString(parameter?.ToString() ?? "MMM d, yyyy", culture);
        }
        if (value is DateTime d)
        {
            if (d.Kind == DateTimeKind.Utc) d = d.ToLocalTime();
            return d.ToString(parameter?.ToString() ?? "MMM d, yyyy", culture);
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StatusBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLowerInvariant();
        return status switch
        {
            "overdue" => Application.Current?.Resources["StatusOverdueBg"] ?? Colors.White,
            "due" => Application.Current?.Resources["StatusDueBg"] ?? Colors.White,
            _ => Application.Current?.Resources["StatusHealthyBg"] ?? Colors.White
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLowerInvariant();
        return status switch
        {
            "overdue" => Application.Current?.Resources["StatusOverdue"] ?? Colors.Red,
            "due" => Application.Current?.Resources["StatusDue"] ?? Colors.Orange,
            _ => Application.Current?.Resources["StatusHealthy"] ?? Colors.Green
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class CountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            System.Collections.ICollection c => c.Count,
            _ => 0
        };
        var invert = parameter?.ToString() == "invert";
        var hasItems = count > 0;
        return invert ? !hasItems : hasItems;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class UrlToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var url = value?.ToString();
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var trimmed = url.Trim();
        bool isAndroid = DeviceInfo.Platform == DevicePlatform.Android;
        string hostPrefix = isAndroid ? "http://10.0.2.2:5253" : "http://localhost:5253";

        if (trimmed.StartsWith("/"))
        {
            trimmed = hostPrefix + trimmed;
        }
        else if (isAndroid)
        {
            trimmed = trimmed.Replace("http://localhost:", "http://10.0.2.2:")
                             .Replace("https://localhost:", "https://10.0.2.2:")
                             .Replace("http://127.0.0.1:", "http://10.0.2.2:")
                             .Replace("https://127.0.0.1:", "https://10.0.2.2:");
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return ImageSource.FromUri(uri);
        }

        return ImageSource.FromFile(trimmed);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StatusLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
        {
            "overdue" => "Overdue",
            "due" => "Due soon",
            _ => "Healthy"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HealthTypeLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
        {
            "vaccine" => "\uD83D\uDC89 Vaccine",
            "medication" => "\uD83D\uDC8A Meds",
            "vital" => "\u2764\uFE0F Vital",
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HealthTypeBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
        {
            "vaccine" => Application.Current?.Resources["Accent"] ?? Colors.White,
            "medication" => Application.Current?.Resources["Highlight"] ?? Colors.White,
            _ => Application.Current?.Resources["Primary10"] ?? Colors.White
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HealthTypeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
        {
            "medication" => Application.Current?.Resources["Amber800"] ?? Colors.Black,
            _ => Application.Current?.Resources["Primary"] ?? Colors.Black
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EmergencyBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return Application.Current?.Resources["StatusOverdueBg"] ?? Colors.White;
        return Application.Current?.Resources["Secondary05"] ?? Colors.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EmergencyTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return Application.Current?.Resources["StatusOverdue"] ?? Colors.Red;
        return Application.Current?.Resources["SecondaryFg"] ?? Colors.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EmergencyPhoneBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return Application.Current?.Resources["StatusOverdue"] ?? Colors.Red;
        return Application.Current?.Resources["Primary"] ?? Colors.Teal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Accepts the total interval as a double (minutes). Shows "Every 8h 30m" or "Every 45 min" etc.
/// If value is a FoodLog, reads its IntervalHours + IntervalMinutes directly.
/// </summary>
public class FeedingIntervalLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int hours = 0, minutes = 0;

        if (value is ShoppetApp.Models.FoodLog log)
        {
            hours = log.IntervalHours;
            minutes = log.IntervalMinutes;
        }
        else if (value is double d)
        {
            hours = (int)d;
            minutes = (int)Math.Round((d - hours) * 60);
        }

        if (hours == 0 && minutes == 0)
            return "�";

        if (hours == 0)
            return $"Every {minutes} min";
        if (minutes == 0)
            return $"Every {hours} hr{(hours != 1 ? "s" : "")}";
        return $"Every {hours}h {minutes}m";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Accepts a FoodLog and returns a friendly "Next: 3:45 PM" or "Next: Tomorrow 8:00 AM" label.
/// Returns empty string if no interval or start time is set.
/// </summary>
public class NextFeedingLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ShoppetApp.Models.FoodLog log)
            return string.Empty;

        var next = log.NextFeedingAt;
        if (next is null)
            return string.Empty;

        var now = DateTime.Now;
        if (next < now)
            return "Due now";

        var diff = next.Value - now;
        if (diff.TotalMinutes < 60)
            return $"In {(int)diff.TotalMinutes} min";

        // Show time on the same day or prefix with "Tomorrow"
        var timeStr = next.Value.ToString("h:mm tt");
        if (next.Value.Date == now.Date)
            return $"Next at {timeStr}";
        if (next.Value.Date == now.Date.AddDays(1))
            return $"Tomorrow {timeStr}";
        return $"Next {next.Value:MMM d}, {timeStr}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Accepts a FoodLog and returns the LastFedTimestamp as a friendly "Last fed: Today 3:45 PM" string.
/// </summary>
public class LastFedLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ShoppetApp.Models.FoodLog log || string.IsNullOrEmpty(log.LastFedTimestamp))
            return string.Empty;

        if (!DateTime.TryParse(log.LastFedTimestamp, out var dt))
            return string.Empty;
            
        if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();

        return $"Last fed: {dt.ToString("MMM d, h:mmtt")}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}


public class HealthScheduleLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ShoppetApp.Models.HealthLog log)
            return string.Empty;

        if (log.Completed)
        {
            if (log.CompletedAt.HasValue)
            {
                var diff = DateTime.Now - log.CompletedAt.Value;
                if (diff.TotalMinutes < 60)
                    return $"Completed {(int)diff.TotalMinutes} min ago";
                if (diff.TotalHours < 24)
                    return $"Completed {(int)diff.TotalHours} hr ago";
                if (diff.TotalDays < 2)
                    return "Completed yesterday";
                return $"Completed {log.CompletedAt.Value:MMM d, yyyy}";
            }
            return "Completed";
        }

        if (!DateTime.TryParse(log.DueDate, out var next))
            return string.Empty;

        var now = DateTime.Now;
        if (next < now)
            return "Due now";

        var dueDiff = next - now;
        if (dueDiff.TotalMinutes < 60)
            return $"In {(int)dueDiff.TotalMinutes} min";

        var timeStr = next.ToString("h:mm tt");
        if (next.Date == now.Date)
            return $"Today {timeStr}";
        if (next.Date == now.Date.AddDays(1))
            return $"Tomorrow {timeStr}";
        if (dueDiff.TotalDays < 7)
            return $"In {(int)dueDiff.TotalDays} days";

        return $"Due {next:MMM d, yyyy}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToLikeTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLiked) return isLiked ? "\u2764 Liked" : "\u2661 Like";
        return "\u2661 Like";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BoolToLikeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLiked) return isLiked ? Color.FromArgb("#E0245E") : Color.FromArgb("#AAAAAA");
        return Color.FromArgb("#AAAAAA");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}





public class LikeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Color.FromArgb("#E0245E") : Color.FromArgb("#AAAAAA");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InitialsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value as string;
        return string.IsNullOrWhiteSpace(name) ? "?" : name.Substring(0, 1).ToUpper();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HeartIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "\u2764" : "\u2661";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToHeartIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLiked) return isLiked ? "\u2764" : "\u2661";
        return "\u2661";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class DepthToMarginConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is int depth)
        {
            return new Microsoft.Maui.Thickness(depth * 60, 0, 0, 10);
        }
        return new Microsoft.Maui.Thickness(0, 0, 0, 10);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}


public class BoolToLayoutOptionsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool b && b ? LayoutOptions.End : LayoutOptions.Start;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class IsMineColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool b && b ? Application.Current?.Resources["Primary"] ?? Colors.Teal : Color.FromArgb("#F0F0F0");
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class IsMineTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool b && b ? Colors.White : Application.Current?.Resources["BodyText"] ?? Colors.Black;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class IsMineTimeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool b && b ? Color.FromArgb("#DDDDDD") : Application.Current?.Resources["NavMuted"] ?? Colors.Gray;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}




public class Base64ToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string base64 && !string.IsNullOrEmpty(base64))
        {
            try
            {
                byte[] imageBytes = System.Convert.FromBase64String(base64);
                return ImageSource.FromStream(() => new MemoryStream(imageBytes));
            }
            catch { return null; }
        }
        return null;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();

// IsReadColorConverter: ✓ = gray (sent), ✓✓ = bright blue (seen)
public class IsReadColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRead) return isRead ? Color.FromArgb("#4FC3F7") : Color.FromArgb("#AAAAAA");
        return Color.FromArgb("#AAAAAA");
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BoolToBoldConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b) return FontAttributes.Bold;
        return FontAttributes.None;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class BoolToObjectConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string paramStr && paramStr.Contains('|'))
        {
            var parts = paramStr.Split('|');
            bool isTrue = value is bool b && b;
            var choice = isTrue ? parts[0] : parts[1];

            if (targetType == typeof(Color))
            {
                if (choice.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) return Colors.Transparent;
                return Color.FromArgb(choice);
            }
            return choice;
        }
        return value;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
}