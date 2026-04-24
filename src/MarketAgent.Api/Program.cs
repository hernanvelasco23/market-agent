using MarketAgent.Application.Abstractions;
using MarketAgent.Application.PriceIngestion;
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
builder.Services.AddSingleton<IMarketDataProviderResolver, MarketDataProviderResolver>();
builder.Services.AddSingleton<IMarketSnapshotRepository, InMemoryMarketSnapshotRepository>();
builder.Services.AddScoped<IPriceIngestionService, PriceIngestionService>();

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

app.Run();
