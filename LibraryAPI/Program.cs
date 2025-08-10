using LibraryApp.Models;
using LibraryApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Para manejar enums como strings en JSON
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Configuración para nombres de propiedades en camelCase
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// MongoDB Services
builder.Services.AddSingleton<MongoDBService>();

// Custom Services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<Logservice>();
builder.Services.AddScoped<BookService>();
builder.Services.AddScoped<LoanService>();

// HTTP Context Accessor para logging
builder.Services.AddHttpContextAccessor();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings.GetValue<string>("Key");
var jwtIssuer = jwtSettings.GetValue<string>("Issuer");
var jwtAudience = jwtSettings.GetValue<string>("Audience");

if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key no configurada en appsettings.json");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero // Elimina el tiempo de gracia por defecto
        };

        // Configuración adicional para desarrollo/debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError("Autenticación fallida: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var username = context.Principal?.Identity?.Name ?? "Unknown";
                logger.LogInformation("Token validado para usuario: {Username}", username);
                return Task.CompletedTask;
            }
        };
    });

// Authorization
builder.Services.AddAuthorization(options =>
{
    // Política para administradores
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    // Política para administradores y operadores
    options.AddPolicy("AdminOrOperator", policy =>
        policy.RequireRole("Admin", "Operator"));

    // Política para administradores y contadores
    options.AddPolicy("AdminOrAccountant", policy =>
        policy.RequireRole("Admin", "Accountant"));

    // Política para todos los usuarios autenticados
    options.AddPolicy("AuthenticatedUsers", policy =>
        policy.RequireAuthenticatedUser());
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Library Management API",
        Version = "v1",
        Description = "API segura para gestión de biblioteca digital con autenticación JWT y autorización basada en roles",
        Contact = new OpenApiContact
        {
            Name = "Soporte Técnico",
            Email = "soporte@biblioteca.com"
        }
    });

    // Configuración de JWT en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese 'Bearer' seguido de un espacio y el JWT token. Ejemplo: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS - Política segura
builder.Services.AddCors(options =>
{
    options.AddPolicy("SecurePolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "https://localhost:4200") // Solo Angular dev
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Content-Type", "Authorization")
            .AllowCredentials();
    });
});

// Rate Limiting básico
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Security Headers básicos
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

    await next();
});

// Exception Handling simple
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var errorId = Guid.NewGuid().ToString();
        var response = new
        {
            error = "Error interno del servidor",
            errorId = errorId,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Library API V1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz
    });
}

// HTTPS Redirection
app.UseHttpsRedirection();

// CORS
app.UseCors("SecurePolicy");

// Rate Limiting middleware
app.Use(async (context, next) =>
{
    var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var key = $"rate_limit_{clientIp}";

    if (!cache.TryGetValue(key, out int requestCount))
    {
        requestCount = 0;
    }

    requestCount++;
    cache.Set(key, requestCount, TimeSpan.FromMinutes(1));

    // Límite: 100 requests por minuto por IP
    if (requestCount > 100)
    {
        context.Response.StatusCode = 429;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Rate limit exceeded. Try again later." }));
        return;
    }

    await next();
});

// Logging de requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    await next();

    stopwatch.Stop();
    logger.LogInformation(
        "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds}ms",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        stopwatch.ElapsedMilliseconds);
});

// Middleware de autenticación y autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Información básica de la API
app.MapGet("/info", () => new
{
    Application = "Library Management API",
    Version = "1.0.0",
    Environment = app.Environment.EnvironmentName,
    Timestamp = DateTime.UtcNow,
    Features = new[]
    {
        "JWT Authentication",
        "Role-based Authorization",
        "MongoDB Integration",
        "Comprehensive Logging",
        "Rate Limiting",
        "Security Headers"
    }
}).AllowAnonymous();

// Health check
app.MapGet("/health", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}).AllowAnonymous();

// Inicialización de MongoDB y datos semilla
using (var scope = app.Services.CreateScope())
{
    try
    {
        var mongoService = scope.ServiceProvider.GetRequiredService<MongoDBService>();
        await mongoService.CreateIndexesAsync();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("MongoDB indices creados exitosamente");

        // Crear usuario administrador por defecto si no existe
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        var adminUser = await userService.GetUserByUserNameAsync("admin");

        if (adminUser == null)
        {
            var defaultAdmin = new User
            {
                Username = "admin",
                Email = "admin@biblioteca.com",
                Role = "Admin"
            };

            await userService.CreateUserAsync(defaultAdmin, "Admin123!");
            logger.LogInformation("Usuario administrador por defecto creado: admin/Admin123!");
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error durante la inicialización de la aplicación");
    }
}

app.Run();