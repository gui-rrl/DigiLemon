using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.OpenApi.Models;
using RankingDigi.Data;
using RankingDigi.Services;
using RankingDigi.Models;
using System.IO;
using static System.Net.WebRequestMethods;

var builder = WebApplication.CreateBuilder(args);

// Adicionar servi�os ao container.
builder.Services.AddControllers(); // Adiciona suporte para controladores da API
builder.Services.AddDbContext<RankingContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Compress�o de resposta (gzip + brotli) para reduzir tr�fego
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// Configurar o Swagger
builder.Services.AddEndpointsApiExplorer();

// Configura o Swagger para incluir a defini��o de seguran�a da chave de API
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RankingDigi API", Version = "v1" });
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-API-KEY",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey",
        In = ParameterLocation.Header,
        Description = "Chave de API necess�ria para acessar a API."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddScoped<TournamentService>();

var app = builder.Build();

app.UseResponseCompression();

// Rotas p�blicas (sem API key) - acessadas via link de convite por convidados
var publicRoutes = new[]
{
    "/api/tournament/invite/",   // GET e POST /invite/{code} e /invite/{code}/join
};

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    // Libera acesso para rotas que n�o s�o da API (Swagger, arquivos est�ticos, etc.)
    if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Libera rotas p�blicas (links de convite)
    if (publicRoutes.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
    {
        await next();
        return;
    }

    // Verifica se a chave de API est� presente no cabe�alho da requisi��o
    if (!context.Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || apiKey != "rankD")
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Chave de API inv�lida.");
        return;
    }

    await next();
});


// Configurar o pipeline de requisi��es HTTP.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Adicionar middlewares para servir arquivos est�ticos e definir a p�gina padr�o
app.UseDefaultFiles(); // Define o index.html como p�gina padr�o
app.UseStaticFiles();  // Habilita o servidor de arquivos est�ticos

app.UseRouting();

app.UseAuthorization();
app.UseCors("AllowAll");

// Mapeia os controladores da API
app.MapControllers();

app.Run();


////////////////////////////////// Exemplo de requisi��o HTTP com chave de API //////////////////////////////////
/*                                                                                                              /
GET / api / players HTTP / 1.1                                                                                  /
Host: localhost: 44393                                                                                          /
X - API - KEY: rankD                                                                                            /
*/                                                                                                              
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////