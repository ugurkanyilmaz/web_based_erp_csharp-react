using KetenErp.Infrastructure.Data;
using KetenErp.Infrastructure.Identity;
using KetenErp.Infrastructure.Repositories;
using KetenErp.Core.Repositories;
using KetenErp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS - allow the frontend dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Configure EF Core to use SQLite (file-based DB)
var defaultDbPath = Path.Combine(AppContext.BaseDirectory, "..", "ketenerp.db");
var sqliteDefault = $"Data Source={Path.GetFullPath(defaultDbPath)}";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? sqliteDefault;
builder.Services.AddDbContext<KetenErpDbContext>(options => options.UseSqlite(connectionString));

// DI
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<KetenErpDbContext>()
    .AddDefaultTokenProviders();

// JWT
builder.Services.AddScoped<TokenService>();
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? "please-change-this-secret";
var jwtIssuer = jwtSection["Issuer"] ?? "KetenErp";
var jwtAudience = jwtSection["Audience"] ?? "KetenErpUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Configure controllers and JSON options to avoid object cycle serialization errors
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Prevent possible reference cycles between entities when serializing EF navigation properties
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // Optional: don't emit null properties
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
// register service repositories
builder.Services.AddScoped<KetenErp.Core.Service.IServiceRecordRepository, KetenErp.Infrastructure.Repositories.ServiceRecordRepository>();
builder.Services.AddScoped<KetenErp.Core.Service.IServiceOperationRepository, KetenErp.Infrastructure.Repositories.ServiceOperationRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
    // Do not force HTTPS redirection in development to avoid https-port redirect warnings
}
else
{
    app.UseHttpsRedirection();
}

// Enable CORS early so preflight requests are handled
app.UseCors("AllowFrontend");

// Simple request logging to help debug incoming requests (method/path and Authorization header presence)
app.Use(async (context, next) =>
{
    try
    {
        var hasAuth = context.Request.Headers.ContainsKey("Authorization");
        Console.WriteLine($"[{DateTime.Now:O}] Incoming: {context.Request.Method} {context.Request.Path} AuthHeader={(hasAuth ? "yes" : "no")}");
    }
    catch { }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed roles and example users
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var db = services.GetRequiredService<KetenErpDbContext>();
    // Ensure the SQLite database and AspNet* tables are created when not using migrations
    db.Database.EnsureCreated();

    string[] roles = new[] { "admin", "servis", "muhasebe", "user" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    async Task EnsureUser(string userName, string email, string pwd, string role, string? fullName = null)
    {
        var existing = await userManager.FindByNameAsync(userName);
        if (existing == null)
        {
            var u = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FullName = fullName,
                LockoutEnabled = false
            };
            var res = await userManager.CreateAsync(u, pwd);
            if (res.Succeeded)
            {
                // Make sure email is confirmed and user is added to role
                await userManager.AddToRoleAsync(u, role);
                u.EmailConfirmed = true;
                u.LockoutEnabled = false;
                await userManager.UpdateAsync(u);
            }
        }
    }

    await EnsureUser("admin", "admin@keten.local", "admin123", "admin", "System Administrator");
    await EnsureUser("servis", "servis@keten.local", "Servis123!", "servis", "Servis User");
    await EnsureUser("muhasebe", "muhasebe@keten.local", "Muhasebe123!", "muhasebe", "Muhasebe User");
    await EnsureUser("user", "user@keten.local", "User123!", "user", "Normal User");
}

app.Run();
