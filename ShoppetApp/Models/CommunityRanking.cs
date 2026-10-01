namespace ShoppetApp.Models;

public class CommunityRanking
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public int PostCount { get; set; }
    public int LikesReceived { get; set; }
    public int CommentCount { get; set; }
    public int Score { get; set; }

    public string RankBadge => Rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"#{Rank}"
    };

    public string Initial => string.IsNullOrWhiteSpace(FullName) ? "U" : FullName.Trim()[0].ToString().ToUpperInvariant();
    public string DisplayStats => $"{PostCount} posts • {LikesReceived} likes • {CommentCount} cmts";
}
