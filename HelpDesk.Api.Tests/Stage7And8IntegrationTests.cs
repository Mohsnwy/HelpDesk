using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HelpDesk.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace HelpDesk.Api.Tests;

[Collection(StageApiIntegrationCollection.Name)]
public sealed class Stage7And8IntegrationTests
{
    [Fact]
    public async Task Login_returns_a_token_and_uses_the_same_generic_failure_for_bad_password_or_unknown_email()
    {
        await using var host = await ApiTestHost.StartAsync();
        using var client = host.CreateClient();

        var success = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "employee1@example.test", password = ApiTestHost.Password
        });
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        var login = await success.Content.ReadFromJsonAsync<LoginResponse>(ApiTestHost.JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(login?.AccessToken));
        Assert.True(login!.ExpiresAtUtc > DateTime.UtcNow);

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "employee1@example.test", password = "wrong-password"
        });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unknown@example.test", password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        using var wrongBody = JsonDocument.Parse(await wrongPassword.Content.ReadAsStringAsync());
        using var unknownBody = JsonDocument.Parse(await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal(wrongBody.RootElement.GetProperty("title").GetString(), unknownBody.RootElement.GetProperty("title").GetString());
        Assert.Equal(wrongBody.RootElement.GetProperty("detail").GetString(), unknownBody.RootElement.GetProperty("detail").GetString());
        Assert.Equal("Invalid credentials", wrongBody.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Protected_routes_reject_missing_malformed_and_expired_tokens()
    {
        await using var host = await ApiTestHost.StartAsync();
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/me")).StatusCode);

        using var malformed = host.CreateClient();
        malformed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await malformed.GetAsync("/api/tickets")).StatusCode);

        var now = DateTime.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiTestHost.SigningKey));
        var expired = new JwtSecurityToken(
            "HelpDesk.Api.Tests", "HelpDesk.Tests",
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, host.EmployeeId.ToString()),
                new Claim(ClaimTypes.Role, nameof(UserRole.Employee))
            },
            now.AddHours(-2), now.AddHours(-1),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        using var expiredClient = host.CreateClient();
        expiredClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(expired));
        Assert.Equal(HttpStatusCode.Unauthorized, (await expiredClient.GetAsync("/api/tickets")).StatusCode);
    }

    [Fact]
    public async Task Employee_identity_ownership_comments_and_assignment_rules_are_enforced()
    {
        await using var host = await ApiTestHost.StartAsync();
        using var employee = await host.CreateAuthenticatedClientAsync("employee1@example.test");
        using var otherEmployee = await host.CreateAuthenticatedClientAsync("employee2@example.test");
        using var agent = await host.CreateAuthenticatedClientAsync("agent@example.test");

        var ownTicket = await host.CreateTicketAsync(employee, "My ticket", host.OtherEmployeeId);
        var otherTicket = await host.CreateTicketAsync(otherEmployee, "Another employee ticket", host.EmployeeId);
        Assert.Equal(host.EmployeeId, ownTicket.RequesterId);
        Assert.Equal(host.OtherEmployeeId, otherTicket.RequesterId);

        var list = await employee.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets", ApiTestHost.JsonOptions);
        Assert.Single(list!.Items);
        Assert.Equal(ownTicket.Id, list.Items[0].Id);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/tickets/{otherTicket.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await employee.PutAsJsonAsync($"/api/tickets/{ownTicket.Id}/assignment", new { assigneeId = host.AgentId })).StatusCode);

        var internalNote = await agent.PostAsJsonAsync($"/api/tickets/{ownTicket.Id}/comments", new
        {
            authorId = host.OtherEmployeeId,
            body = "Agent-only note",
            isInternal = true
        });
        Assert.Equal(HttpStatusCode.Created, internalNote.StatusCode);
        var visibleComments = await employee.GetFromJsonAsync<List<TicketCommentResponse>>($"/api/tickets/{ownTicket.Id}/comments", ApiTestHost.JsonOptions);
        Assert.Empty(visibleComments!);

        var publicComment = await employee.PostAsJsonAsync($"/api/tickets/{ownTicket.Id}/comments", new
        {
            authorId = host.OtherEmployeeId,
            body = "Public employee comment",
            isInternal = false
        });
        Assert.Equal(HttpStatusCode.Created, publicComment.StatusCode);
        var createdComment = await publicComment.Content.ReadFromJsonAsync<TicketCommentResponse>(ApiTestHost.JsonOptions);
        Assert.Equal(host.EmployeeId, createdComment!.AuthorId);
    }

    [Fact]
    public async Task Agent_sees_all_tickets_and_can_assign_and_read_or_add_internal_notes()
    {
        await using var host = await ApiTestHost.StartAsync();
        using var employee = await host.CreateAuthenticatedClientAsync("employee1@example.test");
        using var otherEmployee = await host.CreateAuthenticatedClientAsync("employee2@example.test");
        using var agent = await host.CreateAuthenticatedClientAsync("agent@example.test");

        var first = await host.CreateTicketAsync(employee, "First employee ticket");
        var second = await host.CreateTicketAsync(otherEmployee, "Second employee ticket");
        var allTickets = await agent.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets", ApiTestHost.JsonOptions);
        Assert.Equal(2, allTickets!.TotalCount);

        var assignment = await agent.PutAsJsonAsync($"/api/tickets/{first.Id}/assignment", new { assigneeId = host.AgentId });
        Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
        Assert.Equal(host.AgentId, (await assignment.Content.ReadFromJsonAsync<TicketResponse>(ApiTestHost.JsonOptions))!.AssigneeId);

        var addNote = await agent.PostAsJsonAsync($"/api/tickets/{second.Id}/comments", new
        {
            authorId = host.OtherEmployeeId,
            body = "Agent internal note",
            isInternal = true
        });
        Assert.Equal(HttpStatusCode.Created, addNote.StatusCode);
        var comments = await agent.GetFromJsonAsync<List<TicketCommentResponse>>($"/api/tickets/{second.Id}/comments", ApiTestHost.JsonOptions);
        Assert.Single(comments!);
        Assert.True(comments![0].IsInternal);
        Assert.Equal(host.AgentId, comments[0].AuthorId);
    }

    [Fact]
    public async Task Only_admin_can_create_users_and_password_is_usable_after_creation()
    {
        await using var host = await ApiTestHost.StartAsync();
        using var admin = await host.CreateAuthenticatedClientAsync("admin@example.test");
        using var employee = await host.CreateAuthenticatedClientAsync("employee1@example.test");
        using var agent = await host.CreateAuthenticatedClientAsync("agent@example.test");
        var request = new
        {
            fullName = "New Support Employee",
            email = "new.user@example.test",
            password = "New-user-password-123!",
            role = "Employee"
        };

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/users", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/users", request)).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain("PasswordHash", await created.Content.ReadAsStringAsync());

        using var newUser = host.CreateClient();
        var login = await newUser.PostAsJsonAsync("/api/auth/login", new
        {
            email = request.email,
            password = request.password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Lookup_data_is_readable_by_authenticated_users_and_writable_only_by_admins()
    {
        await using var host = await ApiTestHost.StartAsync();
        using var employee = await host.CreateAuthenticatedClientAsync("employee1@example.test");
        using var agent = await host.CreateAuthenticatedClientAsync("agent@example.test");
        using var admin = await host.CreateAuthenticatedClientAsync("admin@example.test");

        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/departments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/categories", new { name = "Employee Category" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/departments", new { name = "Agent Department" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/categories", new { name = "Admin Category" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/departments", new { name = "Admin Department" })).StatusCode);
    }
}
