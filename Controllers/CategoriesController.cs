using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Controllers;

[ApiController, Route("api/categories"), Authorize]
public sealed class CategoriesController(HelpDeskDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Category>>> Get(CancellationToken ct) =>
        Ok(await db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Category>> Create(CreateLookupRequest request, CancellationToken ct)
    {
        var category = new Category { Name = request.Name.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return Created($"/api/categories/{category.Id}", category);
    }
}
