using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ShiCi {
public class Word {
    public string Text { get; set; }
    public string Phonetic { get; set; }
    public string Meaning { get; set; }
    public string Tags { get; set; }
    public int Rank { get; set; }
}
public class ReviewWord {
    public Word Word { get; set; }
    public int Rating { get; set; }
    public string Added { get; set; }
}
public class Answer {
    public string StudyDay { get; set; }
    public Word Word { get; set; }
    public int Rating { get; set; }
    public ReviewWord Previous { get; set; }
    public bool PreviouslyTested { get; set; }
    public Dictionary<string, bool> MasteryBefore { get; set; }
    public Dictionary<string, ReviewSchedule> ScheduleBefore { get; set; }
}
public class Round {
    public List<Word> Words { get; set; }
    public List<Answer> Answers { get; set; }
    public string Book { get; set; }
    public double Seconds { get; set; }
    public bool Finished { get; set; }
    public string BookKey { get; set; }
    public bool IsReview { get; set; }
    public bool IsSpacedReview { get; set; }
    public string SourceBookKey { get; set; }
    public Round() { Words = new List<Word>(); Answers = new List<Answer>(); }
}
public class SavedData {
    public int Version { get; set; }
    public Round Round { get; set; }
    public Dictionary<string, ReviewWord> Review { get; set; }
    public List<Word> Imported { get; set; }
    public string SelectedBook { get; set; }
    public int Columns { get; set; }
    public Dictionary<string, Round> Rounds { get; set; }
    public Dictionary<string, List<string>> Tested { get; set; }
    public Dictionary<string, List<string>> Mastered { get; set; }
    public bool SkipMastered { get; set; }
    public Dictionary<string, Dictionary<string, ReviewSchedule>> Spaced { get; set; }
    public Dictionary<string, DailyStudy> Daily { get; set; }
    public string DailyStartedOn { get; set; }
    public SavedData() { Columns = 2; Version = 6; SkipMastered = true; Review = new Dictionary<string, ReviewWord>(StringComparer.OrdinalIgnoreCase); Imported = new List<Word>(); Rounds = new Dictionary<string, Round>(); Tested = new Dictionary<string, List<string>>(); Mastered = new Dictionary<string, List<string>>(); Spaced = new Dictionary<string, Dictionary<string, ReviewSchedule>>(); Daily = new Dictionary<string, DailyStudy>(); }
}
public class BookProgress {
    public int Total, Studied, Mastered;
    public int Remaining { get { return Total - Mastered; } }
}
public partial class Engine {
    public readonly Dictionary<string, Word> Dictionary = new Dictionary<string, Word>(StringComparer.OrdinalIgnoreCase);
    public SavedData Data = new SavedData();
    public string FilePath;
    public string LoadWarning;
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 128000000, RecursionLimit = 100 };
    readonly Func<DateTime> now;
    public Engine(string path, Func<DateTime> currentTime = null) { FilePath = path; now = currentTime ?? (() => DateTime.Now); InitializeDaily(); }
    public void LoadDictionary() {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("dictionary.gz"))
        using (var zip = new GZipStream(stream, CompressionMode.Decompress))
        using (var read = new StreamReader(zip, Encoding.UTF8)) {
            string line;
            while ((line = read.ReadLine()) != null) {
                var p = line.Split('\t');
                if (p.Length < 5) continue;
                Dictionary[p[0]] = new Word { Text = p[0], Phonetic = Phonetics.Normalize(p[1]), Meaning = p[2].Replace("\\n", "\n"), Tags = p[3], Rank = int.Parse(p[4]) };
            }
        }
    }
    public void Load() {
        if (!File.Exists(FilePath)) return;
        try { LoadFile(FilePath); }
        catch {
            try { LoadFile(FilePath + ".bak"); LoadWarning = "上次的数据文件未能读取，已恢复备份。"; }
            catch { LoadWarning = "上次的数据未能读取；原文件已保留。本次新进度会保存为恢复文件。"; FilePath = FilePath + ".recovered.json"; }
        }
    }
    void LoadFile(string path) {
        var d = json.Deserialize<SavedData>(File.ReadAllText(path, Encoding.UTF8));
        if (d == null || d.Review == null || d.Imported == null) throw new InvalidDataException();
        d.Review = new Dictionary<string, ReviewWord>(d.Review, StringComparer.OrdinalIgnoreCase);
        if (d.Round != null && (d.Round.Words == null || d.Round.Answers == null || d.Round.Answers.Count > d.Round.Words.Count || d.Round.Seconds < 0)) throw new InvalidDataException();
        if (d.Columns != 2 && d.Columns != 3) d.Columns = 2;
        Data = d;
        if (Data.Rounds == null) Data.Rounds = new Dictionary<string, Round>();
        if (Data.Tested == null) Data.Tested = new Dictionary<string, List<string>>();
        if (Data.Mastered == null) Data.Mastered = new Dictionary<string, List<string>>();
        foreach (var pair in Data.Rounds.ToList()) {
            var r = pair.Value;
            if (r == null || r.Words == null || r.Answers == null || r.Answers.Count > r.Words.Count) { Data.Rounds.Remove(pair.Key); continue; }
            if (string.IsNullOrEmpty(r.BookKey)) r.BookKey = pair.Key;
            if (r.BookKey == "review") r.IsReview = true;
        }
        if (Data.Round != null) {
            if (string.IsNullOrEmpty(Data.Round.BookKey)) Data.Round.BookKey = InferKey(Data.Round.Book);
            if (Data.Round.BookKey == "review") Data.Round.IsReview = true;
            ArchiveRound();
            if (!Data.Round.IsReview) { var seen = TestedWords(Data.Round.BookKey); foreach (var answer in Data.Round.Answers) seen.Add(answer.Word.Text); Data.Tested[Data.Round.BookKey] = seen.ToList(); }
        }
        foreach (var r in Data.Rounds.Values.Where(x => !x.IsReview)) {
            var seen = TestedWords(r.BookKey); foreach (var a in r.Answers) seen.Add(a.Word.Text); Data.Tested[r.BookKey] = seen.ToList();
        }
        if (Data.Version < 4) {
            foreach (var pair in Data.Tested) Data.Mastered[pair.Key] = (pair.Value ?? new List<string>()).Where(w => !Data.Review.ContainsKey(w)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var r in Data.Rounds.Values.Where(x => !x.IsReview)) foreach (var a in r.Answers) MarkMastered(r.BookKey, a.Word.Text, a.Rating == 0);
        }
        RefreshPhonetics(); InitializeSchedules(); InitializeDaily(); Data.Version = 6;
    }
    public void Save() {
        ArchiveRound();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json.Serialize(Data), new UTF8Encoding(false));
        if (File.Exists(FilePath)) File.Replace(tmp, FilePath, FilePath + ".bak", true);
        else File.Move(tmp, FilePath);
    }
    public List<Word> Book(string tag) {
        if (tag == "import") return Data.Imported.ToList();
        if (tag == "review") return Data.Review.Values.OrderBy(x => x.Added).Select(x => x.Word).ToList();
        if (tag == "common") return Dictionary.Values.Where(x => !x.Text.Contains(" ")).OrderBy(x => x.Rank).ThenBy(x => x.Text).Take(3000).ToList();
        return Dictionary.Values.Where(x => (" " + x.Tags + " ").Contains(" " + tag + " ")).OrderBy(x => x.Text, StringComparer.OrdinalIgnoreCase).ToList();
    }
    static readonly Random random = new Random();
    public static string InferKey(string name) {
        if (string.IsNullOrEmpty(name)) return "legacy";
        if (name.Contains("生词")) return "review";
        string[][] names = { new[]{"cet4","大学英语四级"},new[]{"cet6","大学英语六级"},new[]{"ky","考研英语"},new[]{"ielts","雅思 IELTS"},new[]{"toefl","托福 TOEFL"},new[]{"gre","GRE"},new[]{"gk","高中英语"},new[]{"common","常用 3000 词"} };
        var pair = names.FirstOrDefault(x => x[1] == name);
        if (pair != null) return pair[0];
        if (name.Contains("导入") || name == "我的词书") return "import";
        return name;
    }
    public void ArchiveRound() { var r = Data.Round; if (r != null && !string.IsNullOrEmpty(r.BookKey)) Data.Rounds[r.BookKey] = r; }
    public Round SavedRound(string key) { if (Data.Round != null && Data.Round.BookKey == key) return Data.Round; Round r; return Data.Rounds.TryGetValue(key, out r) ? r : null; }
    public Round PendingRound(string key) { var r = SavedRound(key); return r != null && !r.Finished && r.Words.Count > r.Answers.Count ? r : null; }
    public void ActivateRound(Round r) { ArchiveRound(); Data.Round = r; if (!r.IsReview && !r.Finished && Data.SkipMastered) { var mastered = MasteredWords(r.BookKey); r.Words = r.Words.Take(r.Answers.Count).Concat(r.Words.Skip(r.Answers.Count).Where(w => !mastered.Contains(w.Text))).ToList(); r.Finished = r.Words.Count == r.Answers.Count; } ArchiveRound(); }
    public HashSet<string> TestedWords(string key) { List<string> words; return new HashSet<string>(Data.Tested.TryGetValue(key, out words) && words != null ? words : new List<string>(), StringComparer.OrdinalIgnoreCase); }
    public HashSet<string> MasteredWords(string key) { List<string> words; return new HashSet<string>(Data.Mastered.TryGetValue(key, out words) && words != null ? words : new List<string>(), StringComparer.OrdinalIgnoreCase); }
    void MarkMastered(string key, string word, bool value) { var words = MasteredWords(key); if (value) words.Add(word); else words.Remove(word); Data.Mastered[key] = words.ToList(); }
    public BookProgress Progress(string key) { var all = Book(key).Select(w => w.Text).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); var studied = TestedWords(key); var mastered = MasteredWords(key); return new BookProgress { Total = all.Count, Studied = all.Count(studied.Contains), Mastered = all.Count(mastered.Contains) }; }
    public void RefreshPhonetics() {
        Action<Word> refresh = w => { if (w == null) return; Word latest; w.Phonetic = Dictionary.TryGetValue(w.Text, out latest) ? latest.Phonetic : Phonetics.Normalize(w.Phonetic); };
        foreach (var w in Data.Imported) refresh(w);
        foreach (var item in Data.Review.Values) refresh(item.Word);
        foreach (var r in Data.Rounds.Values) { foreach (var w in r.Words) refresh(w); foreach (var a in r.Answers) { refresh(a.Word); if (a.Previous != null) refresh(a.Previous.Word); } }
    }
    void MarkTested(string key, string word, bool value) { var words = TestedWords(key); if (value) words.Add(word); else words.Remove(word); Data.Tested[key] = words.ToList(); }
    static void Shuffle<T>(List<T> words) { for (int i = words.Count - 1; i > 0; i--) { int j = random.Next(i + 1); T w = words[i]; words[i] = words[j]; words[j] = w; } }
    public List<Word> PlanWords(string key, int count, bool shuffle) {
        var mastered = MasteredWords(key); var all = Book(key).Where(w => !Data.SkipMastered || !mastered.Contains(w.Text)).ToList(); var tested = TestedWords(key);
        var fresh = all.Where(w => !tested.Contains(w.Text)).ToList();
        var previous = all.Where(w => tested.Contains(w.Text)).ToList();
        if (shuffle) { Shuffle(fresh); Shuffle(previous); }
        return fresh.Concat(previous).Take(Math.Max(0, count)).ToList();
    }
    public void Start(List<Word> words, string book, string key = null, bool review = false) {
        ArchiveRound(); key = key ?? InferKey(book);
        Data.Round = new Round { Words = words, Book = book, BookKey = key, IsReview = review || key == "review" }; ArchiveRound();
    }
    public void Rate(int rating) {
        var r = Data.Round;
        if (r == null || r.Finished || r.Answers.Count >= r.Words.Count || rating < 0 || rating > 2) return;
        var w = r.Words[r.Answers.Count];
        ReviewWord previous;
        Data.Review.TryGetValue(w.Text, out previous);
        bool seen = !r.IsReview && TestedWords(r.BookKey).Contains(w.Text);
        var touched = r.IsSpacedReview && !string.IsNullOrEmpty(r.SourceBookKey) ? new List<string> { r.SourceBookKey } : r.IsReview ? Data.Tested.Keys.Where(k => TestedWords(k).Contains(w.Text)).ToList() : new List<string> { r.BookKey };
        var before = touched.ToDictionary(k => k, k => MasteredWords(k).Contains(w.Text));
        var scheduleBefore = touched.ToDictionary(k => k, k => ScheduleSnapshot(k, w.Text));
        r.Answers.Add(new Answer { Word = w, Rating = rating, Previous = previous, PreviouslyTested = seen, MasteryBefore = before, ScheduleBefore = scheduleBefore, StudyDay = RecordStudyAnswer(w.Text) });
        if (rating != 0) Data.Review[w.Text] = new ReviewWord { Word = w, Rating = rating, Added = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
        else Data.Review.Remove(w.Text);
        if (!r.IsReview) MarkTested(r.BookKey, w.Text, true);
        foreach (var key in touched) { MarkMastered(key, w.Text, rating == 0); UpdateSchedule(key, w.Text, rating, before[key], r.IsSpacedReview); }
        if (r.Answers.Count == r.Words.Count) r.Finished = true;
    }
    public void Undo() {
        var r = Data.Round;
        if (r == null || r.Answers.Count == 0) return;
        var a = r.Answers[r.Answers.Count - 1];
        UndoStudyAnswer(a);
        if (a.Previous == null) Data.Review.Remove(a.Word.Text);
        else Data.Review[a.Word.Text] = a.Previous;
        if (!r.IsReview) MarkTested(r.BookKey, a.Word.Text, a.PreviouslyTested);
        if (a.MasteryBefore != null) foreach (var item in a.MasteryBefore) MarkMastered(item.Key, a.Word.Text, item.Value);
        else if (!r.IsReview) MarkMastered(r.BookKey, a.Word.Text, a.PreviouslyTested && a.Previous == null);
        if (a.ScheduleBefore != null) foreach (var item in a.ScheduleBefore) RestoreSchedule(item.Key, a.Word.Text, item.Value);
        else if (a.MasteryBefore != null) foreach (var item in a.MasteryBefore) { if (!item.Value) RestoreSchedule(item.Key, a.Word.Text, null); }
        else if (!r.IsReview && !MasteredWords(r.BookKey).Contains(a.Word.Text)) RestoreSchedule(r.BookKey, a.Word.Text, null);
        r.Answers.RemoveAt(r.Answers.Count - 1); r.Finished = false;
    }
    public static List<List<string>> ReadRows(string text) {
        // Preserve quoted CSV definitions, including line breaks and doubled quotes.
        char delimiter = text.IndexOf('\t') >= 0 ? '\t' : text.IndexOf(',') >= 0 ? ',' : text.IndexOf('，') >= 0 ? '，' : '\t';
        var rows = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); bool quote = false;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == '"' && (quote || field.ToString().Trim().Length == 0)) {
                if (quote && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else quote = !quote;
            }
            else if (c == delimiter && !quote) { row.Add(field.ToString().Trim()); field.Clear(); }
            else if (c == '\n' && !quote) { row.Add(field.ToString().Trim()); field.Clear(); rows.Add(row); row = new List<string>(); }
            else if (c != '\r') field.Append(c);
        }
        if (quote) throw new InvalidDataException("CSV 引号未闭合，请检查导入文件。");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString().Trim()); rows.Add(row); }
        return rows;
    }
    public List<Word> ParseImport(string text, out int unknown, out int skipped) {
        var words = new List<Word>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); unknown = 0; skipped = 0;
        int meaningColumn = 1;
        foreach (var fields in ReadRows(text.TrimStart('\uFEFF'))) {
            var name = fields[0];
            if (name.Length == 0 || name.StartsWith("#")) continue;
            if (name.Equals("word", StringComparison.OrdinalIgnoreCase) || name == "单词") {
                int header = fields.FindIndex(x => x == "释义" || x.Equals("meaning", StringComparison.OrdinalIgnoreCase) || x.Equals("translation", StringComparison.OrdinalIgnoreCase));
                if (header >= 0) { meaningColumn = header; continue; }
            }
            string meaning = fields.Count > meaningColumn ? fields[meaningColumn] : "";
            if (!Regex.IsMatch(name, @"^[a-zA-Z][a-zA-Z' -]{0,79}$") || !seen.Add(name)) { skipped++; continue; }
            Word found; Dictionary.TryGetValue(name, out found);
            if (found == null && meaning.Length == 0) unknown++;
            words.Add(new Word { Text = name, Phonetic = found == null ? "" : found.Phonetic, Meaning = meaning.Length > 0 ? meaning : found == null ? "词典未收录此词。可在导入文件中为它补充释义。" : found.Meaning, Tags = "import", Rank = 999999 });
        }
        return words;
    }
}


}
