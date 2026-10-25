using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;

namespace HelpDesk.Api.Tests;

public class TicketsApiTests
{
    [Fact]
    public async Task Crud_endpoints_work_against_postgresql()
    {
        PostgreSqlContainer postgres;
        try
        {
            postgres = new PostgreSqlBuilder().WithImage("postgres:17").Build();
            await postgres.StartAsync();
        }
        catch (DockerUnavailableException exception)
        {
            var reason = $"PostgreSQL integration test needs a working Docker daemon. {exception.Message}";
            throw Xunit.Sdk.SkipException.ForSkip(reason);
        }

        await using var container = postgres;
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__HelpDesk");
        Environment.SetEnvironmentVariable("ConnectionStrings__HelpDesk", container.GetConnectionString());

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            var invalidResponse = await client.PostAsJsonAsync("/api/tickets", new
            {
                title = " ",
                description = "Missing required title",
                priority = "Medium"
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

            var invalidCategory = await client.PostAsJsonAsync("/api/tickets", new
            {
                title = "Missing category",
                description = "This lookup does not exist.",
                priority = "Medium",
                categoryId = 999
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidCategory.StatusCode);

            var createResponse = await client.PostAsJsonAsync("/api/tickets", new
            {
                title = "VPN connection issue",
                description = "Unable to connect to the company VPN.",
                priority = "High"
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createResponse.Headers.Location);
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            jsonOptions.Converters.Add(new JsonStringEnumConverter());
            var created = await createResponse.Content.ReadFromJsonAsync<TicketResponse>(jsonOptions);
            Assert.NotNull(created);

            var getResponse = await client.GetAsync(createResponse.Headers.Location);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var list = await client.GetFromJsonAsync<List<TicketResponse>>("/api/tickets", jsonOptions);
            Assert.NotNull(list);
            Assert.Single(list);

            var updateResponse = await client.PutAsJsonAsync($"/api/tickets/{created.Id}", new
            {
                title = created.Title,
                description = created.Description,
                priority = "Critical",
                status = "InProgress"
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var invalidTransition = await client.PutAsJsonAsync($"/api/tickets/{created.Id}", new
            {
                title = created.Title,
                description = created.Description,
                priority = "Critical",
                status = "Closed"
            });
            Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tickets/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tickets/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/tickets/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/tickets/9876", new
            {
                title = "No ticket",
                description = "No ticket",
                priority = "Low",
                status = "Open"
            })).StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__HelpDesk", previousConnectionString);
        }
    }
}
