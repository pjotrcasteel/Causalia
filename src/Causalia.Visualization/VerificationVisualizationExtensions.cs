using System.Net;
using System.Text;
using Causalia.Verification;

namespace Causalia.Visualization;

/// <summary>
/// Exports unified verification-platform outcomes as standalone HTML suitable for CI artifacts.
/// </summary>
public static class VerificationVisualizationExtensions
{
    /// <summary>
    /// Exports one verification run as a standalone dependency-free HTML report.
    /// </summary>
    public static string ToVerificationHtml(this VerificationRunResult result, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var report = result.Report;
        var heading = string.IsNullOrWhiteSpace(title) ? $"Causalia Verification · {report.PlanName}" : title;
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append("<title>").Append(Encode(heading)).Append("</title>");
        html.Append("<style>body{font-family:system-ui,sans-serif;margin:0;background:#101214;color:#edf0f2}main{max-width:1180px;margin:auto;padding:28px}");
        html.Append("h1{margin:0 0 8px}.meta{color:#aab2b9;margin-bottom:24px}.cards{display:flex;gap:12px;flex-wrap:wrap;margin:18px 0}");
        html.Append(".card{background:#1a1e22;border:1px solid #30363d;border-radius:10px;padding:14px 18px;min-width:120px}.value{font-size:24px;font-weight:700}");
        html.Append("table{width:100%;border-collapse:collapse;background:#171a1e}th,td{text-align:left;padding:10px;border-bottom:1px solid #30363d;vertical-align:top}");
        html.Append("th{color:#aab2b9}.pass{color:#69db7c}.fail{color:#ff8787}.skip{color:#ffd43b}code{font-family:ui-monospace,monospace;font-size:12px;word-break:break-all}");
        html.Append("</style></head><body><main><h1>").Append(Encode(heading)).Append("</h1><div class=\"meta\"><code>")
            .Append(Encode(report.Fingerprint)).Append("</code></div>");
        html.Append("<div class=\"cards\">");
        Card(html, "Status", report.Passed ? "PASS" : "FAIL");
        Card(html, "Steps", result.Steps.Count.ToString());
        Card(html, "Failed", result.FailedCount.ToString());
        Card(html, "Unique failures", result.FailureIntelligence.UniqueFailureCount.ToString());
        html.Append(
            "</div><h2>Verification steps</h2><table><thead><tr><th>Step</th><th>Engine</th><th>Status</th>" +
            "<th>Seed</th><th>Summary</th><th>Replay</th></tr></thead><tbody>");

        foreach (var step in report.Steps)
        {
            html.Append("<tr><td>").Append(Encode(step.Name)).Append("</td><td>").Append(Encode(step.Kind.ToString())).Append("</td><td class=\"")
                .Append(StatusClass(step.Status)).Append("\">").Append(Encode(step.Status.ToString())).Append("</td><td>").Append(step.Seed)
                .Append("</td><td>").Append(Encode(step.Summary)).Append("</td><td><code>").Append(Encode(step.ScheduleReplayToken ?? string.Empty))
                .Append("</code></td></tr>");
        }

        html.Append("</tbody></table>");

        if (report.Failures.Count > 0)
        {
            html.Append(
                "<h2>Failure intelligence</h2><table><thead><tr><th>Rank</th><th>Signature</th><th>Kind</th>" +
                "<th>Occurrences</th><th>Trigger</th><th>Summary</th></tr></thead><tbody>");
            foreach (var failure in report.Failures)
            {
                html.Append("<tr><td>").Append(failure.TriageRank).Append("</td><td><code>").Append(Encode(failure.Signature))
                    .Append("</code></td><td>").Append(Encode(failure.Kind)).Append("</td><td>").Append(failure.OccurrenceCount)
                    .Append("</td><td>").Append(Encode(failure.Trigger)).Append("</td><td>").Append(Encode(failure.Summary)).Append("</td></tr>");
            }
            html.Append("</tbody></table>");
        }

        html.Append("</main></body></html>");
        return html.ToString();
    }

    private static void Card(StringBuilder html, string label, string value)
    {
        html.Append("<div class=\"card\"><div>").Append(Encode(label)).Append("</div><div class=\"value\">")
            .Append(Encode(value)).Append("</div></div>");
    }

    private static string StatusClass(VerificationStepStatus status)
    {
        return status switch
        {
            VerificationStepStatus.Passed => "pass",
            VerificationStepStatus.Failed => "fail",
            VerificationStepStatus.Skipped => "skip",
            _ => string.Empty
        };
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
