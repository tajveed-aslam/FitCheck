namespace FitCheck.Api.Models;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>Temporary account created by the "Try the live demo" button.</summary>
    public bool IsGuest { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Analysis> Analyses { get; set; } = [];
}

public sealed class Analysis
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Role title, from the user or inferred from the job description.</summary>
    public string Title { get; set; } = "";
    public string? Company { get; set; }

    /// <summary>Original upload name. The file itself is never stored, only its extracted text.</summary>
    public string FileName { get; set; } = "";
    public string CvText { get; set; } = "";
    public string JobDescription { get; set; } = "";

    public int MatchScore { get; set; }
    public string Summary { get; set; } = "";
    /// <summary>Serialized <see cref="MatchDetails"/> (jsonb).</summary>
    public string DetailsJson { get; set; } = "{}";

    public string Model { get; set; } = "";
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
