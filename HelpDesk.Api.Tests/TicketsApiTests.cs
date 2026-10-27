using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
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
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web => web.UseEnvironment("Testing"));
            using var client = factory.CreateClient();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
                await db.Database.EnsureCreatedAsync();
                var testUsers = new[]
                {
                    new User { FullName = "Test Employee", Email = "employee@example.test", Role = UserRole.Employee },
                    new User { FullName = "Test Agent", Email = "agent@example.test", Role = UserRole.Agent },
                    new User { FullName = "Test Admin", Email = "admin@example.test", Role = UserRole.Admin }
                };
                db.Users.AddRange(testUsers);
                var hasher = new PasswordHasher<User>();
                foreach (var user in testUsers) user.PasswordHash = hasher.HashPassword(user, "Testing-password-123!");
                await db.SaveChangesAsync();
            }

            using var unauthenticated = factory.CreateClient();
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            jsonOptions.Converters.Add(new JsonStringEnumConverter());
            Assert.Equal(HttpStatusCode.Unauthorized, (await unauthenticated.GetAsync("/api/tickets")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await unauthenticated.GetAsync("/api/categories")).StatusCode);
            var badLogin = await unauthenticated.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "incorrect" });
            Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "Testing-password-123!" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>(jsonOptions);
            Assert.NotNull(loginResult);
            Assert.True(loginResult.ExpiresAtUtc > DateTime.UtcNow);
            Assert.NotNull(loginResult.AccessToken);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult.AccessToken);
            using var invalidTokenClient = factory.CreateClient();
            invalidTokenClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");
            Assert.Equal(HttpStatusCode.Unauthorized, (await invalidTokenClient.GetAsync("/api/tickets")).StatusCode);
            var profile = await client.GetFromJsonAsync<UserResponse>("/api/users/me", jsonOptions);
            Assert.Equal("admin@example.test", profile!.Email);
            var createdUserResponse = await client.PostAsJsonAsync("/api/users", new
            {
                fullName = "New Employee", email = "new.employee@example.test", password = "New-password-123!", role = "Employee"
            });
            Assert.Equal(HttpStatusCode.Created, createdUserResponse.StatusCode);
            Assert.DoesNotContain("PasswordHash", await createdUserResponse.Content.ReadAsStringAsync());
            using var employeeClient = factory.CreateClient();
            var employeeLogin = await employeeClient.PostAsJsonAsync("/api/auth/login", new { email = "employee@example.test", password = "Testing-password-123!" });
            var employeeToken = (await employeeLogin.Content.ReadFromJsonAsync<LoginResponse>(jsonOptions))!;
            employeeClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", employeeToken.AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await employeeClient.PostAsJsonAsync("/api/categories", new { name = "Forbidden" })).StatusCode);

            var departments = await client.GetFromJsonAsync<List<Department>>("/api/departments");
            Assert.NotNull(departments);
            Assert.Contains(departments, department => department.Name == "Engineering");
            var newDepartment = await client.PostAsJsonAsync("/api/departments", new { name = "Customer Success" });
            Assert.Equal(HttpStatusCode.Created, newDepartment.StatusCode);
            var categories = await client.GetFromJsonAsync<List<Category>>("/api/categories");
            Assert.NotNull(categories);
            Assert.Contains(categories, category => category.Name == "IT Support");
            var newCategory = await client.PostAsJsonAsync("/api/categories", new { name = "Payroll" });
            Assert.Equal(HttpStatusCode.Created, newCategory.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/categories", new { name = " " })).StatusCode);

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
                priority = "High",
                categoryId = 1
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createResponse.Headers.Location);
            var created = await createResponse.Content.ReadFromJsonAsync<TicketResponse>(jsonOptions);
            Assert.NotNull(created);

            var secondCreate = await client.PostAsJsonAsync("/api/tickets", new
            {
                title = "Account access request",
                description = "Request access to the finance portal.",
                priority = "Low"
            });
            Assert.Equal(HttpStatusCode.Created, secondCreate.StatusCode);
            var secondTicket = (await secondCreate.Content.ReadFromJsonAsync<TicketResponse>(jsonOptions))!;

            Assert.Equal(HttpStatusCode.Forbidden,
                (await employeeClient.PutAsJsonAsync($"/api/tickets/{created.Id}/assignment", new { assigneeId = 2 })).StatusCode);
            var employeeCreate = await employeeClient.PostAsJsonAsync("/api/tickets", new
            {
                title = "Employee owned ticket", description = "Created by an employee", priority = "Low", requesterId = 2
            });
            Assert.Equal(HttpStatusCode.Created, employeeCreate.StatusCode);
            var employeeTicket = (await employeeCreate.Content.ReadFromJsonAsync<TicketResponse>(jsonOptions))!;
            Assert.Equal(1, employeeTicket.RequesterId);
            Assert.Equal(HttpStatusCode.NotFound, (await employeeClient.GetAsync($"/api/tickets/{created.Id}")).StatusCode);
            var employeeList = await employeeClient.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets", jsonOptions);
            Assert.Single(employeeList!.Items);
            await client.PostAsJsonAsync($"/api/tickets/{employeeTicket.Id}/comments", new { body = "Agent only note", isInternal = true });
            var employeeComments = await employeeClient.GetFromJsonAsync<List<TicketCommentResponse>>($"/api/tickets/{employeeTicket.Id}/comments", jsonOptions);
            Assert.NotNull(employeeComments);
            Assert.Empty(employeeComments);
            Assert.Equal(HttpStatusCode.Forbidden, (await employeeClient.PostAsJsonAsync($"/api/tickets/{employeeTicket.Id}/comments", new { body = "Spoof internal", isInternal = true })).StatusCode);
            using var agentClient = factory.CreateClient();
            var agentLogin = await agentClient.PostAsJsonAsync("/api/auth/login", new { email = "agent@example.test", password = "Testing-password-123!" });
            var agentToken = (await agentLogin.Content.ReadFromJsonAsync<LoginResponse>(jsonOptions))!;
            agentClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", agentToken.AccessToken);
            var agentComments = await agentClient.GetFromJsonAsync<List<TicketCommentResponse>>($"/api/tickets/{employeeTicket.Id}/comments", jsonOptions);
            Assert.Single(agentComments!);
            Assert.True(agentComments![0].IsInternal);
            Assert.Equal(HttpStatusCode.Created, (await agentClient.PostAsJsonAsync($"/api/tickets/{employeeTicket.Id}/comments", new
            {
                body = "Agent internal follow-up", isInternal = true
            })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await agentClient.PutAsJsonAsync($"/api/tickets/{employeeTicket.Id}/assignment", new { assigneeId = 2 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await agentClient.PostAsJsonAsync("/api/users", new
            {
                fullName = "Agent cannot create users", email = "blocked@example.test", password = "Blocked-password-123!", role = "Employee"
            })).StatusCode);
            var agentList = await agentClient.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets", jsonOptions);
            Assert.Equal(3, agentList!.TotalCount);

            var getResponse = await client.GetAsync(createResponse.Headers.Location);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var list = await client.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets", jsonOptions);
            Assert.NotNull(list);
            Assert.Equal(3, list.Items.Count);
            Assert.Equal(3, list.TotalCount);
            var multiFilter = await client.GetFromJsonAsync<PagedResult<TicketResponse>>(
                "/api/tickets?status=Open&priority=High&categoryId=1&search=VPN&createdFrom=2020-01-01T00%3A00%3A00Z", jsonOptions);
            Assert.NotNull(multiFilter);
            Assert.Single(multiFilter.Items);
            Assert.Equal(created.Id, multiFilter.Items[0].Id);

            var pageZero = await client.GetAsync("/api/tickets?page=0");
            Assert.Equal(HttpStatusCode.BadRequest, pageZero.StatusCode);
            var oversizedPage = await client.GetAsync("/api/tickets?pageSize=101");
            Assert.Equal(HttpStatusCode.BadRequest, oversizedPage.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/tickets?sortBy=unknown")).StatusCode);
            var emptyPage = await client.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets?status=Closed", jsonOptions);
            Assert.NotNull(emptyPage);
            Assert.Empty(emptyPage.Items);
            Assert.Equal(0, emptyPage.TotalPages);

            var commentResponse = await client.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new
            {
                authorId = 1,
                body = "  Investigating this issue.  ",
                isInternal = true
            });
            Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);
            var comments = await client.GetFromJsonAsync<List<TicketCommentResponse>>($"/api/tickets/{created.Id}/comments", jsonOptions);
            Assert.NotNull(comments);
            Assert.Single(comments);
            Assert.Equal("Investigating this issue.", comments[0].Body);
            Assert.True(comments[0].IsInternal);
            Assert.Equal(3, comments[0].AuthorId);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/tickets/9876/comments")).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new
            {
                authorId = 999,
                body = "Identity is taken from claims"
            })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new
            {
                authorId = 1,
                body = "   "
            })).StatusCode);

            var assigned = await client.PutAsJsonAsync($"/api/tickets/{created.Id}/assignment", new { assigneeId = 2 });
            Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
            var assignedTicket = await assigned.Content.ReadFromJsonAsync<TicketResponse>(jsonOptions);
            Assert.Equal(2, assignedTicket!.AssigneeId);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PutAsJsonAsync($"/api/tickets/{secondTicket.Id}/assignment", new { assigneeId = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await client.PutAsJsonAsync($"/api/tickets/{secondTicket.Id}/assignment", new { assigneeId = 2 })).StatusCode);
            var filtered = await client.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets?assigneeId=2&sortBy=title&sortDirection=asc", jsonOptions);
            Assert.NotNull(filtered);
            Assert.Equal(3, filtered.Items.Count);
            Assert.Equal("Account access request", filtered.Items[0].Title);
            Assert.Equal(secondTicket.Id, filtered.Items[0].Id);
            Assert.Equal(3, filtered.TotalCount);
            var firstPage = await client.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets?assigneeId=2&sortBy=title&sortDirection=asc&pageSize=1&page=1", jsonOptions);
            var secondPage = await client.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets?assigneeId=2&sortBy=title&sortDirection=asc&pageSize=1&page=2", jsonOptions);
            var thirdPage = await client.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets?assigneeId=2&sortBy=title&sortDirection=asc&pageSize=1&page=3", jsonOptions);
            Assert.Equal("Account access request", firstPage!.Items[0].Title);
            Assert.Equal("Employee owned ticket", secondPage!.Items[0].Title);
            Assert.Equal("VPN connection issue", thirdPage!.Items[0].Title);
            Assert.Equal(3, firstPage.TotalPages);

            var unassigned = await client.PutAsJsonAsync($"/api/tickets/{created.Id}/assignment", new { assigneeId = (int?)null });
            Assert.Equal(HttpStatusCode.OK, unassigned.StatusCode);
            Assert.Null((await unassigned.Content.ReadFromJsonAsync<TicketResponse>(jsonOptions))!.AssigneeId);

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
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/tickets/{secondTicket.Id}")).StatusCode);
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
