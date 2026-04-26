using System.Text.RegularExpressions;
using InventoryControl.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Features.Logs;

[Authorize(Roles = "Admin")]
public class LogsController : Controller
{
    // Matches the Serilog file template: [yyyy-MM-dd HH:mm:ss.fff zzz LVL]
    // e.g.  [2026-04-20 14:30:00.000 +00:00 INF]
    private static readonly Regex LevelRegex =
        new(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} (\w{3})\]",
            RegexOptions.Compiled);

    private const int MaxDisplayLines = 2000;

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LogsController> _logger;

    public LogsController(IWebHostEnvironment env, ILogger<LogsController> logger)
    {
        _env = env;
        _logger = logger;
    }

    public IActionResult Index(string? date = null, string? level = null, string? search = null)
    {
        var vm = new LogViewerViewModel
        {
            LevelFilter = level,
            SearchTerm = search
        };

        var logsDir = Path.Combine(_env.ContentRootPath, "logs");
        vm.LogDirExists = Directory.Exists(logsDir);

        if (!vm.LogDirExists)
            return View(vm);

        // Collect available log dates from file names (inventory-YYYYMMDD.log)
        var files = Directory.GetFiles(logsDir, "inventory-*.log")
            .OrderByDescending(f => f)
            .ToList();

        vm.AvailableDates = files
            .Select(f => Path.GetFileNameWithoutExtension(f)
                .Replace("inventory-", "", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Default to today (or the most recent file)
        vm.SelectedDate = date ?? vm.AvailableDates.FirstOrDefault();

        if (vm.SelectedDate is null)
            return View(vm);

        var logFile = Path.Combine(logsDir, $"inventory-{vm.SelectedDate}.log");

        if (!System.IO.File.Exists(logFile))
            return View(vm);

        var allLines = ReadLogLines(logFile);

        // Parse lines into LogLine objects, tracking continuation lines (exceptions, stack traces)
        var parsed = ParseLines(allLines);

        // Apply level filter
        if (!string.IsNullOrEmpty(level) && level != "ALL")
        {
            parsed = FilterByLevel(parsed, level);
        }

        // Apply text search
        if (!string.IsNullOrEmpty(search))
        {
            parsed = FilterBySearch(parsed, search);
        }

        vm.TotalMatchedLines = parsed.Count;

        // Show newest entries first, truncate to MaxDisplayLines
        parsed.Reverse();
        if (parsed.Count > MaxDisplayLines)
        {
            parsed = parsed.Take(MaxDisplayLines).ToList();
            vm.IsTruncated = true;
        }

        vm.Lines = parsed;
        return View(vm);
    }

    // -------------------------------------------------------------------------

    private static List<string> ReadLogLines(string path)
    {
        // Open with ReadWrite share so we can read while Serilog has the file open
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);
        return lines;
    }

    private static List<LogLine> ParseLines(List<string> rawLines)
    {
        var result = new List<LogLine>();
        string currentLevel = "INF";

        foreach (var raw in rawLines)
        {
            var match = LevelRegex.Match(raw);
            if (match.Success)
            {
                currentLevel = match.Groups[1].Value.ToUpperInvariant();
                result.Add(new LogLine { Raw = raw, Level = currentLevel, IsContinuation = false });
            }
            else
            {
                // Continuation (e.g. exception stack trace)
                result.Add(new LogLine { Raw = raw, Level = currentLevel, IsContinuation = true });
            }
        }

        return result;
    }

    /// <summary>
    /// Filters groups: keeps a log entry (first line + all continuation lines) if the entry's
    /// level matches. Continuation lines are always included with their parent.
    /// </summary>
    private static List<LogLine> FilterByLevel(List<LogLine> lines, string level)
    {
        var result = new List<LogLine>();
        bool include = false;

        foreach (var line in lines)
        {
            if (!line.IsContinuation)
                include = line.Level == level.ToUpperInvariant();

            if (include)
                result.Add(line);
        }

        return result;
    }

    private static List<LogLine> FilterBySearch(List<LogLine> lines, string search)
    {
        // Group entries (first line + continuations), keep group if any line matches
        var groups = new List<List<LogLine>>();
        List<LogLine>? current = null;

        foreach (var line in lines)
        {
            if (!line.IsContinuation)
            {
                current = new List<LogLine> { line };
                groups.Add(current);
            }
            else
            {
                current?.Add(line);
            }
        }

        return groups
            .Where(g => g.Any(l => l.Raw.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(g => g)
            .ToList();
    }
}
