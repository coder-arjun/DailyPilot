using DailyPilot.Models;

namespace DailyPilot.ViewModels;

/// <summary>Month calendar view (PRD §4 Calendar View).</summary>
public class CalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly Today { get; set; }

    public string MonthName => new DateOnly(Year, Month, 1).ToString("MMMM yyyy");
    public DateOnly PrevMonth => new DateOnly(Year, Month, 1).AddMonths(-1);
    public DateOnly NextMonth => new DateOnly(Year, Month, 1).AddMonths(1);

    public List<CalendarDay> Days { get; set; } = new();
}

public class CalendarDay
{
    public DateOnly Date { get; set; }
    public bool InMonth { get; set; }
    public int Planned { get; set; }
    public int Completed { get; set; }
    public bool IsToday { get; set; }
    public double CompletionRate => Planned == 0 ? 0 : Math.Round(Completed * 100.0 / Planned, 0);
}
