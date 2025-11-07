using KetenErp.Infrastructure.Data;
using KetenErp.Infrastructure.Identity;
using KetenErp.Infrastructure.Repositories;
using KetenErp.Core.Repositories;
using KetenErp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using KetenErp.Api;

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
            // For both development and production allow the frontend origin and credentials
            // so the refresh-token cookie (HttpOnly) can be sent by the browser.
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Configure EF Core to use SQLite (file-based DB)
// Place the DB file inside the runtime output folder (AppContext.BaseDirectory) so the app
// always uses the DB from the `bin/...` folder during execution.
var dbFilePath = Path.Combine(AppContext.BaseDirectory, "ketenerp.db");
var sqliteDefault = $"Data Source={Path.GetFullPath(dbFilePath)}";
// Log the DB file path at startup to help debugging where the file is created
Console.WriteLine($"Using SQLite DB at runtime path: {Path.GetFullPath(dbFilePath)}");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? sqliteDefault;
builder.Services.AddDbContext<KetenErpDbContext>(options => options.UseSqlite(connectionString));

// DI
builder.Services.AddScoped<IProductRepository, ProductRepository>();
// register spare part repository
builder.Services.AddScoped<KetenErp.Core.Repositories.ISparePartRepository, KetenErp.Infrastructure.Repositories.SparePartRepository>();

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
builder.Services.AddScoped<KetenErp.Core.Service.IServiceTemplateRepository, KetenErp.Infrastructure.Repositories.ServiceTemplateRepository>();
// register email service
builder.Services.AddScoped<EmailService>();

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

// Serve static files (uploads etc.) from project-level wwwroot (if exists)
app.UseStaticFiles();
// Additionally serve static files from the runtime output wwwroot (AppContext.BaseDirectory/wwwroot)
try
{
    var runtimeWww = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    if (Directory.Exists(runtimeWww))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(runtimeWww),
            RequestPath = ""
        });
    }
}
catch (Exception ex)
{
    Console.WriteLine("Could not configure runtime static file provider: " + ex.Message);
}

app.MapControllers();

// Ensure DB schema, seed roles/users and perform any ALTER TABLE / compatibility steps
await EnsureDb.EnsureAsync(app.Services, app.Environment);

app.Run();
