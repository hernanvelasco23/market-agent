using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Briefing;
using MarketAgent.Application.PriceIngestion;
using MarketAgent.Infrastructure.AI;
using MarketAgent.Infrastructure.MarketData;
using MarketAgent.Infrastructure.Persistence;
using MarketAgent.Infrastructure.Watchlists;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddHttpClient(nameof(SemanticKernelMarketBriefingGenerator));
builder.Services.AddSingleton<IMarketDataProviderResolver, MarketDataProviderResolver>();
builder.Services.AddSingleton<IMarketSnapshotRepository, InMemoryMarketSnapshotRepository>();
builder.Services.AddScoped<IPriceIngestionService, PriceIngestionService>();
builder.Services.AddScoped<IMarketBriefingService, MarketBriefingService>();
builder.Services.AddScoped<IMarketBriefingGenerator, SemanticKernelMarketBriefingGenerator>();
builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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

app.Run();
