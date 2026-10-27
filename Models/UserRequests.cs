using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Api.Models;

public sealed class CreateUserRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(320)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(12), StringLength(128)] public string Password { get; set; } = string.Empty;
    [Required] public UserRole Role { get; set; } = UserRole.Employee;
    [Range(1, int.MaxValue)] public int? DepartmentId { get; set; }
}

public sealed record UserResponse(int Id, string FullName, string Email, int? DepartmentId, UserRole Role, bool IsActive, DateTime CreatedAt);

public sealed class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);

public sealed class CreateLookupRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
}
