using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ShiCi {
public partial class ReaderWindow {
    TextBlock dailyWords, dailyTime;
    string homeStatsDay;
    Border DailyHomeCard() {
        var content=Stack();
        var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var title=Text("今日学习",13,Muted);title.VerticalAlignment=VerticalAlignment.Center;Place(heading,title);
        var history=Btn("历史记录",ShowDaily);history.Name="DailyHistory";history.FontSize=12;history.Padding=new Thickness(10,4,10,4);Place(heading,history,0,1);content.Children.Add(heading);
        var values=new Grid {Margin=new Thickness(0,8,0,0)};values.ColumnDefinitions.Add(new ColumnDefinition());values.ColumnDefinitions.Add(new ColumnDefinition());
        dailyWords=Text("",23,Green,true);dailyWords.Name="DailyWords";dailyTime=Text("",23,Ink,true);dailyTime.Name="DailyTime";
        Place(values,dailyWords);Place(values,dailyTime,0,1);content.Children.Add(values);
        var captions=new Grid();captions.ColumnDefinitions.Add(new ColumnDefinition());captions.ColumnDefinitions.Add(new ColumnDefinition());
        Place(captions,Text("学习词数 · 当日去重",11,Muted));Place(captions,Text("有效背词时长",11,Muted),0,1);content.Children.Add(captions);
        var card=Card(content,new Thickness(17,12,17,14));card.Margin=new Thickness(0,20,0,0);return card;
    }
    void UpdateDailyHome() {
        homeStatsDay=engine.TodayKey;if(dailyWords==null)return;
        var stats=engine.StudyStatistics(homeStatsDay);dailyWords.Text=stats.Words+" 词";dailyTime.Text=Duration(stats.Seconds);
    }
    Grid DailyRow(string date,string words,string duration,bool header) {
        var row=new Grid {Margin=new Thickness(0,header?0:10,0,header?10:10)};
        row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1.35,GridUnitType.Star)});
        row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition());
        Place(row,Text(date,header?12:15,header?Muted:Ink,!header));
        Place(row,Text(words,header?12:15,header?Muted:Green),0,1);
        Place(row,Text(duration,header?12:15,header?Muted:Ink),0,2);return row;
    }
    void ShowDaily() {
        Halt();Save();Page("daily");
        var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        var heading=Stack();heading.Children.Add(Text("每日学习记录",25,Ink,true));
        var note=Text("新学和复习都计入，同日同词跨词书只算一个；撤销回退词数，已用时间保留。",13,Muted);note.Margin=new Thickness(0,8,0,16);heading.Children.Add(note);Place(layout,heading);
        Place(layout,DailyRow("日期","学习词数","有效时长",true),1);
        var rows=new StackPanel();
        foreach(var stats in engine.StudyHistory()) {
            var row=DailyRow(stats.Date==engine.TodayKey?stats.Date+"  今天":stats.Date,stats.Words+" 词",Duration(stats.Seconds),false);
            row.ToolTip="作答 "+stats.Answers+" 次；同词重复作答只计入 1 个学习词数。";
            rows.Children.Add(new Border {Child=row,BorderBrush=Brush(Line),BorderThickness=new Thickness(0,0,0,1)});
        }
        Place(layout,new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},2);
        var footer=Stack(18);footer.Children.Add(Text("从 "+engine.Data.DailyStartedOn+" 起记录。旧版存档缺少每日明细，不补算历史时长。",12,Muted));
        var back=Btn("返回首页",Home);back.Margin=new Thickness(0,12,0,0);back.HorizontalAlignment=HorizontalAlignment.Right;footer.Children.Add(back);Place(layout,footer,3);body.Children.Add(layout);
    }
    async Task QaDaily() {
        Halt();engine.Data.Daily.Clear();engine.Data.DailyStartedOn=engine.TodayKey;
        engine.Data.Round=null;engine.Data.Rounds.Clear();engine.Data.Imported=new[]{"world","bird","preserve"}.Select(k=>engine.Dictionary[k]).ToList();engine.Data.SelectedBook="import";
        Home();Check(dailyWords.Text=="0 词"&&dailyTime.Text=="00:00","daily home begins with zero without backfill");
        engine.Start(engine.Data.Imported,"我的词书","import");Begin();await Task.Delay(350);
        lastRate=DateTime.MinValue;Rate(1);lastRate=DateTime.MinValue;Rate(2);Home();
        Check(dailyWords.Text=="2 词"&&engine.StudyStatistics(engine.TodayKey).Seconds>=0.3,"home shows daily count and foreground duration");
        Click("继续");Click("撤销");Home();Check(dailyWords.Text=="1 词","UI undo decrements daily unique count");
        double before=engine.StudyStatistics(engine.TodayKey).Seconds;await Task.Delay(120);Check(engine.StudyStatistics(engine.TodayKey).Seconds==before,"home does not accumulate daily time");
        engine.AddStudyTime(DateTime.Now.Date.AddDays(-1).AddHours(21),90);Save();
        Screenshot(Path.Combine(qaFolder,"14-今日统计.png"));Click("历史记录");Check(page=="daily","history entry opens daily page");
        Check(engine.StudyHistory().Count==2&&engine.StudyHistory()[0].Date==engine.TodayKey,"history contains both learning days in order");
        Screenshot(Path.Combine(qaFolder,"15-每日历史.png"));var restored=new Engine(engine.FilePath);restored.Load();
        Check(restored.StudyStatistics(restored.TodayKey).Words==1&&restored.StudyHistory().Count==2,"daily history persists after reload");
        Click("返回首页");Width=800;Height=680;UpdateLayout();
        var scroll=body.Children[0] as ScrollViewer;Check(scroll!=null&&scroll.ViewportHeight>0,"small desktop home remains scrollable");scroll.ScrollToEnd();UpdateLayout();Screenshot(Path.Combine(qaFolder,"16-小窗口每日统计.png"));
        Width=1040;Height=780;
    }
}
}
