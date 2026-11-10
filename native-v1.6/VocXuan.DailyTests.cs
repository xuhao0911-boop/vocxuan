using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace ShiCi {
public static class DailyTests {
    public static void Run(Engine dictionary,string folder,Action<bool,string> check) {
        DateTime date=new DateTime(2026,11,4,23,59,58);
        var words=new[]{"world","bird","preserve"}.Select(k=>dictionary.Dictionary[k]).ToList();
        var e=new Engine(Path.Combine(folder,"daily-desktop.json"),()=>date);e.Data.Imported=words;
        check(e.Data.Daily.Count==0&&e.StudyStatistics(e.TodayKey).Words==0,"daily starts empty");
        e.Start(words,"我的词书","import");e.Rate(-1);check(e.StudyStatistics(e.TodayKey).Words==0,"invalid rating does not count");
        e.Rate(0);e.Rate(1);e.Rate(2);var summary=e.StudyStatistics(e.TodayKey);
        check(summary.Words==3&&summary.Answers==3,"all three ratings count daily studied words");
        e.Rate(0);check(e.StudyStatistics(e.TodayKey).Answers==3,"finished round cannot inflate daily count");
        e.Start(new List<Word>{words[0]},"生词本","review",true);e.Rate(2);
        check(e.StudyStatistics(e.TodayKey).Words==3&&e.StudyStatistics(e.TodayKey).Answers==4,"repeated word across books counts once");
        e.Undo();check(e.StudyStatistics(e.TodayKey).Words==3&&e.StudyStatistics(e.TodayKey).Answers==3,"undo repeat preserves earlier occurrence");
        e.Start(new List<Word>{new Word{Text="WORLD",Meaning="世界"}},"自定义","other");e.Rate(0);
        check(e.StudyStatistics(e.TodayKey).Words==3,"daily word identity ignores case");e.Undo();
        e.AddStudyTime(date,5.25);
        check(Math.Abs(e.StudyStatistics("2026-11-04").Seconds-2)<0.0001&&Math.Abs(e.StudyStatistics("2026-11-05").Seconds-3.25)<0.0001,"daily duration splits midnight precisely");
        e.AddStudyTime(date,-1);e.AddStudyTime(date,double.NaN);e.AddStudyTime(date,double.PositiveInfinity);
        check(Math.Abs(e.StudyStatistics("2026-11-04").Seconds-2)<0.0001,"invalid duration ignored");
        e.Start(new List<Word>{words[2]},"我的词书","import");e.Rate(2);e.Save();
        date=date.AddMinutes(1);var reloaded=new Engine(e.FilePath,()=>date);reloaded.LoadDictionary();reloaded.Load();reloaded.Undo();
        check(reloaded.StudyStatistics("2026-11-04").Words==3&&reloaded.StudyStatistics("2026-11-04").Answers==3&&reloaded.StudyStatistics("2026-11-05").Words==0,"persisted undo after midnight updates original date only");
        check(Math.Abs(reloaded.StudyStatistics("2026-11-05").Seconds-3.25)<0.0001,"undo preserves time already spent");
        reloaded.Start(new List<Word>{words[0]},"生词本","review",true);reloaded.Rate(0);
        check(reloaded.StudyStatistics("2026-11-05").Words==1&&reloaded.StudyStatistics("2026-11-04").Words==3,"same word on another day counts again");
        check(reloaded.StudyHistory()[0].Date=="2026-11-05"&&reloaded.Data.DailyStartedOn=="2026-11-04","history sorts and retains tracking start");reloaded.Save();
        var old=new SavedData{Version=5,Imported=words,Round=new Round{Book="我的词书",BookKey="import",Words=words,Answers=new List<Answer>{new Answer{Word=words[0],Rating=1}},Seconds=5400},Daily=null,DailyStartedOn=null};
        string legacy=Path.Combine(folder,"daily-legacy.json");File.WriteAllText(legacy,new JavaScriptSerializer().Serialize(old));
        var migrated=new Engine(legacy,()=>date);migrated.Load();
        check(migrated.Data.Round.Seconds==5400&&migrated.Data.Daily.Count==0&&migrated.StudyStatistics(migrated.TodayKey).Words==0,"legacy progress preserved without fabricating dated history");
        migrated.Start(new List<Word>{words[1]},"生词本","review",true);migrated.Rate(0);migrated.ActivateRound(migrated.SavedRound("import"));migrated.Undo();
        check(migrated.StudyStatistics(migrated.TodayKey).Words==1,"undo legacy answer cannot deduct new statistics");
        double stamp=0;var clockEngine=new Engine(Path.Combine(folder,"daily-clock.json"),()=>new DateTime(2026,11,4));
        DateTime origin=new DateTime(2026,11,4,23,59,58);var timer=new ActivityTimer(()=>stamp,()=>origin.AddSeconds(stamp));timer.OnElapsed=clockEngine.AddStudyTime;
        timer.Start(100);stamp=100;double seconds=timer.Seconds;
        check(seconds==105&&clockEngine.StudyStatistics("2026-11-04").Seconds==2&&clockEngine.StudyStatistics("2026-11-05").Seconds==3,"late wake counts first five seconds on actual dates");
        seconds=timer.Seconds;check(clockEngine.StudyStatistics("2026-11-05").Seconds==3,"repeated timer reads never double count");
        timer.Activity();stamp=102;timer.ToggleManual();stamp=150;timer.Activity();seconds=timer.Seconds;
        check(seconds==107&&clockEngine.StudyStatistics("2026-11-05").Seconds==5,"manual pause and idle excluded from daily duration");
        timer.ToggleManual();stamp=151;timer.Stop();stamp=160;seconds=timer.Seconds;
        check(seconds==108&&clockEngine.StudyStatistics("2026-11-05").Seconds==6,"navigation or background stop excludes elapsed time");
        timer.Resume();stamp=161;seconds=timer.Seconds;
        check(seconds==109&&clockEngine.StudyStatistics("2026-11-05").Seconds==7,"resume does not add old round baseline twice");
        reloaded.Save();
        string mobile=Path.Combine(folder,"daily-mobile.json");
        if(File.Exists(mobile)) {
            var fromMobile=new Engine(mobile,()=>new DateTime(2026,11,5));fromMobile.Load();
            check(fromMobile.StudyStatistics("2026-11-04").Words==3&&fromMobile.StudyStatistics("2026-11-05").Words==1,"Android daily schema loads on desktop");
            fromMobile.Undo();check(fromMobile.StudyStatistics("2026-11-05").Words==0,"Android answer date supports desktop undo");
        }
    }
}
}
