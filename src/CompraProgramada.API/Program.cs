using CompraProgramada.Application.Interfaces;
using CompraProgramada.Application.Services;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Cotacoes;
using CompraProgramada.Infrastructure.Data;
using CompraProgramada.Infrastructure.Kafka;
using CompraProgramada.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=mysql;Port=3306;Database=compra_programada;User=root;Password=root123;";
var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mysqlOptions =>
    {
        mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    })
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning)));

// Repositories
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<ICestaRepository, CestaRepository>();
builder.Services.AddScoped<ICustodiaFilhoteRepository, CustodiaFilhoteRepository>();
builder.Services.AddScoped<ICustodiaMasterRepository, CustodiaMasterRepository>();
builder.Services.AddScoped<IOrdemCompraRepository, OrdemCompraRepository>();
builder.Services.AddScoped<IDistribuicaoRepository, DistribuicaoRepository>();
builder.Services.AddScoped<IExecucaoCompraRepository, ExecucaoCompraRepository>();
builder.Services.AddScoped<IVendaRebalanceamentoRepository, VendaRebalanceamentoRepository>();

// Infrastructure services
builder.Services.AddScoped<ICotahistService, CotahistService>();

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "kafka:9092";
builder.Services.AddSingleton<IKafkaProducer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KafkaProducer>>();
    return new KafkaProducer(kafkaBootstrapServers, logger);
});

// Application services
var pastaCotacoes = builder.Configuration["CotacoesPath"] ?? "/app/cotacoes";

builder.Services.AddScoped<ClienteService>(sp =>
    new ClienteService(
        sp.GetRequiredService<IClienteRepository>(),
        sp.GetRequiredService<ICustodiaFilhoteRepository>(),
        sp.GetRequiredService<ICotahistService>(),
        pastaCotacoes));

builder.Services.AddScoped<CestaService>(sp =>
    new CestaService(
        sp.GetRequiredService<ICestaRepository>(),
        sp.GetRequiredService<IClienteRepository>(),
        sp.GetRequiredService<ICotahistService>(),
        pastaCotacoes));

builder.Services.AddScoped<MotorCompraService>(sp =>
    new MotorCompraService(
        sp.GetRequiredService<IClienteRepository>(),
        sp.GetRequiredService<ICestaRepository>(),
        sp.GetRequiredService<ICustodiaFilhoteRepository>(),
        sp.GetRequiredService<ICustodiaMasterRepository>(),
        sp.GetRequiredService<IOrdemCompraRepository>(),
        sp.GetRequiredService<IDistribuicaoRepository>(),
        sp.GetRequiredService<IExecucaoCompraRepository>(),
        sp.GetRequiredService<ICotahistService>(),
        sp.GetRequiredService<IKafkaProducer>(),
        pastaCotacoes));

builder.Services.AddScoped<RebalanceamentoService>(sp =>
    new RebalanceamentoService(
        sp.GetRequiredService<IClienteRepository>(),
        sp.GetRequiredService<ICestaRepository>(),
        sp.GetRequiredService<ICustodiaFilhoteRepository>(),
        sp.GetRequiredService<IVendaRebalanceamentoRepository>(),
        sp.GetRequiredService<ICotahistService>(),
        sp.GetRequiredService<IKafkaProducer>(),
        pastaCotacoes));

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Compra Programada API - Itau Corretora",
        Version = "v1",
        Description = "Sistema de Compra Programada de Acoes - Itau Corretora. " +
            "API REST para gerenciamento de compras programadas de acoes, " +
            "incluindo adesao de clientes, gestao de cestas recomendadas (Top Five), " +
            "motor de compra consolidada e distribuicao de ativos."
    });
});

var app = builder.Build();

// Custom exception-handling middleware (avoids fail-level logging for business exceptions)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Request-Id"] = Guid.NewGuid().ToString();
    try
    {
        await next();
    }
    catch (BusinessException businessEx)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { erro = businessEx.Message, codigo = businessEx.Codigo });
    }
    catch (NotFoundException notFoundEx)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { erro = notFoundEx.Message, codigo = notFoundEx.Codigo });
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erro nao tratado na requisicao {Path}", context.Request.Path);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { erro = "Erro interno do servidor.", codigo = "ERRO_INTERNO" });
    }
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Compra Programada API v1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();

// Auto-create database schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var maxRetries = 30;

    // Step 1: Wait for database connectivity
    for (var i = 1; i <= maxRetries; i++)
    {
        try
        {
            db.Database.CanConnect();
            logger.LogInformation("Database connection established (attempt {Attempt}/{Max}).", i, maxRetries);
            break;
        }
        catch
        {
            if (i == maxRetries)
            {
                logger.LogError("Failed to connect to database after {Max} attempts.", maxRetries);
                throw;
            }
            Thread.Sleep(2000);
        }
    }

    // Step 2: Create schema
    db.Database.EnsureCreated();
    logger.LogInformation("Database schema verified successfully.");
}

app.Run();

public partial class Program { }
