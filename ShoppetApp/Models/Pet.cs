using SQLite;

namespace ShoppetApp.Models;

public class Pet
{
    [PrimaryKey]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>"Dog" | "Cat" | "Bird" | "Small Pet" | "Other"</summary>
    public string Species { get; set; } = "Dog";

    public string Breed { get; set; } = string.Empty;

    public int AgeYears { get; set; }

    /// <summary>Free text e.g. "25 kg"</summary>
    public string Weight { get; set; } = string.Empty;

    /// <summary>URL or local path</summary>
    public string PhotoUrl { get; set; } = string.Empty;
    public string Diet { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public DateTime? CardIssuedAt { get; set; }
    public string CardTheme { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    [Ignore]
    public bool HasDigitalPetId => !string.IsNullOrWhiteSpace(CardId);
}
