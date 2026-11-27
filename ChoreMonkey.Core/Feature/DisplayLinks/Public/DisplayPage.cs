using System.Globalization;
using System.Net;
using System.Text;
using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayLately;
using ChoreMonkey.Core.Feature.DisplayLinks.Queries.DisplayToday;

namespace ChoreMonkey.Core.Feature.DisplayLinks.Public;

/// <summary>The small HTML page of what a connected screen sees. No scripts; every value from the household is HTML-encoded.</summary>
internal static class DisplayPage
{
    public static string Render(DisplayTodayResponse today, DisplayLatelyResponse lately)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var name = E(today.HouseholdName);
        var html = new StringBuilder();

        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"robots\" content=\"noindex\">")
            .Append("<meta name=\"referrer\" content=\"no-referrer\">")
            .Append($"<title>{name} · chores</title>")
            .Append("<style>")
            .Append("body{font:16px/1.4 system-ui,sans-serif;margin:0;padding:24px;background:#faf8f3;color:#2b2a27;max-width:760px}")
            .Append("h1{font-size:1.5rem;margin:0 0 4px}h2{font-size:1.05rem;margin:28px 0 8px}")
            .Append(".note{color:#6b665c;font-size:.9rem;margin:0}")
            .Append("ul{list-style:none;padding:0;margin:0}li{padding:6px 0;border-bottom:1px solid #e7e2d6}")
            .Append(".done{color:#6b665c;text-decoration:line-through}.by{color:#6b665c;font-size:.9rem}")
            .Append(".row{display:flex;align-items:center;gap:10px;margin:6px 0}.who{width:90px;flex:none;overflow:hidden;text-overflow:ellipsis}")
            .Append(".days{display:flex;gap:2px;flex-wrap:wrap}.d{width:9px;height:14px;border-radius:2px;background:#e7e2d6}")
            .Append(".l1{background:#bfdcae}.l2{background:#7fbf6b}.l3{background:#2f8a3d}.total{margin-left:auto;font-variant-numeric:tabular-nums}")
            .Append("</style></head><body>")
            .Append($"<h1>{name}</h1>")
            .Append("<p class=\"note\">What a connected screen (barrio) sees. No pay or money is ever shared.</p>");

        html.Append($"<h2>Today · {E(today.Day)}</h2>");
        if (today.Chores.Count == 0)
        {
            html.Append("<p>Nothing on the list today.</p>");
        }
        else
        {
            html.Append("<ul>");
            foreach (var chore in today.Chores)
            {
                var isDone = chore.State == "done";
                html.Append("<li>")
                    .Append($"<span class=\"{(isDone ? "done" : "open")}\">{E(chore.Title)}</span>")
                    .Append(isDone ? $" <span class=\"by\">done{(chore.By == null ? "" : " by " + E(chore.By))}</span>" : " <span class=\"by\">to do</span>")
                    .Append("</li>");
            }
            html.Append("</ul>");
        }

        html.Append("<h2>The last 40 days</h2>");
        if (lately.People.Count == 0)
        {
            html.Append("<p>Nobody has joined yet.</p>");
        }
        foreach (var person in lately.People)
        {
            html.Append("<div class=\"row\">")
                .Append($"<span class=\"who\">{E(person.Name)}</span><span class=\"days\">");
            foreach (var day in person.Days)
            {
                var level = Math.Min(day.Count, 3);
                var tip = $"{person.Name}, {day.Day}: {day.Count.ToString(CultureInfo.InvariantCulture)}";
                html.Append($"<span class=\"d l{level}\" title=\"{E(tip)}\"></span>");
            }
            html.Append($"</span><span class=\"total\">{person.Total.ToString(CultureInfo.InvariantCulture)}</span></div>");
        }

        html.Append("</body></html>");
        return html.ToString();
    }
}
