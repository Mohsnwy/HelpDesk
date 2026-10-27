using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Controllers;

[ApiController, Route("api/departments")]
public sealed class DepartmentsController(HelpDeskDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Department>>> Get(CancellationToken ct) =>
        Ok(await db.Departments.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<Department>> Create(CreateLookupRequest request, CancellationToken ct)
    {
        var department = new Department { Name = request.Name.Trim() };
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return Created($"/api/departments/{department.Id}", department);
    }
}
