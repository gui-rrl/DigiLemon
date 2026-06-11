using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RankingDigi.Data;
using RankingDigi.Models;
using RankingDigi.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Serviços ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddDbContext<RankingContext>(options =>
    options
        .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Compressão de resposta
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        // Suppress WWW-Authenticate header on 401 to avoid triggering browser
        // Basic Auth dialogs when running behind ngrok with basic-auth enabled.
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"error\":\"N\\u00e3o autenticado.\"}");
            }
        };
    });

builder.Services.AddAuthorization();

// ── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RankingDigi API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Informe: Bearer {token}",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddScoped<TournamentService>();
builder.Services.AddScoped<SwissService>();

var app = builder.Build();

// ── Seed: cria admin padrão se não existir ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db       = scope.ServiceProvider.GetRequiredService<RankingContext>();
    var config   = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    db.Database.Migrate();   // aplica migrations pendentes automaticamente

    if (!db.AppUsers.Any(u => u.Role == "Admin"))
    {
        var adminUser = new AppUser
        {
            Username = config["AdminSeed:Username"] ?? "admin",
            Role     = "Admin",
        };
        var hasher = new PasswordHasher<AppUser>();
        adminUser.PasswordHash = hasher.HashPassword(adminUser, config["AdminSeed:Password"] ?? "admin123");
        db.AppUsers.Add(adminUser);
        db.SaveChanges();
    }
}

app.UseResponseCompression();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

// Arquivos estáticos (HTML/JS/CSS)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
