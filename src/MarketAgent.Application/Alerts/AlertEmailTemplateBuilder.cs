using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using MarketAgent.Application.Models;

namespace MarketAgent.Application.Alerts;

public static class AlertEmailTemplateBuilder
{
    public static EmailMessage BuildMessage(
        IReadOnlyCollection<AlertEventItem> alerts,
        EmailDeliveryOptions options,
        DateTime generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentNullException.ThrowIfNull(options);

        var orderedAlerts = alerts
            .OrderByDescending(alert => alert.Score)
            .ThenByDescending(alert => ConfidenceRank(alert.Confidence))
            .ThenByDescending(alert => EnsureUtc(alert.CreatedAtUtc))
            .ThenBy(alert => alert.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var topSymbols = orderedAlerts
            .Select(alert => alert.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        var subject = alerts.Count == 1
            ? $"[MarketAgent] 1 alerta nueva - {topSymbols.FirstOrDefault() ?? "Mercado"}"
            : $"[MarketAgent] {alerts.Count} alertas nuevas - {string.Join(", ", topSymbols)}";

        return new EmailMessage(
            options.FromEmail ?? string.Empty,
            string.IsNullOrWhiteSpace(options.FromName) ? "MarketAgent" : options.FromName.Trim(),
            options.ToEmail ?? string.Empty,
            subject,
            BuildHtml(alerts, generatedAtUtc));
    }

    public static string BuildHtml(
        IReadOnlyCollection<AlertEventItem> alerts,
        DateTime generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(alerts);

        var orderedAlerts = alerts
            .OrderByDescending(alert => alert.Score)
            .ThenByDescending(alert => ConfidenceRank(alert.Confidence))
            .ThenByDescending(alert => EnsureUtc(alert.CreatedAtUtc))
            .ThenBy(alert => alert.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var topAlert = orderedAlerts.FirstOrDefault();
        var highestScore = orderedAlerts.Length == 0
            ? "n/a"
            : FormatDecimal(orderedAlerts.Max(alert => alert.Score));
        var bestUpside = orderedAlerts
            .Select(alert => new { Alert = alert, Upside = AlertUpsideCalculator.Calculate(alert) })
            .Where(item => item.Upside is not null)
            .OrderByDescending(item => item.Upside!.PotentialUpsidePercent)
            .FirstOrDefault();
        var bestUpsideText = bestUpside is null
            ? "-"
            : $"{bestUpside.Alert.Symbol} {FormatSignedPercent(bestUpside.Upside!.PotentialUpsidePercent)}";
        var rows = new StringBuilder();

        foreach (var alert in orderedAlerts)
        {
            var reason = BuildReasonSummary(alert.ReasonJson);
            var upside = AlertUpsideCalculator.Calculate(alert);
            var upsideText = upside is null
                ? "-"
                : FormatSignedPercent(upside.PotentialUpsidePercent);
            var entryText = upside is null
                ? "-"
                : FormatCurrency(upside.Entry);
            var takeProfitText = upside is null
                ? "-"
                : FormatCurrency(upside.TakeProfit);
            var upsideBadge = BuildUpsideBadge(upside?.PotentialUpsidePercent);

            rows.AppendLine($"""
                <tr>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;"><strong style="color:#f8fbff;">{Escape(alert.Symbol)}</strong></td>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;"><span style="display:inline-block;border-radius:999px;padding:4px 8px;font-size:11px;font-weight:700;white-space:nowrap;color:#99f6e4;background:#143235;">{Escape(alert.Setup)}</span></td>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;text-align:right;white-space:nowrap;">{FormatDecimal(alert.Score)}</td>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;">{BuildConfidenceBadge(alert.Confidence)}</td>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;text-align:right;white-space:nowrap;">{FormatCurrency(alert.PriceAtSignal)}</td>
                  <td style="padding:10px 8px;color:#86efac;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;text-align:right;white-space:nowrap;font-weight:700;">{Escape(upsideText)}{upsideBadge}</td>
                  <td style="padding:10px 8px;color:#9fb0c9;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;text-align:right;white-space:nowrap;">{Escape(entryText)}</td>
                  <td style="padding:10px 8px;color:#9fb0c9;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;text-align:right;white-space:nowrap;">{Escape(takeProfitText)}</td>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;">{Escape(alert.Title)}</td>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;">{Escape(alert.Message)}</td>
                  <td style="padding:10px 8px;color:#9fb0c9;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;">{Escape(reason)}</td>
                  <td style="padding:10px 8px;color:#d8e3f3;border-bottom:1px solid rgba(148,163,184,0.16);vertical-align:top;font-weight:700;white-space:nowrap;">{Escape(EnsureUtc(alert.CreatedAtUtc).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))}</td>
                </tr>
                """);
        }

        return $$"""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <title>Resumen de alertas MarketAgent</title>
            </head>
            <body style="margin:0;padding:0;background:#090d13;color:#e5eefc;font-family:Arial,Helvetica,sans-serif;">
              <div style="padding:24px;background:#090d13;">
                <div style="max-width:1180px;margin:0 auto;border:1px solid #263346;border-radius:12px;background:#0d131d;overflow:hidden;">
                  <div style="padding:22px 24px;background:#101722;border-bottom:1px solid #263346;">
                    <div style="color:#8aa0bd;font-size:12px;font-weight:700;text-transform:uppercase;">MarketAgent</div>
                    <h1 style="margin:6px 0 0;color:#f8fbff;font-size:26px;line-height:1.15;">Resumen de alertas MarketAgent</h1>
                    <p style="margin:8px 0 0;color:#9fb0c9;font-size:14px;">Generado {{Escape(generatedAtUtc.ToString("u", CultureInfo.InvariantCulture))}}</p>
                  </div>
                  <div style="padding:18px 24px;border-bottom:1px solid #263346;">
                    <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;border-collapse:collapse;">
                      <tr>
                        <td style="padding:10px 12px;border:1px solid #263346;background:#101722;border-radius:8px;"><span style="display:block;color:#8aa0bd;font-size:11px;font-weight:700;text-transform:uppercase;">Alertas</span><strong style="display:block;margin-top:4px;color:#f8fbff;font-size:18px;">{{alerts.Count}}</strong></td>
                        <td style="padding:10px 12px;border:1px solid #263346;background:#101722;border-radius:8px;"><span style="display:block;color:#8aa0bd;font-size:11px;font-weight:700;text-transform:uppercase;">Mejor candidato</span><strong style="display:block;margin-top:4px;color:#f8fbff;font-size:18px;">{{Escape(topAlert?.Symbol ?? "n/a")}}</strong></td>
                        <td style="padding:10px 12px;border:1px solid #263346;background:#101722;border-radius:8px;"><span style="display:block;color:#8aa0bd;font-size:11px;font-weight:700;text-transform:uppercase;">Score más alto</span><strong style="display:block;margin-top:4px;color:#f8fbff;font-size:18px;">{{highestScore}}</strong></td>
                        <td style="padding:10px 12px;border:1px solid #263346;background:#101722;border-radius:8px;"><span style="display:block;color:#8aa0bd;font-size:11px;font-weight:700;text-transform:uppercase;">Mejor upside</span><strong style="display:block;margin-top:4px;color:#f8fbff;font-size:18px;">{{Escape(bestUpsideText)}}</strong></td>
                      </tr>
                    </table>
                  </div>
                  <div style="padding:18px 24px;overflow-x:auto;">
                    <table cellpadding="0" cellspacing="0" style="width:100%;border-collapse:collapse;font-size:13px;">
                      <thead>
                        <tr>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:left;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Símbolo</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:left;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Setup</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:right;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Score</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:left;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Confianza</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:right;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Precio</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:right;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Upside</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:right;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Entrada</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:right;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">TP</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:left;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Título</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:left;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Mensaje</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:left;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Motivo</th>
                          <th style="padding:9px 8px;color:#9fb0c9;text-align:left;border-bottom:1px solid #263346;font-size:11px;text-transform:uppercase;">Creada UTC</th>
                        </tr>
                      </thead>
                      <tbody>
                        {{rows}}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private static string BuildConfidenceBadge(string confidence)
    {
        if (confidence.Equals("High", StringComparison.OrdinalIgnoreCase))
        {
            return """<span style="display:inline-block;border-radius:999px;padding:4px 8px;font-size:11px;font-weight:700;white-space:nowrap;color:#86efac;background:#12351f;">ALTA CONFIANZA</span>""";
        }

        if (confidence.Equals("Medium", StringComparison.OrdinalIgnoreCase))
        {
            return """<span style="display:inline-block;border-radius:999px;padding:4px 8px;font-size:11px;font-weight:700;white-space:nowrap;color:#fde68a;background:#342c12;">CONFIANZA MEDIA</span>""";
        }

        return Escape(confidence);
    }

    private static string BuildUpsideBadge(decimal? potentialUpsidePct)
    {
        if (potentialUpsidePct is >= 20m)
        {
            return """ <span style="display:inline-block;border-radius:999px;padding:3px 7px;font-size:10px;font-weight:700;white-space:nowrap;color:#86efac;background:#12351f;">UPSIDE ALTO</span>""";
        }

        if (potentialUpsidePct is >= 10m)
        {
            return """ <span style="display:inline-block;border-radius:999px;padding:3px 7px;font-size:10px;font-weight:700;white-space:nowrap;color:#99f6e4;background:#143235;">BUEN UPSIDE</span>""";
        }

        return string.Empty;
    }

    private static string BuildReasonSummary(string reasonJson)
    {
        if (string.IsNullOrWhiteSpace(reasonJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(reasonJson);
            var root = document.RootElement;
            var parts = new List<string>();

            AddDecimal(parts, root, "setupAverageReturn15m", "promedio setup 15m");
            AddDecimal(parts, root, "minimumScore", "umbral score");
            AddString(parts, root, "confidence", "confianza");

            if (root.TryGetProperty("ruleDecisions", out var ruleDecisions) &&
                ruleDecisions.ValueKind == JsonValueKind.Object &&
                ruleDecisions.TryGetProperty("meetsSetupPerformance", out var meetsSetupPerformance) &&
                (meetsSetupPerformance.ValueKind == JsonValueKind.True || meetsSetupPerformance.ValueKind == JsonValueKind.False))
            {
                parts.Add($"performance setup: {meetsSetupPerformance.GetBoolean()}");
            }

            return string.Join("; ", parts.Take(4));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void AddDecimal(List<string> parts, JsonElement root, string propertyName, string label)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
        {
            parts.Add($"{label}: {FormatDecimal(value.GetDecimal())}");
        }
    }

    private static void AddString(List<string> parts, JsonElement root, string propertyName, string label)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            parts.Add($"{label}: {value.GetString()}");
        }
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedPercent(decimal value)
    {
        return value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
    }

    private static int ConfidenceRank(string confidence)
    {
        return confidence.Trim() switch
        {
            var value when value.Equals("High", StringComparison.OrdinalIgnoreCase) => 3,
            var value when value.Equals("Medium", StringComparison.OrdinalIgnoreCase) => 2,
            var value when value.Equals("Low", StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string Escape(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
