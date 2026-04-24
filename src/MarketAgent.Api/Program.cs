using MarketAgent.Application.Abstractions;
using MarketAgent.Application.PriceIngestion;
using MarketAgent.Infrastructure.MarketData;
using MarketAgent.Infrastructure.Persistence;
using MarketAgent.Infrastructure.Watchlists;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IWatchlistProvider, StaticWatchlistProvider>();
builder.Services.AddHttpClient<IMarketDataProvider, EquityMarketDataProvider>();
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

app.Run();
