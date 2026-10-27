using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace HelpDesk.Api.Tests;

internal sealed class ApiTestHost : IAsyncDisposable
{
    internal const string SigningKey = "integration-test-key-that-is-long-enough-for-hmac-sha256";
    internal const string Password = "Testing-password-123!";
    internal static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly PostgreSqlContainer postgres;
    private readonly WebApplicationFactory<Program> factory;
    private readonly Dictionary<string, string?> previousEnvironment;

    private ApiTestHost(PostgreSqlContainer postgres, WebApplicationFactory<Program> factory, Dictionary<string, string?> previousEnvironment)
    {
        this.postgres = postgres;
        this.factory = factory;
        this.previousEnvironment = previousEnvironment;
    }

    internal int EmployeeId { get; private set; }
    internal int OtherEmployeeId { get; private set; }
    internal int AgentId { get; private set; }
    internal int AdminId { get; private set; }

    internal static async Task<ApiTestHost> StartAsync()
    {
        var postgres = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        try
        {
            await postgres.StartAsync();
        }
        catch (DockerUnavailableException exception)
        {
            await postgres.DisposeAsync();
            throw Xunit.Sdk.SkipException.ForSkip($"Authentication integration tests need a working Docker daemon. {exception.Message}");
        }

        var previousEnvironment = new Dictionary<string, string?>
        {
            ["ConnectionStrings__HelpDesk"] = Environment.GetEnvironmentVariable("ConnectionStrings__HelpDesk"),
            ["Jwt__SigningKey"] = Environment.GetEnvironmentVariable("Jwt__SigningKey"),
            ["Jwt__Issuer"] = Environment.GetEnvironmentVariable("Jwt__Issuer"),
            ["Jwt__Audience"] = Environment.GetEnvironmentVariable("Jwt__Audience")
        };
        Environment.SetEnvironmentVariable("ConnectionStrings__HelpDesk", postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "HelpDesk.Api.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "HelpDesk.Tests");
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web => web.UseEnvironment("Testing"));

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
            await db.Database.EnsureCreatedAsync();

            var users = new[]
            {
                CreateUser("Employee One", "employee1@example.test", UserRole.Employee),
                CreateUser("Employee Two", "employee2@example.test", UserRole.Employee),
                CreateUser("Support Agent", "agent@example.test", UserRole.Agent),
                CreateUser("Help Desk Admin", "admin@example.test", UserRole.Admin)
            };
            var hasher = new PasswordHasher<User>();
            foreach (var user in users) user.PasswordHash = hasher.HashPassword(user, Password);
            db.Users.AddRange(users);
            await db.SaveChangesAsync();

            return new ApiTestHost(postgres, factory, previousEnvironment)
            {
                EmployeeId = users[0].Id,
                OtherEmployeeId = users[1].Id,
                AgentId = users[2].Id,
                AdminId = users[3].Id
            };
        }
        catch
        {
            await factory.DisposeAsync();
            await postgres.DisposeAsync();
            RestoreEnvironment(previousEnvironment);
            throw;
        }
    }

    internal HttpClient CreateClient() => factory.CreateClient();

    internal async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Login returned an empty response.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    internal async Task<TicketResponse> CreateTicketAsync(HttpClient client, string title, int? spoofedRequesterId = null)
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title,
            description = $"Description for {title}",
            priority = "Medium",
            requesterId = spoofedRequesterId ?? OtherEmployeeId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketResponse>(JsonOptions))!;
    }

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
        await postgres.DisposeAsync();
        RestoreEnvironment(previousEnvironment);
    }

    private static User CreateUser(string name, string email, UserRole role) =>
        new() { FullName = name, Email = email, Role = role };

    private static void RestoreEnvironment(Dictionary<string, string?> values)
    {
        foreach (var (key, value) in values) Environment.SetEnvironmentVariable(key, value);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
