using SQLite;

namespace ShoppetApp.Models;

public class HealthLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int PetId { get; set; }

    /// <summary>"vaccine" | "medication" | "vital"</summary>
    public string Type { get; set; } = "vaccine";

    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string VetName { get; set; } = string.Empty;
    public string RecordDate { get; set; } = string.Empty;

    /// <summary>Next Appointment / Due Date (ISO 8601 string)</summary>
    public string DueDate { get; set; } = string.Empty;

    public bool Completed { get; set; }

    public DateTime? CompletedAt { get; set; }

    // ── VACCINE Fields ─────────────────────────────────────────────────────────

    /// <summary>ISO 8601 string</summary>
    public string DateAdministered { get; set; } = string.Empty;
    public int ValidityInterval { get; set; }
    /// <summary>"Weeks", "Months", "Years"</summary>
    public string ValidityUnit { get; set; } = "Months";

    // ── MEDICATION Fields ──────────────────────────────────────────────────────

    public double MedicationIntervalHours { get; set; }
    /// <summary>ISO 8601 string (Date + Time)</summary>
    public string TimeStarted { get; set; } = string.Empty;
    public int DosageTotal { get; set; }
    public int DosageRemaining { get; set; }

    // ── CHECKUP/VITAL Fields ───────────────────────────────────────────────────

    /// <summary>ISO 8601 string</summary>
    public string CheckupDate { get; set; } = string.Empty;
    /// <summary>Pipe-separated file paths of uploaded documents</summary>
    public string DocumentPaths { get; set; } = string.Empty;

    // ── Computed helpers ───────────────────────────────────────────────────────

    [Ignore]
    public string Status { get; set; } = "healthy";

    [Ignore]
    public string? PetName { get; set; }

    [Ignore]
    public string? PetPhotoUrl { get; set; }

    [Ignore]
    public bool IsVaccine => Type.Equals("vaccine", StringComparison.OrdinalIgnoreCase);

    [Ignore]
    public bool IsMedication => Type.Equals("medication", StringComparison.OrdinalIgnoreCase);

    [Ignore]
    public bool IsCheckup => Type.Equals("vital", StringComparison.OrdinalIgnoreCase);

    [Ignore]
    public string BaseDateDisplay => IsVaccine ? DateAdministered : IsMedication ? TimeStarted : CheckupDate;

    [Ignore]
    public string NextDateFormatted
    {
        get
        {
            if (!DateTime.TryParse(DueDate, out var dt)) return "";
            string format = (IsVaccine || IsMedication) ? "MMM d, yyyy, h:mm tt" : "MMM d, h:mm tt";
            return dt.ToString(format);
        }
    }

    [Ignore]
    public string[] DocumentsList => string.IsNullOrEmpty(DocumentPaths)
        ? []
        : DocumentPaths.Split('|', StringSplitOptions.RemoveEmptyEntries);
}

