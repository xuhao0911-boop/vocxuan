using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace ShiCi {
public class ReviewSchedule {
    public string DueDate { get; set; }
    public string LastReviewed { get; set; }
    public int Stage { get; set; }
    public ReviewSchedule Copy() { return new ReviewSchedule { DueDate=DueDate, LastReviewed=LastReviewed, Stage=Stage }; }
}
public partial class Engine {
    static readonly int[] ReviewIntervals = { 2, 7, 14, 30 };
    public DateTime Today { get { return now().Date; } }
    static bool ScheduleDate(ReviewSchedule schedule, out DateTime date) {
        date=DateTime.MinValue;
        return schedule!=null && schedule.Stage>=0 && schedule.Stage<ReviewIntervals.Length && DateTime.TryParseExact(schedule.DueDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date);
    }
    Dictionary<string,ReviewSchedule> Schedules(string book) {
        if(Data.Spaced==null)Data.Spaced=new Dictionary<string,Dictionary<string,ReviewSchedule>>();
        Dictionary<string,ReviewSchedule> result;
        if(!Data.Spaced.TryGetValue(book,out result)||result==null){result=new Dictionary<string,ReviewSchedule>(StringComparer.OrdinalIgnoreCase);Data.Spaced[book]=result;}
        return result;
    }
    ReviewSchedule NewSchedule(int stage) { return new ReviewSchedule { Stage=stage,LastReviewed=Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),DueDate=Today.AddDays(ReviewIntervals[stage]).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) }; }
    public void InitializeSchedules() {
        if(Data.Spaced==null)Data.Spaced=new Dictionary<string,Dictionary<string,ReviewSchedule>>();
        foreach(var key in Data.Spaced.Keys.ToList())Data.Spaced[key]=new Dictionary<string,ReviewSchedule>(Data.Spaced[key]??new Dictionary<string,ReviewSchedule>(),StringComparer.OrdinalIgnoreCase);
        foreach(var book in Data.Mastered.Keys.ToList()) {
            var schedules=Schedules(book);
            foreach(var word in MasteredWords(book)){ReviewSchedule schedule;DateTime due;if(!schedules.TryGetValue(word,out schedule)||!ScheduleDate(schedule,out due))schedules[word]=NewSchedule(0);}
        }
    }
    ReviewSchedule ScheduleSnapshot(string book,string word) { ReviewSchedule value;return Schedules(book).TryGetValue(word,out value)&&value!=null?value.Copy():null; }
    void RestoreSchedule(string book,string word,ReviewSchedule value) { if(value==null)Schedules(book).Remove(word);else Schedules(book)[word]=value.Copy(); }
    void UpdateSchedule(string book,string word,int rating,bool wasMastered,bool spaced) {
        var schedules=Schedules(book);
        if(rating!=0){schedules.Remove(word);return;}
        ReviewSchedule previous;DateTime due;
        if(!wasMastered||!schedules.TryGetValue(word,out previous)||!ScheduleDate(previous,out due)){schedules[word]=NewSchedule(0);return;}
        if(spaced&&due<=Today)schedules[word]=NewSchedule(Math.Min(ReviewIntervals.Length-1,previous.Stage+1));
    }
    public List<Word> DueWords(string book,int limit=10) {
        var mastered=MasteredWords(book);var schedules=Schedules(book);var due=new List<Word>();
        foreach(var word in Book(book).GroupBy(w=>w.Text,StringComparer.OrdinalIgnoreCase).Select(g=>g.First())) {
            if(!mastered.Contains(word.Text))continue;
            ReviewSchedule schedule;DateTime date;
            if(!schedules.TryGetValue(word.Text,out schedule)||!ScheduleDate(schedule,out date)){schedules[word.Text]=NewSchedule(0);continue;}
            if(date<=Today)due.Add(word);
        }
        return due.OrderBy(w=>schedules[w.Text].DueDate,StringComparer.Ordinal).ThenBy(w=>w.Text,StringComparer.OrdinalIgnoreCase).Take(Math.Max(0,Math.Min(limit,50000))).ToList();
    }
    public DateTime? NextReviewDate(string book) {
        var mastered=MasteredWords(book);var schedules=Schedules(book);var dates=new List<DateTime>();
        foreach(var word in Book(book)){ReviewSchedule schedule;DateTime date;if(mastered.Contains(word.Text)&&schedules.TryGetValue(word.Text,out schedule)&&ScheduleDate(schedule,out date))dates.Add(date);}
        return dates.Count==0?(DateTime?)null:dates.Min();
    }
    public bool StartSpacedReview(string book,string title) {
        var words=DueWords(book);if(words.Count==0)return false;
        Start(words,title+" · 到期复习","due:"+book,true);Data.Round.IsSpacedReview=true;Data.Round.SourceBookKey=book;return true;
    }
}
}
