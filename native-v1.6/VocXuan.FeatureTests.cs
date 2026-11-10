using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using System.Web.Script.Serialization;
namespace ShiCi {
public static class FeatureTests {
    public static void Run(Engine dictionary,string folder,Action<bool,string> check) {
        DateTime date=new DateTime(2026,10,31,23,58,0);
        var words=dictionary.Book("cet4").Take(13).ToList();
        var e=new Engine(Path.Combine(folder,"spaced.json"),()=>date);e.LoadDictionary();e.Data.Imported=words;
        e.Start(words.Take(12).ToList(),"我的词书","import");foreach(var w in e.Data.Round.Words)e.Rate(0);
        check(e.Data.Spaced["import"].Count==12&&e.DueWords("import").Count==0,"first recognition schedules future review without immediate retest");
        check(e.Data.Spaced["import"][words[0].Text].DueDate=="2026-11-02","two-day interval crosses month using local calendar");
        date=date.AddDays(1);check(e.DueWords("import").Count==0,"tomorrow is too early");date=date.AddDays(1);check(e.DueWords("import",50000).Count==12&&e.DueWords("import").Count==10,"due boundary and ten-word batch limit");
        e.Start(new List<Word>{words[12]},"我的词书","import");e.Data.Round.Seconds=13;var pending=e.Data.Round;
        check(e.StartSpacedReview("import","我的词书")&&e.Data.Round.IsSpacedReview&&e.Data.Round.SourceBookKey=="import","due review stores source book");
        check(e.PendingRound("import")==pending&&pending.Seconds==13,"due review preserves independent normal round");
        var reviewed=e.Data.Round.Words[0];e.Rate(0);
        check(e.Progress("import").Mastered==12&&e.Progress("import").Studied==12,"successful retest does not inflate progress");
        check(e.Data.Spaced["import"][reviewed.Text].Stage==1&&e.Data.Spaced["import"][reviewed.Text].DueDate=="2026-11-09","successful due recall schedules seven days later");
        e.Undo();check(e.Data.Spaced["import"][reviewed.Text].Stage==0&&e.DueWords("import",50000).Count==12,"undo restores exact old due date");
        e.Data.Mastered["other-book"]=new List<string>{reviewed.Text};e.Data.Tested["other-book"]=new List<string>{reviewed.Text};e.InitializeSchedules();var otherDue=e.Data.Spaced["other-book"][reviewed.Text].DueDate;e.Rate(1);
        check(e.Progress("import").Mastered==11&&e.Progress("import").Remaining==2&&e.Data.Review.ContainsKey(reviewed.Text),"forgotten word returns to pending and review list");
        check(!e.Data.Spaced["import"].ContainsKey(reviewed.Text)&&e.MasteredWords("other-book").Contains(reviewed.Text)&&e.Data.Spaced["other-book"][reviewed.Text].DueDate==otherDue,"source-specific retest leaves another book schedule unchanged");
        e.Save();var restored=new Engine(e.FilePath,()=>date);restored.LoadDictionary();restored.Load();
        check(restored.PendingRound("due:import")!=null&&restored.PendingRound("due:import").IsSpacedReview&&restored.PendingRound("import").Seconds==13,"normal and due rounds both survive restart");
        restored.Undo();check(restored.Progress("import").Mastered==12&&restored.Data.Spaced["import"][reviewed.Text].DueDate=="2026-11-02"&&!restored.Data.Review.ContainsKey(reviewed.Text),"persisted undo restores mastery schedule and review list");
        restored.Start(new List<Word>{reviewed},"我的词书","import");restored.Rate(0);check(restored.Data.Spaced["import"][reviewed.Text].DueDate=="2026-11-02","ordinary immediate repeat does not postpone overdue review");
        var single=new Engine(Path.Combine(folder,"single-schedule.json"),()=>date);single.Data.Imported=new List<Word>{words[0]};single.Start(single.Data.Imported,"我的词书","import");single.Rate(0);
        foreach(int days in new[]{7,14,30,30}) {
            date=DateTime.ParseExact(single.Data.Spaced["import"][words[0].Text].DueDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);
            check(single.StartSpacedReview("import","我的词书"),"due interval becomes eligible");single.Rate(0);
            check(single.Data.Spaced["import"][words[0].Text].DueDate==date.AddDays(days).ToString("yyyy-MM-dd"),"bounded spaced interval "+days);
        }
        date=date.AddDays(30);single.StartSpacedReview("import","我的词书");single.Rate(2);check(single.Progress("import").Mastered==0&&single.Data.Spaced["import"].Count==0&&single.Data.Review[words[0].Text].Rating==2,"fuzzy recall resets mastery and schedule");
        single.Start(single.Data.Imported,"我的词书","import");single.Rate(0);check(single.Data.Spaced["import"][words[0].Text].Stage==0&&single.Data.Spaced["import"][words[0].Text].DueDate==date.AddDays(2).ToString("yyyy-MM-dd"),"relearning restarts at two days");
        var old=new SavedData {Version=4,Imported=new List<Word>{words[0]},Mastered=new Dictionary<string,List<string>>{{"import",new List<string>{words[0].Text}}},Tested=new Dictionary<string,List<string>>{{"import",new List<string>{words[0].Text}}},Spaced=null};
        var path=Path.Combine(folder,"old-v14.json");File.WriteAllText(path,new JavaScriptSerializer().Serialize(old));var migrated=new Engine(path,()=>date);migrated.LoadDictionary();migrated.Load();var seeded=migrated.Data.Spaced["import"][words[0].Text].DueDate;
        check(migrated.Data.Version==6&&migrated.Progress("import").Mastered==1&&migrated.DueWords("import").Count==0,"v14 migration keeps mastery and delays initial review");migrated.Save();date=date.AddDays(1);migrated.Load();check(migrated.Data.Spaced["import"][words[0].Text].DueDate==seeded,"restart does not reset scheduled date");date=date.AddDays(1);migrated.Load();check(migrated.DueWords("import").Count==1,"migrated known words become due after two days");
        migrated.Data.Imported=new List<Word>();check(migrated.DueWords("import").Count==0,"removed imported words are excluded from review");
        string audio=Path.Combine(folder,"offline-pronunciation.wav");if(File.Exists(audio))File.Delete(audio);
        using(var speech=new WordSpeaker(audio)) {
            check(!File.Exists(audio)&&speech.LastWord==null,"speech is lazy and never autoplays");speech.Speak("world");Thread.Sleep(200);
            DateTime deadline=DateTime.UtcNow.AddSeconds(8);while(speech.Speaking&&DateTime.UtcNow<deadline)Thread.Sleep(30);
            check(!speech.Speaking&&speech.LastWord=="world"&&!string.IsNullOrEmpty(speech.VoiceName),"offline English speech finishes asynchronously");
            File.WriteAllText(Path.Combine(folder,"voice.txt"),speech.VoiceName);speech.Stop();
        }
        var wave=File.ReadAllBytes(audio);check(wave.Length>1000&&System.Text.Encoding.ASCII.GetString(wave,0,4)=="RIFF"&&wave.Skip(100).Any(b=>b!=0),"English pronunciation generated nonempty audio");
    }
}
}
