using MarketAgent.Api.Services;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Alerts;
using MarketAgent.Application.Briefing;
using MarketAgent.Application.Historical;
using MarketAgent.Application.Models;
using MarketAgent.Application.PriceIngestion;
using MarketAgent.Application.Signals;
using MarketAgent.Application.SystemCycle;
using MarketAgent.Infrastructure.AI;
using MarketAgent.Infrastructure.Email;
using MarketAgent.Infrastructure.Indicators;
using MarketAgent.Infrastructure.MarketData;
using MarketAgent.Infrastructure.Persistence;
using MarketAgent.Infrastructure.Watchlists;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "MarketAgentFrontend";
const string SqlServerConnectionStringName = "DefaultConnection";
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (builder.Environment.IsDevelopment() && allowedCorsOrigins.Length == 0)
{
    allowedCorsOrigins = ["http://localhost:5173", "https://localhost:5173"];
}

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
builder.Services.AddSingleton<IHistoricalCandleRepository, InMemoryHistoricalCandleRepository>();
var sqlServerConnectionString = builder.Configuration.GetConnectionString(SqlServerConnectionStringName);
if (string.IsNullOrWhiteSpace(sqlServerConnectionString))
{
    builder.Services.AddSingleton<IMarketSnapshotRepository, InMemoryMarketSnapshotRepository>();
    builder.Services.AddScoped<ISignalSnapshotHistoryRepository, NoOpSignalSnapshotHistoryRepository>();
    builder.Services.AddScoped<ISignalOutcomeRepository, NoOpSignalOutcomeRepository>();
    builder.Services.AddScoped<IAlertEventRepository, NoOpAlertEventRepository>();
}
else
{
    builder.Services.AddDbContext<MarketAgentDbContext>(options =>
        options.UseSqlServer(sqlServerConnectionString));
    builder.Services.AddScoped<IMarketSnapshotRepository, EfMarketSnapshotRepository>();
    builder.Services.AddScoped<ISignalSnapshotHistoryRepository, EfSignalSnapshotHistoryRepository>();
    builder.Services.AddScoped<ISignalOutcomeRepository, EfSignalOutcomeRepository>();
    builder.Services.AddScoped<IAlertEventRepository, EfAlertEventRepository>();
}
builder.Services.AddScoped<IPriceIngestionService, PriceIngestionService>();
builder.Services.AddScoped<IHistoricalMarketDataService, HistoricalMarketDataService>();
builder.Services.AddScoped<IMarketBriefingService, MarketBriefingService>();
builder.Services.AddScoped<IMarketBriefingGenerator, SemanticKernelMarketBriefingGenerator>();
builder.Services.AddScoped<ITechnicalIndicatorService, TechnicalIndicatorService>();
builder.Services.AddScoped<IMarketSignalAnalyzer, TechnicalMarketSignalAnalyzer>();
builder.Services.AddScoped<IMarketSignalService, MarketSignalService>();
builder.Services.AddScoped<ISignalOutcomeService, SignalOutcomeService>();
builder.Services.AddScoped<ISignalPerformancePreviewService, SignalPerformancePreviewService>();
builder.Services.AddScoped<IAlertEvaluationService, AlertEvaluationService>();
builder.Services.AddScoped<IScoreAttributionService, ScoreAttributionService>();
builder.Services.AddScoped<IManualSystemCycleService, ManualSystemCycleService>();
builder.Services.AddScoped<IEmailAlertDeliveryService, EmailAlertDeliveryService>();
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
builder.Services.AddSingleton<IMarketHoursService, UsEquityMarketHoursService>();
builder.Services.AddSingleton(_ =>
    builder.Configuration.GetSection(RiskPositionOptions.SectionName).Get<RiskPositionOptions>() ?? new RiskPositionOptions());
builder.Services.AddSingleton(_ =>
    builder.Configuration.GetSection(EmailDeliveryOptions.SectionName).Get<EmailDeliveryOptions>() ?? new EmailDeliveryOptions());
builder.Services.AddSingleton(_ =>
    builder.Configuration.GetSection(MarketAgentSchedulerOptions.SectionName).Get<MarketAgentSchedulerOptions>() ?? new MarketAgentSchedulerOptions());
builder.Services.AddSingleton<IMarketAgentCycleSchedulerRunner, MarketAgentCycleSchedulerRunner>();
builder.Services.AddHostedService<MarketAgentCycleSchedulerService>();
builder.Services.Configure<HistoricalMarketDataOptions>(
    builder.Configuration.GetSection(HistoricalMarketDataOptions.SectionName));
builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        FrontendCorsPolicy,
        policy =>
        {
            if (allowedCorsOrigins.Length > 0)
            {
                policy
                    .WithOrigins(allowedCorsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
LogStartupConfiguration(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (allowedCorsOrigins.Length > 0)
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
    "/api/signals/performance-preview",
    async (ISignalPerformancePreviewService signalPerformancePreviewService, int? days, CancellationToken cancellationToken) =>
    {
        var result = await signalPerformancePreviewService.GenerateAsync(
            days ?? 180,
            cancellationToken);

        return Results.Ok(result);
    });

app.MapPost(
    "/api/signals/outcomes/evaluate",
    async (ISignalOutcomeService signalOutcomeService, int? limit, CancellationToken cancellationToken) =>
    {
        var result = await signalOutcomeService.EvaluateAsync(limit, cancellationToken);
        return Results.Ok(result);
    });

app.MapPost(
    "/api/alerts/evaluate",
    async (IAlertEvaluationService alertEvaluationService, int? limit, CancellationToken cancellationToken) =>
    {
        var result = await alertEvaluationService.EvaluateAsync(limit, cancellationToken);
        return Results.Ok(result);
    });

app.MapPost(
    "/api/system/run-cycle",
    async (
        IManualSystemCycleService manualSystemCycleService,
        int? outcomeLimit,
        int? alertLimit,
        CancellationToken cancellationToken) =>
    {
        var result = await manualSystemCycleService.RunAsync(
            new ManualSystemCycleRequest(outcomeLimit, alertLimit),
            cancellationToken);

        return Results.Ok(result);
    });

app.MapPost(
    "/api/alerts/deliver/email",
    async (
        IEmailAlertDeliveryService emailAlertDeliveryService,
        int? limit,
        bool? retryFailed,
        int? sinceMinutes,
        CancellationToken cancellationToken) =>
    {
        var result = await emailAlertDeliveryService.DeliverAsync(
            new EmailAlertDeliveryRequest(limit, retryFailed ?? false, sinceMinutes),
            cancellationToken);

        return Results.Ok(result);
    });

app.MapGet(
    "/api/system/status",
    (
        MarketAgentSchedulerOptions schedulerOptions,
        EmailDeliveryOptions emailOptions,
        IMarketAgentCycleSchedulerRunner schedulerRunner,
        IWebHostEnvironment environment) =>
    {
        return Results.Ok(new
        {
            schedulerEnabled = schedulerOptions.Enabled,
            intervalMinutes = schedulerOptions.GetSafeIntervalMinutes(),
            marketHoursOnly = schedulerOptions.MarketHoursOnly,
            emailConfigured = IsEmailDeliveryConfigured(emailOptions),
            lastCycleRunUtc = schedulerRunner.LastCycleRunUtc,
            environmentName = environment.EnvironmentName
        });
    });

app.MapGet(
    "/api/alerts",
    async (IAlertEvaluationService alertEvaluationService, int? limit, CancellationToken cancellationToken) =>
    {
        var result = await alertEvaluationService.GetAlertsAsync(
            new AlertEventQuery(limit),
            cancellationToken);

        return Results.Ok(result);
    });

app.MapGet(
    "/api/signals/{signalSnapshotId:guid}/score-attribution",
    async (IScoreAttributionService scoreAttributionService, Guid signalSnapshotId, CancellationToken cancellationToken) =>
    {
        var result = await scoreAttributionService.GetAsync(signalSnapshotId, cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    });

app.MapGet(
    "/api/signals/outcomes",
    async (
        ISignalOutcomeService signalOutcomeService,
        string? symbol,
        string? status,
        bool? isSuccessful,
        int? days,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var result = await signalOutcomeService.GetOutcomesAsync(
            new SignalOutcomeQuery(symbol, status, isSuccessful, days, limit),
            cancellationToken);

        return Results.Ok(result);
    });

app.MapGet(
    "/api/signals/outcomes/summary",
    async (
        ISignalOutcomeService signalOutcomeService,
        string? symbol,
        string? status,
        bool? isSuccessful,
        int? days,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var result = await signalOutcomeService.GetSummaryAsync(
            new SignalOutcomeQuery(symbol, status, isSuccessful, days, limit),
            cancellationToken);

        return Results.Ok(result);
    });

app.MapGet(
    "/api/signals/outcomes/setup-summary",
    async (
        ISignalOutcomeService signalOutcomeService,
        string? symbol,
        string? status,
        bool? isSuccessful,
        int? days,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var result = await signalOutcomeService.GetSetupSummaryAsync(
            new SignalOutcomeQuery(symbol, status, isSuccessful, days, limit),
            cancellationToken);

        return Results.Ok(result);
    });

app.MapGet(
    "/api/signals/outcomes/score-buckets",
    async (
        ISignalOutcomeService signalOutcomeService,
        string? symbol,
        string? status,
        bool? isSuccessful,
        int? days,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var result = await signalOutcomeService.GetScoreBucketSummaryAsync(
            new SignalOutcomeQuery(symbol, status, isSuccessful, days, limit),
            cancellationToken);

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

static void LogStartupConfiguration(WebApplication app)
{
    var schedulerOptions = app.Services.GetRequiredService<MarketAgentSchedulerOptions>();
    var emailOptions = app.Services.GetRequiredService<EmailDeliveryOptions>();
    var emailConfigured = IsEmailDeliveryConfigured(emailOptions);

    app.Logger.LogInformation(
        "MarketAgentScheduler config. Enabled: {Enabled}. IntervalMinutes: {IntervalMinutes}. RunEmailDelivery: {RunEmailDelivery}. MarketHoursOnly: {MarketHoursOnly}. RunOnStartup: {RunOnStartup}.",
        schedulerOptions.Enabled,
        schedulerOptions.GetSafeIntervalMinutes(),
        schedulerOptions.RunEmailDelivery,
        schedulerOptions.MarketHoursOnly,
        schedulerOptions.RunOnStartup);

    app.Logger.LogInformation(
        "EmailDelivery config. SmtpHostConfigured: {SmtpHostConfigured}. ToEmailConfigured: {ToEmailConfigured}. FromEmailConfigured: {FromEmailConfigured}.",
        !string.IsNullOrWhiteSpace(emailOptions.SmtpHost),
        !string.IsNullOrWhiteSpace(emailOptions.ToEmail),
        !string.IsNullOrWhiteSpace(emailOptions.FromEmail));

    if (schedulerOptions.Enabled && schedulerOptions.IntervalMinutes <= 0)
    {
        app.Logger.LogWarning(
            "MarketAgentScheduler interval is invalid ({IntervalMinutes}); effective interval is {EffectiveIntervalMinutes} minutes.",
            schedulerOptions.IntervalMinutes,
            schedulerOptions.GetSafeIntervalMinutes());
    }

    if (schedulerOptions.Enabled && schedulerOptions.RunEmailDelivery && !emailConfigured)
    {
        app.Logger.LogWarning("MarketAgentScheduler email delivery is enabled, but EmailDelivery SMTP configuration is incomplete.");
    }
}

static bool IsEmailDeliveryConfigured(EmailDeliveryOptions options)
{
    return !string.IsNullOrWhiteSpace(options.SmtpHost)
        && !string.IsNullOrWhiteSpace(options.ToEmail)
        && !string.IsNullOrWhiteSpace(options.FromEmail)
        && options.SmtpPort > 0;
}
