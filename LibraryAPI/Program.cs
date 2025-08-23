using LibraryApp.Models;
using LibraryApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
            ClockSkew = TimeSpan.Zero
        };
    });

// Authorization con roles correctos
builder.Services.AddAuthorization(options =>
{
    // Política para administradores
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Administrador"));

    // Política para administradores y bibliotecarios
    options.AddPolicy("AdminOrLibrarian", policy =>
        policy.RequireRole("Administrador", "Bibliotecario"));

    // Política para usuarios registrados
    options.AddPolicy("RegisteredUser", policy =>
        policy.RequireRole("UsuarioRegistrado", "Bibliotecario", "Administrador"));

    // Política para todos los usuarios autenticados
    options.AddPolicy("AuthenticatedUsers", policy =>
        policy.RequireAuthenticatedUser());
});

// Rate Limiting Configuración Completa
builder.Services.AddRateLimiter(options =>
{
    // Política global por IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Política específica para autenticación
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Política para APIs de lectura
    options.AddPolicy("ReadPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Política para APIs de escritura (más restrictiva)
    options.AddPolicy("WritePolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 50,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Respuesta cuando se excede el límite
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.ContentType = "application/json";

        var response = new
        {
            error = "Rate limit exceeded",
            message = "Too many requests. Please try again later.",
            retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter.TotalSeconds : 60,
            timestamp = DateTime.UtcNow
        };

        await context.HttpContext.Response
    .WriteAsync(System.Text.Json.JsonSerializer.Serialize(response), token);

    };
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Library Management API",
        Version = "v1",
        Description = "API segura para gestión de biblioteca digital"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese 'Bearer' seguido de un espacio y el JWT token"
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

// CORS - Política segura de producción
var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ??
    new[] { "http://localhost:4200", "https://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("SecurePolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Content-Type", "Authorization", "x-api-type", "x-api-name")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(5));
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.


// Security Headers
// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.XXSSProtection = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin"; 
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'";

    if (app.Environment.IsProduction())
    {
        context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
    }

    await next();
});



// Exception Handling
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = "Error interno del servidor",
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
        c.RoutePrefix = string.Empty;
    });
}

// HTTPS Redirection
app.UseHttpsRedirection();

// CORS
app.UseCors("SecurePolicy");

// Rate Limiting
app.UseRateLimiter();

// Request Logging simplificado para producción
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await next();

        stopwatch.Stop();
        logger.LogInformation(
            "HTTP {Method} {Path} - {StatusCode} - {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    });
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check
app.MapGet("/health", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Environment = app.Environment.EnvironmentName
}).AllowAnonymous().RequireRateLimiting("ReadPolicy");

// Inicialización de MongoDB
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
                Role = "Administrador"
            };

            await userService.CreateUserAsync(defaultAdmin, "Admin123!");
            logger.LogInformation("Usuario administrador por defecto creado");
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error durante la inicialización");
        throw;
    }
}

await app.RunAsync();
