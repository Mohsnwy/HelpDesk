using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace HelpDesk.Api.Tests;

public class Stage5CatchUpTests
{
    [Fact]
    public async Task Exception_handler_returns_problem_details_without_internal_details()
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        context.Request.Path = "/api/tickets";

        Assert.True(await handler.TryHandleAsync(context, new InvalidOperationException("secret stack detail"), default));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        var json = await new StreamReader(body, Encoding.UTF8).ReadToEndAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.Equal("An unexpected error occurred.", problem.Title);
        Assert.Null(problem.Detail);
        Assert.DoesNotContain("secret stack detail", json);
        Assert.DoesNotContain("StackTrace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_request_validation_rejects_missing_and_oversized_fields()
    {
        var missing = new CreateTicketRequest { Title = " ", Description = "Description" };
        Assert.Contains(Validate(missing), result => result.MemberNames.Contains(nameof(missing.Title)));

        var oversized = new CreateTicketRequest { Title = new string('x', 201), Description = "Description" };
        Assert.Contains(Validate(oversized), result => result.MemberNames.Contains(nameof(oversized.Title)));
    }

    [Fact]
    public async Task Ticket_service_creates_and_reads_tickets()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        try
        {
            await postgres.StartAsync();
        }
        catch (DockerUnavailableException exception)
        {
            throw Xunit.Sdk.SkipException.ForSkip($"Ticket service integration test needs a working Docker daemon. {exception.Message}");
        }

        var options = new DbContextOptionsBuilder<HelpDeskDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new HelpDeskDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new TicketService(db);
        var created = await service.CreateAsync(new CreateTicketRequest
        {
            Title = "  Printer issue  ", Description = "  Paper jam  ", Priority = TicketPriority.High
        }, default);

        var found = await service.GetByIdAsync(created.Id, default);
        Assert.NotNull(found);
        Assert.Equal("Printer issue", found.Title);
        Assert.Equal("Paper jam", found.Description);
        Assert.Equal(created.Id, found.Id);
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
