using System.Text.Json.Serialization;
using System.Text;
using System.Security.Cryptography;
using HelpDesk.Api.Data;
using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var signingKey = builder.Configuration["Jwt:SigningKey"];
if (builder.Environment.IsEnvironment("Testing")) signingKey ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("Configure Jwt:SigningKey (or Jwt__SigningKey) with at least 32 UTF-8 bytes via user-secrets or environment variables.");
builder.Configuration["Jwt:SigningKey"] = signingKey;
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "HelpDesk.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "HelpDesk.Clients";

var connectionString = builder.Configuration.GetConnectionString("HelpDesk");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Configure ConnectionStrings:HelpDesk (or ConnectionStrings__HelpDesk) before starting the API.");
}

builder.Services.AddDbContext<HelpDeskDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwtIssuer,
        ValidateAudience = true, ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
});
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Paste the access token returned by POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var bootstrapEmail = app.Configuration["BootstrapAdmin:Email"];
    var bootstrapName = app.Configuration["BootstrapAdmin:FullName"];
    var bootstrapPassword = app.Configuration["BootstrapAdmin:Password"];
    if (new[] { bootstrapEmail, bootstrapName, bootstrapPassword }.Any(value => !string.IsNullOrWhiteSpace(value)))
    {
        if (string.IsNullOrWhiteSpace(bootstrapEmail) || string.IsNullOrWhiteSpace(bootstrapName) ||
            string.IsNullOrWhiteSpace(bootstrapPassword) || bootstrapPassword.Length < 12)
            throw new InvalidOperationException("Development admin bootstrap requires BootstrapAdmin:Email, FullName, and a Password of at least 12 characters.");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
        var normalizedEmail = bootstrapEmail.Trim().ToLowerInvariant();
        var existingAdmin = await db.Users.SingleOrDefaultAsync(user => user.Email == normalizedEmail);
        if (existingAdmin is null)
        {
            var admin = new User { FullName = bootstrapName.Trim(), Email = normalizedEmail, Role = UserRole.Admin };
            admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(admin, bootstrapPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
        else if (existingAdmin.Role != UserRole.Admin)
        {
            throw new InvalidOperationException("The configured bootstrap email already belongs to a non-admin user.");
        }
    }
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
