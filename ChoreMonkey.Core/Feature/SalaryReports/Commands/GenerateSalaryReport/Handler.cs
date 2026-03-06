using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace ChoreMonkey.Core.Feature.SalaryReports.Commands.GenerateSalaryReport;

public record GenerateSalaryReportCommand(Guid HouseholdId, Guid MemberId, string Period);
public record GenerateSalaryReportRequest(Guid MemberId, string Period);

public record GenerateSalaryReportResponse(
    bool Success,
    string? Period,
    decimal? BaseAmount,
    List<DeductionDto>? Deductions,
    decimal? FinalAmount,
    string? Error);

public record DeductionDto(Guid ChoreId, string ChoreName, string MissedPeriod, decimal Amount);

internal class Handler(IEventStore store)
{
    public async Task<GenerateSalaryReportResponse> HandleAsync(GenerateSalaryReportCommand request)
    {
        var salaryStreamId = SalaryReportsAggregate.StreamId(request.HouseholdId);
        var householdStreamId = HouseholdAggregate.StreamId(request.HouseholdId);
        var choreStreamId = ChoreAggregate.StreamId(request.HouseholdId);
        
        // Verify salary reports are enabled
        var salaryEvents = await store.FetchEventsAsync(salaryStreamId);
        var enabledEvent = salaryEvents.OfType<SalaryReportsEnabled>().FirstOrDefault();
        if (enabledEvent == null)
        {
            return new GenerateSalaryReportResponse(false, null, null, null, null, "Salary reports not enabled");
        }
        
        // Get current base amount (could be updated)
        var baseAmount = salaryEvents.OfType<BaseSalaryUpdated>()
            .Select(e => (decimal?)e.NewBaseAmount)
            .LastOrDefault() ?? enabledEvent.BaseAmount;
        
        // Verify member exists
        var householdEvents = await store.FetchEventsAsync(householdStreamId);
        var memberExists = householdEvents.OfType<MemberJoinedHousehold>()
            .Any(e => e.MemberId == request.MemberId);
        if (!memberExists)
        {
            return new GenerateSalaryReportResponse(false, null, null, null, null, "Member not found");
        }
        
        // Parse period
        if (!TryParsePeriod(request.Period, out var periodStart, out var periodEnd, out var periodType))
        {
            return new GenerateSalaryReportResponse(false, null, null, null, null, "Invalid period format. Use YYYY-Wnn for weeks or YYYY-MM for months");
        }
        
        // Get chores and completions
        var choreEvents = await store.FetchEventsAsync(choreStreamId);
        
        var deletedChoreIds = choreEvents.OfType<ChoreDeleted>()
            .Select(e => e.ChoreId)
            .ToHashSet();
        
        var chores = choreEvents.OfType<ChoreCreated>()
            .Where(c => !deletedChoreIds.Contains(c.ChoreId))
            .ToDictionary(e => e.ChoreId);
        
        var assignments = choreEvents.OfType<ChoreAssigned>()
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.Last());
        
        var completions = choreEvents.OfType<ChoreCompleted>()
            .Where(c => c.CompletedByMemberId == request.MemberId)
            .GroupBy(e => e.ChoreId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        var deductions = new List<SalaryDeduction>();
        
        foreach (var (choreId, chore) in chores)
        {
            // Skip if not required for salary
            if (!chore.IsRequired) continue;
            
            // Skip optional/bonus chores
            if (chore.IsOptional) continue;
            
            // Check if assigned to this member
            var assignment = assignments.GetValueOrDefault(choreId);
            var isAssignedToMe = assignment?.AssignToAll == true ||
                (assignment?.AssignedToMemberIds?.Contains(request.MemberId) ?? false);
            
            if (!isAssignedToMe) continue;
            
            // Get chore's start date
            var choreStartDate = chore.StartDate?.Date 
                ?? (DateTime.TryParse(chore.TimestampUtc, out var parsed) ? parsed.Date : DateTime.UtcNow.Date);
            
            // Calculate missed periods within the report period
            var choreCompletions = completions.GetValueOrDefault(choreId) ?? [];
            var missedPeriods = GetMissedPeriods(chore, choreCompletions, periodStart, periodEnd, choreStartDate);
            
            foreach (var missedPeriod in missedPeriods)
            {
                deductions.Add(new SalaryDeduction(
                    choreId,
                    chore.DisplayName,
                    missedPeriod,
                    chore.MissedDeduction));
            }
        }
        
        var totalDeductions = deductions.Sum(d => d.Amount);
        var finalAmount = Math.Max(0, baseAmount - totalDeductions);
        
        // Record the generated report
        var reportEvent = new SalaryReportGenerated(
            request.HouseholdId,
            request.MemberId,
            request.Period,
            baseAmount,
            deductions.ToArray(),
            finalAmount);
        
        await store.AppendToStreamAsync(salaryStreamId, reportEvent, ExpectedVersion.Any);
        
        var deductionDtos = deductions.Select(d => 
            new DeductionDto(d.ChoreId, d.ChoreName, d.MissedPeriod, d.Amount)).ToList();
        
        return new GenerateSalaryReportResponse(true, request.Period, baseAmount, deductionDtos, finalAmount, null);
    }
    
    private static bool TryParsePeriod(string period, out DateTime start, out DateTime end, out string periodType)
    {
        start = default;
        end = default;
        periodType = "";
        
        // Week format: YYYY-Wnn (e.g., 2026-W10)
        if (period.Contains("-W"))
        {
            var parts = period.Split("-W");
            if (parts.Length == 2 && 
                int.TryParse(parts[0], out var year) && 
                int.TryParse(parts[1], out var week))
            {
                start = FirstDateOfWeekISO8601(year, week);
                end = start.AddDays(7);
                periodType = "week";
                return true;
            }
        }
        
        // Month format: YYYY-MM (e.g., 2026-03)
        if (period.Length == 7 && period[4] == '-')
        {
            if (DateTime.TryParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
            {
                end = start.AddMonths(1);
                periodType = "month";
                return true;
            }
        }
        
        return false;
    }
    
    private static DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
    {
        var jan1 = new DateTime(year, 1, 1);
        var daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;
        
        var firstThursday = jan1.AddDays(daysOffset);
        var cal = CultureInfo.InvariantCulture.Calendar;
        var firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        
        var weekNum = weekOfYear;
        if (firstWeek == 1)
        {
            weekNum -= 1;
        }
        
        var result = firstThursday.AddDays(weekNum * 7);
        return result.AddDays(-3); // Go back to Monday
    }
    
    private static List<string> GetMissedPeriods(
        ChoreCreated chore,
        List<ChoreCompleted> completions,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime choreStartDate)
    {
        var missed = new List<string>();
        var frequency = chore.Frequency;
        
        // One-time chores can't be missed in a recurring way
        if (frequency == null || frequency.Type.ToLower() == "once")
            return missed;
        
        switch (frequency.Type.ToLower())
        {
            case "daily":
                // Check each day in the period
                for (var date = periodStart; date < periodEnd; date = date.AddDays(1))
                {
                    if (date < choreStartDate) continue;
                    
                    var completed = completions.Any(c => c.CompletedAt.Date == date);
                    if (!completed)
                    {
                        missed.Add(date.ToString("yyyy-MM-dd"));
                    }
                }
                break;
                
            case "weekly":
                // Check each week in the period
                var weekStart = GetMondayOfWeek(periodStart);
                while (weekStart < periodEnd)
                {
                    var weekEnd = weekStart.AddDays(7);
                    if (weekStart >= choreStartDate || weekEnd > choreStartDate)
                    {
                        var completed = completions.Any(c => 
                            c.CompletedAt.Date >= weekStart && c.CompletedAt.Date < weekEnd);
                        if (!completed && weekEnd <= periodEnd)
                        {
                            missed.Add(GetWeekPeriod(weekStart));
                        }
                    }
                    weekStart = weekEnd;
                }
                break;
                
            case "interval":
                var intervalDays = frequency.IntervalDays ?? 1;
                var lastCompletion = completions
                    .Where(c => c.CompletedAt.Date < periodStart)
                    .MaxBy(c => c.CompletedAt);
                
                var dueDate = lastCompletion?.CompletedAt.Date.AddDays(intervalDays) ?? choreStartDate.AddDays(intervalDays);
                
                while (dueDate < periodEnd)
                {
                    if (dueDate >= periodStart && dueDate >= choreStartDate)
                    {
                        var completed = completions.Any(c => 
                            c.CompletedAt.Date >= dueDate.AddDays(-intervalDays) && 
                            c.CompletedAt.Date <= dueDate);
                        if (!completed)
                        {
                            missed.Add(dueDate.ToString("yyyy-MM-dd"));
                        }
                    }
                    dueDate = dueDate.AddDays(intervalDays);
                }
                break;
        }
        
        return missed;
    }
    
    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
    
    private static string GetWeekPeriod(DateTime date)
    {
        var cal = CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{date.Year}-W{week:D2}";
    }
}

internal static class GenerateSalaryReportEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapPost("households/{householdId:guid}/salary/report", async (Guid householdId, GenerateSalaryReportRequest dto, Handler handler) =>
        {
            var command = new GenerateSalaryReportCommand(householdId, dto.MemberId, dto.Period);
            var result = await handler.HandleAsync(command);
            
            if (!result.Success)
                return Results.BadRequest(result);
            
            return Results.Ok(result);
        });
    }
}
