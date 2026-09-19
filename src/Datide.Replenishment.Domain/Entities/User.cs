namespace Datide.Replenishment.Domain.Entities;

public enum UserRole
{
    /// <summary>Warehouse staff: manage SKUs, raise purchase orders.</summary>
    Staff = 0,

    /// <summary>Manager: everything staff can do, plus final PO approval.</summary>
    Manager = 1,
}

/// <summary>
/// Application user. Passwords are stored only as hashes (hashed with
/// ASP.NET Core PasswordHasher during seeding / account creation).
/// </summary>
public sealed class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Staff;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
