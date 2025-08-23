using LibraryApp.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger SIN RESTRICCIONES
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Library API - INSECURE VERSION",
        Version = "v1",
        Description = "WARNING: This is an intentionally insecure version for testing purposes. DO NOT USE IN PRODUCTION!"
    });
});

// MongoDB Services
builder.Services.AddSingleton<MongoDBService>();
builder.Services.AddSingleton<BookService>();
builder.Services.AddSingleton<LoanService>();
builder.Services.AddSingleton<UserService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("UnsafePolicy",
        builder =>
        {
            builder.AllowAnyOrigin()      // Cualquier origen
                   .AllowAnyMethod()       // Cualquier m�todo HTTP
                   .AllowAnyHeader()       // Cualquier header
                   .WithExposedHeaders("*"); // Expone todos los headers
        });
});

// SIN VALIDACI�N DE MODELOS GLOBAL
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true; // Desactiva validaci�n autom�tica
});

var app = builder.Build();

// Inicializar MongoDB
using (var scope = app.Services.CreateScope())
{
    var mongoService = scope.ServiceProvider.GetRequiredService<MongoDBService>();
    await mongoService.CreateIndexesAsync();
}

// Configure the HTTP request pipeline.

// Swagger disponible en TODOS los entornos (incluyendo producci�n)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Library API Insecure v1");
    c.RoutePrefix = string.Empty; // Swagger en la ra�z
});

// CORS completamente abierto
app.UseCors("UnsafePolicy");

app.MapControllers();

// Endpoint de depuraci�n PELIGROSO
app.MapGet("/debug/config", () =>
{
    // EXPONE TODA LA CONFIGURACI�N
    return Results.Ok(new
    {
        ConnectionString = builder.Configuration["MongoDB:ConnectionString"],
        DatabaseName = builder.Configuration["MongoDB:DatabaseName"],
        Environment = app.Environment.EnvironmentName,
        AllSettings = builder.Configuration.AsEnumerable()
    });
});

// Endpoint para ejecutar comandos MongoDB - EXTREMADAMENTE PELIGROSO
app.MapPost("/debug/mongo-query", async (MongoQueryRequest request, MongoDBService mongoService) =>
{
    try
    {
        // Permite ejecutar cualquier operaci�n en MongoDB
        var database = mongoService._database;
        var collection = database.GetCollection<dynamic>(request.Collection);

        // Sin validaci�n ni sanitizaci�n
        var result = await collection.Find(request.Filter).ToListAsync();
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = ex.Message, stackTrace = ex.StackTrace });
    }
});

// Health check que expone informaci�n del sistema
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        Status = "Running",
        MachineName = Environment.MachineName,
        OSVersion = Environment.OSVersion.ToString(),
        ProcessorCount = Environment.ProcessorCount,
        CurrentDirectory = Environment.CurrentDirectory,
        SystemDirectory = Environment.SystemDirectory,
        UserName = Environment.UserName,
        DotNetVersion = Environment.Version.ToString()
    });
});

Console.WriteLine("WARNING: Running INSECURE version of the API!");
Console.WriteLine("This version has NO security measures and should NEVER be used in production!");

app.Run();

public class MongoQueryRequest
{
    public string Collection { get; set; } = string.Empty;
    public string Filter { get; set; } = "{}";
}