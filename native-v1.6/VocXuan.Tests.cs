using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
namespace ShiCi {
public static class Tests {
    static int passed;
    static void Check(bool value,string label){if(!value)throw new Exception("FAILED: "+label);passed++;}
    public static int Run(string folder) {
        Directory.CreateDirectory(folder);var log=new List<string>();
        try {
            var e=new Engine(Path.Combine(folder,"selftest.json"));e.LoadDictionary();Check(e.Dictionary.Count>10000,"dictionary loaded");
            foreach(string tag in new[]{"cet4","cet6","ky","ielts","toefl","gre","gk","common"})Check(e.Book(tag).Count>500,"book "+tag);
            Check(e.Dictionary["preserve"].Meaning.Contains("保存")||e.Dictionary["preserve"].Meaning.Contains("保护"),"Chinese definition");
            var list=new[]{"resilient","curiosity","ambiguous"}.Select(x=>e.Dictionary[x]).ToList();e.Start(list,"test");e.Rate(0);e.Rate(1);e.Rate(2);
            Check(e.Data.Round.Finished&&e.Data.Round.Answers.Count==3,"completion");Check(e.Data.Review.Count==2,"unknown and fuzzy saved");
            e.Undo();Check(!e.Data.Round.Finished&&e.Data.Round.Answers.Count==2&&e.Data.Review.Count==1,"undo completed round");
            e.Rate(1);var old=e.Data.Review["ambiguous"];e.Start(new List<Word>{list[2]},"repeat");e.Rate(2);e.Undo();Check(e.Data.Review["ambiguous"].Rating==old.Rating,"undo restores previous book rating");
            e.Data.Round.Seconds=13.5;e.Save();var next=new Engine(e.FilePath);next.Load();Check(next.Data.Round.Words.Count==1&&next.Data.Round.Answers.Count==0&&next.Data.Round.Seconds==13.5&&next.Data.Review.Count==2,"progress persistence");
            e.Save();File.WriteAllText(e.FilePath,"bad json");next.Load();Check(next.LoadWarning!=null&&next.Data.Review.Count==2,"backup recovery");
            int unknown,skipped;var imported=e.ParseImport("\uFEFFword\tmeaning\nresilient\nResilient\ncustomword\t自定义释义\nzzzzmissing\n123\n",out unknown,out skipped);
            Check(imported.Count==3&&unknown==1&&skipped==2,"import dedup and validation");Check(imported[0].Meaning.Length>0&&imported[1].Meaning=="自定义释义","import meanings");
            var importedCsv=e.ParseImport("hello,你好\nworld,世界\n",out unknown,out skipped);Check(importedCsv.Count==2&&importedCsv[0].Meaning=="你好","comma import");
            var y=e.ParseImport("单词,音标,状态,释义\n\"hello\",\"h\",\"模糊\",\"你好，世界\n第二行\"",out unknown,out skipped);Check(y.Count==1&&y[0].Meaning=="你好，世界\n第二行","export CSV roundtrip");
            e.Data.SelectedBook="ielts";e.Data.Columns=3;e.Save();next.Load();Check(next.Data.SelectedBook=="ielts"&&next.Data.Columns==3,"settings persist");
            double stamp=0;var timer=new ActivityTimer(()=>stamp);timer.Start(10);stamp=4.9;Check(Math.Abs(timer.Seconds-14.9)<0.001&&timer.Running,"timer below idle threshold");
            stamp=5.2;Check(Math.Abs(timer.Seconds-15)<0.001&&timer.IdlePaused&&!timer.Running,"idle stops exactly at five seconds");
            stamp=50;Check(Math.Abs(timer.Seconds-15)<0.001,"long idle excluded");timer.Activity();stamp=52;Check(Math.Abs(timer.Seconds-17)<0.001&&timer.Running,"activity resumes without idle time");
            timer.ToggleManual();stamp=100;timer.Activity();Check(timer.ManualPaused&&!timer.Running&&Math.Abs(timer.Seconds-17)<0.001,"manual pause survives mouse or keyboard");
            timer.ToggleManual();stamp=102;Check(Math.Abs(timer.Seconds-19)<0.001,"manual continue");timer.Stop();stamp=200;Check(Math.Abs(timer.Seconds-19)<0.001,"navigation time excluded");
            timer.Start(0);stamp=201;timer.Activity();stamp=205;timer.Activity();stamp=209;Check(Math.Abs(timer.Seconds-9)<0.001&&timer.Running,"each action extends idle boundary");
            stamp=212;Check(Math.Abs(timer.Seconds-10)<0.001&&timer.IdlePaused,"idle boundary follows last action");
            string legacyParent=Path.Combine(folder,"legacy-fixture");string legacy=Path.Combine(legacyParent,"识词客户端_v1.0","学习数据");Directory.CreateDirectory(legacy);File.WriteAllText(Path.Combine(legacy,"progress.json"),"{\"Version\":1,\"Review\":{},\"Imported\":[],\"Round\":null}");
            string migrated=Path.Combine(legacyParent,"VocXuan_v1.1","学习数据");Program.Migrate(migrated,legacyParent);var legacyEngine=new Engine(Path.Combine(migrated,"progress.json"));legacyEngine.Load();Check(legacyEngine.Data.Columns==2&&File.Exists(Path.Combine(legacy,"progress.json")),"legacy schema and copy-only migration");
            legacyEngine.Data.SelectedBook="gre";legacyEngine.Save();Program.Migrate(migrated,legacyParent);legacyEngine.Load();Check(legacyEngine.Data.SelectedBook=="gre","migration never overwrites new progress");
            var learning=new Engine(Path.Combine(folder,"learning.json"));learning.LoadDictionary();var cet=learning.Book("cet4").Take(4).ToList();learning.Start(cet,"大学英语四级","cet4");learning.Rate(1);learning.Data.Round.Seconds=8.25;
            var pending=learning.Data.Round;learning.Start(learning.Book("ielts").Take(3).ToList(),"雅思 IELTS","ielts");learning.Rate(2);learning.Save();
            var reloaded=new Engine(learning.FilePath);reloaded.Load();Check(reloaded.PendingRound("cet4").Answers.Count==1&&reloaded.PendingRound("cet4").Seconds==8.25,"per-book pending round survives switching and restart");Check(reloaded.PendingRound("ielts").Answers.Count==1,"other book keeps its own pending round");
            var plan=learning.PlanWords("cet4",4,false);Check(!plan.Take(3).Any(w=>w.Text==cet[0].Text),"unseen words planned before tested words");
            learning.ActivateRound(pending);learning.Undo();Check(!learning.TestedWords("cet4").Contains(cet[0].Text),"undo restores previously unseen word");learning.Rate(1);
            learning.Start(new List<Word>{cet[0]},"生词本","review",true);learning.Rate(0);Check(!learning.Data.Review.ContainsKey(cet[0].Text),"known in review removes word");learning.Undo();Check(learning.Data.Review[cet[0].Text].Rating==1&&!learning.Data.Round.Finished,"undo last review answer restores word");
            learning.Start(new List<Word>{cet[0]},"大学英语四级","cet4");learning.Rate(0);Check(!learning.Data.Review.ContainsKey(cet[0].Text),"ordinary known answer removes old review word");learning.Undo();Check(learning.TestedWords("cet4").Contains(cet[0].Text),"undo preserves coverage established in earlier round");
            learning.Start(new List<Word>{cet[0]},"生词本","review",true);learning.Rate(2);Check(learning.Data.Review[cet[0].Text].Rating==2,"fuzzy review answer remains in book");
            var mastery=new Engine(Path.Combine(folder,"mastery.json"));mastery.LoadDictionary();mastery.Data.Imported=list;mastery.Start(list,"我的词书","import");mastery.Rate(0);mastery.Rate(1);mastery.Rate(2);
            var totals=mastery.Progress("import");Check(totals.Total==3&&totals.Studied==3&&totals.Mastered==1&&totals.Remaining==2,"unique book counts and mastery");Check(mastery.PlanWords("import",20,false).Count==2&&!mastery.PlanWords("import",20,false).Any(w=>w.Text==list[0].Text),"known words excluded from future rounds");
            mastery.Start(new List<Word>{list[1]},"我的词书","import");mastery.Rate(1);Check(mastery.Progress("import").Studied==3,"repeated words counted once");
            mastery.Start(new List<Word>{list[1]},"生词本","review",true);mastery.Rate(0);Check(mastery.Progress("import").Mastered==2&&!mastery.Data.Review.ContainsKey(list[1].Text),"review recognition advances source book mastery");mastery.Save();var masteryReload=new Engine(mastery.FilePath);masteryReload.LoadDictionary();masteryReload.Load();Check(masteryReload.Progress("import").Mastered==2&&masteryReload.Data.SkipMastered,"mastery survives restart");masteryReload.Undo();Check(masteryReload.Progress("import").Mastered==1&&masteryReload.Data.Review.ContainsKey(list[1].Text),"undo after restart restores mastery and review");
            mastery.Data.SkipMastered=false;Check(mastery.PlanWords("import",20,false).Count==3,"optional retest includes known words");mastery.Start(new List<Word>{list[0]},"我的词书","import");mastery.Rate(2);Check(mastery.Progress("import").Mastered==1,"fuzzy retest revokes prior mastery");mastery.Undo();Check(mastery.Progress("import").Mastered==2,"undo retest restores prior mastery");
            mastery.Data.SkipMastered=true;mastery.Start(list,"我的词书","import");var bookRound=mastery.Data.Round;mastery.Start(new List<Word>{list[2]},"生词本","review",true);mastery.Rate(0);mastery.ActivateRound(bookRound);Check(bookRound.Finished&&bookRound.Words.Count==0,"continuing pending round prunes words mastered in review");
            Check(mastery.Progress("ielts").Studied==0&&mastery.Progress("ielts").Mastered==0,"unopened books have independent progress");mastery.Data.Imported=new List<Word>{list[2]};Check(mastery.Progress("import").Total==1&&mastery.Progress("import").Mastered==1,"import replacement counts only current words");
            var oldPath=Path.Combine(folder,"old-v3.json");var oldData=new SavedData {Version=3,Imported=list,Tested=new Dictionary<string,List<string>> { {"import",new List<string>{list[0].Text,list[1].Text}} },Review=new Dictionary<string,ReviewWord> { {list[1].Text,new ReviewWord{Word=list[1],Rating=1}} }};File.WriteAllText(oldPath,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(oldData));var migratedMastery=new Engine(oldPath);migratedMastery.LoadDictionary();migratedMastery.Load();Check(migratedMastery.Progress("import").Studied==2&&migratedMastery.Progress("import").Mastered==1,"v3 migration keeps coverage and reconstructs known words");migratedMastery.Save();migratedMastery.Load();Check(migratedMastery.Progress("import").Mastered==1,"migration runs once");
            Check(Phonetics.Normalize("wә:ld")=="wɜːld"&&Phonetics.Display("/ә'baut/")=="/ əˈbaut /","legacy schwa length stress and brackets normalized");Check(Phonetics.Normalize("b\\\\:d")==""&&Phonetics.Normalize("b?rd")=="","corrupt phonetics suppressed");Check(Phonetics.Normalize(Phonetics.Normalize(".infә'meiʃәn"))==Phonetics.Normalize(".infә'meiʃәn"),"phonetic normalization idempotent");Check(mastery.Dictionary.Values.All(w=>!System.Text.RegularExpressions.Regex.IsMatch(w.Phonetic,@"[\\?@\uFFFD\u04D9\u0454^]")),"entire bundled dictionary passes encoding audit");
            FeatureTests.Run(e,folder,Check);
            DailyTests.Run(e,folder,Check);
            log.Add("PASS: "+passed+" checks. Dictionary: "+e.Dictionary.Count+" entries.");
            foreach(string tag in new[]{"cet4","cet6","ky","ielts","toefl","gre","gk","common"})log.Add(tag+": "+e.Book(tag).Count);
            File.WriteAllLines(Path.Combine(folder,"test-results.txt"),log,Encoding.UTF8);return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"test-results.txt"),ex.ToString());return 1;}
    }
}
}
