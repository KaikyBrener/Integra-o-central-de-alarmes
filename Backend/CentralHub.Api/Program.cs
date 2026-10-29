using CentralHub.Api.Data;
using CentralHub.Api.Middleware;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CentralHub API",
        Version = "v1",
        Description = "API para cadastro de Prédios, Centrais de alarme e envio de comandos de PGM (Pulso, Ligar, Desligar)."
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyMiddleware.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "Chave de API configurada em ApiKey (variável de ambiente ou User Secrets)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
            Array.Empty<string>()
        }
    });

    var xmlFile =$"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Chaves usadas para criptografar as senhas das centrais. Ficam em disco e NÃO devem ser
// versionadas nem perdidas: sem elas, as senhas já gravadas não podem ser descriptografadas.
var chavesPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
builder.Services.AddDataProtection()
    .SetApplicationName("CentralHub")
    .PersistKeysToFileSystem(new DirectoryInfo(chavesPath));

builder.Services.AddSingleton<IAdapterResolver, AdapterResolver>();
builder.Services.AddScoped<ConnectionService>();
builder.Services.AddScoped<BuildingService>();
builder.Services.AddScoped<CentralService>();
builder.Services.AddScoped<OperationService>();
builder.Services.AddHostedService<KeepAliveService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Falha logo na inicialização em vez de subir uma API que recusaria todas as requisições.
if (string.IsNullOrWhiteSpace(app.Configuration["ApiKey"]))
{
    throw new InvalidOperationException(
        "Chave de API não configurada. Defina a variável de ambiente ApiKey " +
        "ou use 'dotnet user-secrets set ApiKey <valor>' na pasta Backend/CentralHub.Api.");
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    DatabaseUpgrade.Aplicar(context);
}

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CentralHub API v1");
    });
}

// A chave de API vem depois do CORS: o preflight (OPTIONS) não envia o header e é respondido pelo CORS.
app.UseCors("AllowFrontend");
app.UseApiKey();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
