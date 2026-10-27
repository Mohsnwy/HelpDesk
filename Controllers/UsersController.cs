using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Controllers;

[ApiController, Route("api/users")]
public sealed class UsersController(HelpDeskDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentProfile() => Unauthorized(); // Bound to the authenticated principal in Stage 8.

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Role)) return ValidationProblem("Role is not supported.");
        if (request.DepartmentId is not null && !await db.Departments.AnyAsync(x => x.Id == request.DepartmentId, ct))
            return ValidationProblem("Department does not exist.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Conflict("A user with this email already exists.");
        // A non-empty sentinel is replaced by the PasswordHasher when authentication is added.
        var user = new User { FullName = request.FullName.Trim(), Email = email, Role = request.Role, DepartmentId = request.DepartmentId };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Created($"/api/users/{user.Id}", ToResponse(user));
    }

    internal static UserResponse ToResponse(User user) => new(user.Id, user.FullName, user.Email, user.DepartmentId, user.Role, user.IsActive, user.CreatedAt);
}
