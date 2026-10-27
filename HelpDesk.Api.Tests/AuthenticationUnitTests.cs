using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HelpDesk.Api.Tests;

public sealed class AuthenticationUnitTests
{
    [Fact]
    public void Password_hasher_stores_a_hash_and_verifies_correct_or_incorrect_passwords()
    {
        var hasher = new PasswordHasher<User>();
        var user = new User { Id = 42, Email = "hash-test@example.test" };
        const string password = "correct-password-123!";

        var hash = hasher.HashPassword(user, password);

        Assert.NotEqual(password, hash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, hash, password));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, hash, "incorrect-password"));
    }

    [Fact]
    public void Jwt_contains_user_claims_expires_and_is_signed_with_the_configured_key()
    {
        const string key = "unit-test-signing-key-with-more-than-thirty-two-bytes";
        const string issuer = "HelpDesk.UnitTests";
        const string audience = "HelpDesk.UnitTestClients";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = key,
            ["Jwt:Issuer"] = issuer,
            ["Jwt:Audience"] = audience
        }).Build();
        var service = new TokenService(configuration);
        var user = new User
        {
            Id = 73,
            FullName = "JWT Test Agent",
            Email = "jwt-agent@example.test",
            Role = UserRole.Agent
        };

        var (token, expiresAtUtc) = service.Create(user);
        var handler = new JwtSecurityTokenHandler();
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(token, validation, out var validatedToken);
        Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.Equal("73", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("JWT Test Agent", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("jwt-agent@example.test", principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal(nameof(UserRole.Agent), principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.InRange((expiresAtUtc - DateTime.UtcNow).TotalMinutes, 59, 60);

        var wrongKeyValidation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("different-unit-test-signing-key-that-is-long")),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token, wrongKeyValidation, out _));
    }
}
