using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace ShiCi {
public class DailyStudy {
    public double Seconds { get; set; }
    public Dictionary<string, int> WordCounts { get; set; }
    public DailyStudy() { WordCounts = new Dictionary<string, int>(); }
}
public class DailySummary {
    public string Date;
    public int Words, Answers;
    public double Seconds;
}
public partial class Engine {
    static string StudyDate(DateTime value) { return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    static bool ValidStudyDate(string value) { DateTime parsed; return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed); }
    public string TodayKey { get { return StudyDate(now()); } }
    public void InitializeDaily() {
        if (Data.Daily == null) Data.Daily = new Dictionary<string, DailyStudy>();
        foreach (var pair in Data.Daily.ToList()) {
            if (!ValidStudyDate(pair.Key) || pair.Value == null) { Data.Daily.Remove(pair.Key); continue; }
            var d = pair.Value;
            if (double.IsNaN(d.Seconds) || double.IsInfinity(d.Seconds) || d.Seconds < 0) d.Seconds = 0;
            var counts = new Dictionary<string, int>();
            foreach (var entry in d.WordCounts ?? new Dictionary<string, int>()) {
                string word = entry.Key.Trim().ToLowerInvariant();
                if (word.Length == 0 || entry.Value <= 0) continue;
                int old; counts.TryGetValue(word, out old); counts[word] = old + entry.Value;
            }
            d.WordCounts = counts;
        }
        if (!ValidStudyDate(Data.DailyStartedOn)) Data.DailyStartedOn = Data.Daily.Keys.Concat(new[] { TodayKey }).Min();
    }
    DailyStudy DailyRecord(string date) {
        if (Data.Daily == null) InitializeDaily();
        DailyStudy record;
        if (!Data.Daily.TryGetValue(date, out record)) Data.Daily[date] = record = new DailyStudy();
        return record;
    }
    string RecordStudyAnswer(string word) {
        string date = TodayKey, key = word.Trim().ToLowerInvariant();
        var record = DailyRecord(date); int count;
        record.WordCounts.TryGetValue(key, out count); record.WordCounts[key] = count + 1;
        return date;
    }
    void UndoStudyAnswer(Answer answer) {
        // Legacy answers have no calendar date; never deduct from today's work.
        DailyStudy record;
        if (string.IsNullOrEmpty(answer.StudyDay) || !Data.Daily.TryGetValue(answer.StudyDay, out record)) return;
        string key = answer.Word.Text.Trim().ToLowerInvariant(); int count;
        if (!record.WordCounts.TryGetValue(key, out count)) return;
        if (count <= 1) record.WordCounts.Remove(key); else record.WordCounts[key] = count - 1;
    }
    public void AddStudyTime(DateTime start, double seconds) {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
        // Split a foreground interval at local midnight; idle time never reaches here.
        while (seconds > 0.0000001) {
            double part = Math.Min(seconds, (start.Date.AddDays(1) - start).TotalSeconds);
            DailyRecord(StudyDate(start)).Seconds += part;
            start = start.AddTicks((long)Math.Round(part * TimeSpan.TicksPerSecond)); seconds -= part;
        }
    }
    public DailySummary StudyStatistics(string date) {
        DailyStudy record;
        if (!Data.Daily.TryGetValue(date, out record)) return new DailySummary { Date = date };
        return new DailySummary { Date = date, Seconds = record.Seconds, Words = record.WordCounts.Count, Answers = record.WordCounts.Values.Sum() };
    }
    public List<DailySummary> StudyHistory() {
        return Data.Daily.Keys.Concat(new[] { TodayKey }).Distinct().OrderByDescending(k => k, StringComparer.Ordinal).Select(StudyStatistics).ToList();
    }
}
}
