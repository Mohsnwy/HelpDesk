using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace HelpDesk.Api.Controllers;

[ApiController, Route("api/users"), Authorize]
public sealed class UsersController(HelpDeskDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetCurrentProfile(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return user is null ? NotFound() : Ok(ToResponse(user));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Role)) return ValidationProblem("Role is not supported.");
        if (request.DepartmentId is not null && !await db.Departments.AnyAsync(x => x.Id == request.DepartmentId, ct))
            return ValidationProblem("Department does not exist.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Conflict("A user with this email already exists.");
        var user = new User { FullName = request.FullName.Trim(), Email = email, Role = request.Role, DepartmentId = request.DepartmentId };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Created($"/api/users/{user.Id}", ToResponse(user));
    }

    internal static UserResponse ToResponse(User user) => new(user.Id, user.FullName, user.Email, user.DepartmentId, user.Role, user.IsActive, user.CreatedAt);
}
