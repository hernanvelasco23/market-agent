using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Briefing;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Application.PriceIngestion;
using MarketAgent.Application.Signals;
using MarketAgent.Infrastructure.AI;
using MarketAgent.Infrastructure.Indicators;
using MarketAgent.Infrastructure.MarketData;
using MarketAgent.Infrastructure.Persistence;
using MarketAgent.Infrastructure.Watchlists;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "MarketAgentFrontend";

builder.Services.AddSingleton<IWatchlistProvider, StaticWatchlistProvider>();
builder.Services.AddHttpClient<EquityMarketDataProvider>();
builder.Services.AddTransient<IMarketDataProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<EquityMarketDataProvider>());
builder.Services.AddHttpClient<CryptoMarketDataProvider>();
builder.Services.AddTransient<IMarketDataProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<CryptoMarketDataProvider>());
builder.Services.AddHttpClient<MepMarketDataProvider>();
builder.Services.AddTransient<IMarketDataProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<MepMarketDataProvider>());
builder.Services.AddHttpClient<HistoricalMarketDataProvider>();
builder.Services.AddTransient<IHistoricalMarketDataProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<HistoricalMarketDataProvider>());
builder.Services.AddHttpClient(nameof(SemanticKernelMarketBriefingGenerator));
builder.Services.AddSingleton<IMarketDataProviderResolver, MarketDataProviderResolver>();
builder.Services.AddSingleton<IMarketSnapshotRepository, InMemoryMarketSnapshotRepository>();
builder.Services.AddSingleton<IHistoricalCandleRepository, InMemoryHistoricalCandleRepository>();
builder.Services.AddScoped<IPriceIngestionService, PriceIngestionService>();
builder.Services.AddScoped<IHistoricalMarketDataService, HistoricalMarketDataService>();
builder.Services.AddScoped<IMarketBriefingService, MarketBriefingService>();
builder.Services.AddScoped<IMarketBriefingGenerator, SemanticKernelMarketBriefingGenerator>();
builder.Services.AddScoped<ITechnicalIndicatorService, TechnicalIndicatorService>();
builder.Services.AddScoped<IMarketSignalAnalyzer, TechnicalMarketSignalAnalyzer>();
builder.Services.AddScoped<IMarketSignalService, MarketSignalService>();
builder.Services.AddSingleton(_ =>
    builder.Configuration.GetSection(RiskPositionOptions.SectionName).Get<RiskPositionOptions>() ?? new RiskPositionOptions());
builder.Services.Configure<HistoricalMarketDataOptions>(
    builder.Configuration.GetSection(HistoricalMarketDataOptions.SectionName));
builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        FrontendCorsPolicy,
        policy => policy
            .WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(FrontendCorsPolicy);
}

app.MapPost(
    "/api/ingestion/run",
    async (IPriceIngestionService priceIngestionService, CancellationToken cancellationToken) =>
    {
        var result = await priceIngestionService.ExecuteAsync(cancellationToken);
        return Results.Ok(result);
    });

app.MapGet(
    "/api/ingestion/snapshots",
    async (IMarketSnapshotRepository marketSnapshotRepository, CancellationToken cancellationToken) =>
    {
        var snapshots = await marketSnapshotRepository.GetAllAsync(cancellationToken);
        return Results.Ok(snapshots);
    });

app.MapPost(
    "/api/briefing/run",
    async (IMarketBriefingService marketBriefingService, CancellationToken cancellationToken) =>
    {
        var result = await marketBriefingService.GenerateAsync(cancellationToken);
        return Results.Ok(result);
    });

app.MapPost(
    "/api/signals/run",
    async (IMarketSignalService marketSignalService, CancellationToken cancellationToken) =>
    {
        var result = await marketSignalService.GenerateAsync(cancellationToken);
        return Results.Ok(result);
    });

app.MapGet(
    "/api/historical/candles",
    async (IHistoricalMarketDataService historicalMarketDataService, int? days, CancellationToken cancellationToken) =>
    {
        var result = await historicalMarketDataService.GetWatchlistCandlesAsync(
            days ?? HistoricalMarketDataService.DefaultDays,
            cancellationToken);

        return Results.Ok(result);
    });

app.Run();
