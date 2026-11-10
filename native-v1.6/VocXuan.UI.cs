using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ShiCi {
public sealed class WordRow {
    public List<ReviewWord> Words { get; set; }
}

public partial class ReaderWindow : Window {
    const string Green="#2A6851", Ink="#27392E", Muted="#7B877C", Line="#E2E7DE", Paper="#F7F8F5";
    static readonly Random random=new Random();
    static readonly string[][] Books={
        new[]{"cet4","大学英语四级"},new[]{"cet6","大学英语六级"},new[]{"ky","考研英语"},
        new[]{"ielts","雅思 IELTS"},new[]{"toefl","托福 TOEFL"},new[]{"gre","GRE"},
        new[]{"gk","高中英语"},new[]{"common","常用 3000 词"},new[]{"import","我的词书"}
    };
    public readonly Engine engine;
    readonly ActivityTimer clock=new ActivityTimer();
    readonly DispatcherTimer tick=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};
    readonly string qaFolder;
    readonly WordSpeaker speaker;
    Grid outer,body;
    StackPanel headerBrand;
    Button bookButton,pauseButton,revealButton,undoButton,speakButton;
    Button homePrimary,homeSecondary,homeDue;
    Grid homeFooter;
    Round homeResume;
    bool preferActiveReview;
    ComboBox bookChoice,countChoice;
    CheckBox shuffle,skipMastered;
    TextBlock homeStudied,homeMastered;
    ProgressBar homeProgress;
    TextBlock wordText,phoneticText,meaningText,timeText,countText;
    ProgressBar progress;
    string page="home",returnPage="home";
    bool returnManuallyPaused,showAll,filterFuzzy;
    Point pointer=new Point(double.NaN,double.NaN);
    DateTime lastRate=DateTime.MinValue;
    DateTime lastSaved=DateTime.UtcNow;
    bool backgroundSuspended;

    public ReaderWindow(Engine data,string qa) {
        engine=data;qaFolder=qa;
        clock.OnElapsed=engine.AddStudyTime;
        speaker=new WordSpeaker(qa==null?null:Path.Combine(qa,"speech-test.wav"));
        Title="VocXuan";Width=1040;Height=780;MinWidth=800;MinHeight=680;
        if(qa!=null){ShowActivated=false;ShowInTaskbar=false;Opacity=0;}
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new FontFamily(VisualTheme.UiFont);FontSize=14;Background=Brush(Paper);Foreground=Brush(Ink);Icon=RomanLogo.Image();
        Resources=(ResourceDictionary)XamlReader.Parse(VisualTheme.Xaml);
        UseLayoutRounding=true;SnapsToDevicePixels=true;
        TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(this,TextRenderingMode.ClearType);
        BuildShell();Home();
        PreviewKeyDown+=Keys;
        PreviewMouseDown+=delegate {Activity();};
        PreviewMouseWheel+=delegate {Activity();};
        PreviewMouseMove+=delegate(object sender,MouseEventArgs e) {
            var position=e.GetPosition(this);
            if(double.IsNaN(pointer.X)||Math.Abs(position.X-pointer.X)+Math.Abs(position.Y-pointer.Y)>0.5) {pointer=position;Activity();}
        };
        Closing+=delegate {Halt();Save();tick.Stop();speaker.Dispose();};
        Deactivated+=delegate {if(page=="test"){backgroundSuspended=!clock.ManualPaused;Halt();Save();}};
        Activated+=delegate {if(page=="test"&&backgroundSuspended){backgroundSuspended=false;clock.Resume();UpdateClock();}};
        tick.Tick+=delegate {if(page=="test"){bool running=clock.Running;SyncTime();UpdateClock();if(clock.Running&&(DateTime.UtcNow-lastSaved).TotalSeconds>=2||running&&!clock.Running)Save();}else if(page=="home"&&homeStatsDay!=engine.TodayKey)UpdateDailyHome();};tick.Start();
        if(!string.IsNullOrEmpty(engine.LoadWarning)) Loaded+=delegate {Alert(engine.LoadWarning);};
        if(qaFolder!=null) Loaded+=delegate {Dispatcher.BeginInvoke(new Action(Qa),DispatcherPriority.ApplicationIdle);};
    }
    public static SolidColorBrush Brush(string hex){return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);}
    TextBlock Text(string value,double size,string color=Ink,bool bold=false){return new TextBlock {Text=value,FontSize=size,FontFamily=new FontFamily(VisualTheme.UiFont),Foreground=Brush(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};}
    StackPanel Stack(double top=0){return new StackPanel {Margin=new Thickness(0,top,0,0)};}
    void Place(Grid grid,UIElement element,int row=0,int column=0){Grid.SetRow(element,row);Grid.SetColumn(element,column);grid.Children.Add(element);}
    Button Btn(string label,Action action,bool primary=false){var b=new Button {Content=label};if(primary){b.Background=Brush(Green);b.Foreground=Brushes.White;b.BorderBrush=Brush(Green);}b.Click+=delegate {Activity();action();};return b;}
    Border Card(UIElement child,Thickness padding){return new Border {Child=child,Padding=padding,Background=Brushes.White,BorderBrush=Brush(Line),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16)};}
    Image Logo(double size){return new Image {Source=RomanLogo.Image(),Width=size,Height=size,Stretch=Stretch.Uniform};}
    void BuildShell(){
        outer=new Grid {Margin=new Thickness(40,24,40,28),Background=Brush(Paper)};
        outer.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});outer.RowDefinitions.Add(new RowDefinition());
        var header=new Grid {Margin=new Thickness(0,0,0,18)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        headerBrand=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};headerBrand.Children.Add(Logo(33));
        var name=Text("VocXuan",20,Ink,true);name.Margin=new Thickness(8,0,0,0);name.VerticalAlignment=VerticalAlignment.Center;headerBrand.Children.Add(name);Place(header,headerBrand);
        bookButton=Btn("生词本",OpenLibrary);Place(header,bookButton,0,1);Place(outer,header);
        body=new Grid();Place(outer,body,1);Content=outer;
    }
    void Page(string value){speaker.Stop();page=value;body.Children.Clear();headerBrand.Visibility=value=="home"?Visibility.Hidden:Visibility.Visible;bookButton.Visibility=value=="library"?Visibility.Hidden:Visibility.Visible;}
    void Save(){lastSaved=DateTime.UtcNow;try{engine.Save();}catch(Exception ex){Alert("无法保存进度："+ex.Message);}}
    void SyncTime(){if(page=="test"&&engine.Data.Round!=null&&!engine.Data.Round.Finished)engine.Data.Round.Seconds=clock.Seconds;}
    void Halt(){clock.Stop();SyncTime();}
    static string Duration(double seconds){var t=TimeSpan.FromSeconds(Math.Max(0,seconds));return ((int)t.TotalMinutes).ToString("00")+":"+t.Seconds.ToString("00");}
    void Activity(){if(page!="test")return;clock.Activity();UpdateClock();}
    void UpdateClock(){if(timeText==null)return;timeText.Text=Duration(clock.Seconds);timeText.Foreground=Brush(clock.IdlePaused||clock.ManualPaused?Muted:Ink);pauseButton.Content=clock.ManualPaused?"继续":"暂停";}

    void Home(){
        Halt();Save();Page("home");
        var center=Stack();center.Width=420;center.VerticalAlignment=VerticalAlignment.Center;center.HorizontalAlignment=HorizontalAlignment.Center;center.Margin=new Thickness(0,8,0,16);
        var logo=Logo(76);logo.Margin=new Thickness(0,0,0,15);center.Children.Add(logo);
        var title=Text("VocXuan",41,Ink,true);title.TextAlignment=TextAlignment.Center;title.Margin=new Thickness(0,0,0,31);center.Children.Add(title);
        var config=new Grid();config.ColumnDefinitions.Add(new ColumnDefinition());config.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(108)});
        bookChoice=new ComboBox {Margin=new Thickness(0,0,12,0),ToolTip="词书"};foreach(var b in Books)bookChoice.Items.Add(new ComboBoxItem {Content=b[1],Tag=b[0]});Place(config,bookChoice);
        countChoice=new ComboBox {IsEditable=true,ToolTip="本轮数量"};foreach(string n in new[]{"20","50","100","200","全部"})countChoice.Items.Add(n);countChoice.Text="20";Place(config,countChoice,0,1);center.Children.Add(config);
        string selected=engine.Data.SelectedBook;
        if(string.IsNullOrEmpty(selected)){var previous=engine.Data.Round==null?null:Books.FirstOrDefault(b=>b[1]==engine.Data.Round.Book);selected=previous==null?"cet4":previous[0];}
        int selectedIndex=Array.FindIndex(Books,b=>b[0]==selected);bookChoice.SelectedIndex=selectedIndex<0?0:selectedIndex;
        engine.Data.SelectedBook=(string)((ComboBoxItem)bookChoice.SelectedItem).Tag;
        preferActiveReview=engine.Data.Round!=null&&engine.Data.Round.IsReview&&!engine.Data.Round.Finished;
        bookChoice.SelectionChanged+=delegate {engine.Data.SelectedBook=(string)((ComboBoxItem)bookChoice.SelectedItem).Tag;preferActiveReview=false;UpdateHomeActions();Save();};
        homePrimary=Btn("开始",delegate {if(homeResume!=null){engine.ActivateRound(homeResume);Begin();}else StartSelected();},true);homePrimary.Height=50;homePrimary.Margin=new Thickness(0,16,0,0);center.Children.Add(homePrimary);
        var stats=new Grid {Margin=new Thickness(0,20,0,7)};stats.ColumnDefinitions.Add(new ColumnDefinition());stats.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});homeStudied=Text("",12,Muted);homeMastered=Text("",12,Green);Place(stats,homeStudied);Place(stats,homeMastered,0,1);center.Children.Insert(center.Children.Count-1,stats);
        homeProgress=new ProgressBar {Minimum=0,Maximum=1,Height=6,Foreground=Brush(Green),Background=Brush("#E3EAE4"),BorderThickness=new Thickness(0)};center.Children.Insert(center.Children.Count-1,homeProgress);
        var tools=new Grid {Margin=new Thickness(0,17,0,0)};tools.ColumnDefinitions.Add(new ColumnDefinition());tools.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var options=new StackPanel {Orientation=Orientation.Horizontal};shuffle=new CheckBox {Content="随机",IsChecked=true,VerticalAlignment=VerticalAlignment.Center};options.Children.Add(shuffle);skipMastered=new CheckBox {Content="跳过已掌握",IsChecked=engine.Data.SkipMastered,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(20,0,0,0)};skipMastered.Click+=delegate {engine.Data.SkipMastered=skipMastered.IsChecked==true;UpdateHomeActions();Save();};options.Children.Add(skipMastered);Place(tools,options);var import=Btn("导入",Import);import.Padding=new Thickness(14,7,14,7);Place(tools,import,0,1);center.Children.Add(tools);
        homeFooter=new Grid {Margin=new Thickness(0,16,0,0)};homeFooter.ColumnDefinitions.Add(new ColumnDefinition());homeFooter.ColumnDefinitions.Add(new ColumnDefinition());
        homeSecondary=Btn("新一轮",delegate {if(homeResume!=null)StartSelected();else {var previous=engine.SavedRound(engine.Data.SelectedBook);if(previous!=null){engine.ActivateRound(previous);Results();}}});homeSecondary.Margin=new Thickness(0,0,5,0);Place(homeFooter,homeSecondary);
        homeDue=Btn("到期复习 · 0",StartDueReview);homeDue.Margin=new Thickness(5,0,0,0);homeDue.Foreground=Brush(Green);Place(homeFooter,homeDue,0,1);center.Children.Add(homeFooter);UpdateHomeActions();
        center.Children.Add(DailyHomeCard());UpdateDailyHome();
        body.Children.Add(new ScrollViewer {Content=center,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalContentAlignment=VerticalAlignment.Center});
    }
    void UpdateHomeActions(){
        if(homePrimary==null||homeSecondary==null)return;
        var current=engine.Data.Round;bool resumeReview=preferActiveReview&&current!=null&&current.IsReview&&!current.Finished&&(!current.IsSpacedReview||current.SourceBookKey==engine.Data.SelectedBook);
        homeResume=resumeReview?current:engine.PendingRound(engine.Data.SelectedBook);
        homePrimary.Content=homeResume==null?"开始":homeResume.IsSpacedReview?"继续复习":homeResume.IsReview?"继续生词本":"继续";
        homePrimary.ToolTip=homeResume==null?null:(object)(homeResume.Book+"  "+homeResume.Answers.Count+"/"+homeResume.Words.Count);
        var previous=engine.SavedRound(engine.Data.SelectedBook);homeSecondary.Content=homeResume==null?"上轮结果":"新一轮";homeSecondary.Visibility=homeResume!=null||previous!=null&&previous.Finished?Visibility.Visible:Visibility.Collapsed;
        var stats=engine.Progress(engine.Data.SelectedBook);if(homeStudied!=null){homeStudied.Text="已学 "+stats.Studied+"/"+stats.Total;homeMastered.Text="已掌握 "+stats.Mastered+" · 剩余 "+stats.Remaining;homeProgress.Maximum=Math.Max(1,stats.Total);homeProgress.Value=stats.Mastered;homeProgress.ToolTip="掌握进度 "+(stats.Total==0?0:100.0*stats.Mastered/stats.Total).ToString("0.0")+"%";}
        bool completed=homeResume==null&&stats.Total>0&&stats.Remaining==0&&engine.Data.SkipMastered;homePrimary.IsEnabled=!completed;if(completed)homePrimary.Content="已全部掌握";
        if(homeDue!=null){var due=engine.DueWords(engine.Data.SelectedBook,50000);var pending=engine.PendingRound("due:"+engine.Data.SelectedBook);homeDue.Content=pending!=null?"继续到期复习":"到期复习 · "+due.Count;homeDue.IsEnabled=pending!=null||due.Count>0;var next=engine.NextReviewDate(engine.Data.SelectedBook);homeDue.ToolTip=pending!=null?"继续上次到期复习，本轮进度 "+pending.Answers.Count+"/"+pending.Words.Count:due.Count>0?"每轮最多复习 10 个已掌握的词":next.HasValue?"下次复习："+next.Value.ToString("M月d日"):"掌握单词后，隔两天开始少量复习";bool hasSecondary=homeSecondary.Visibility==Visibility.Visible;homeFooter.ColumnDefinitions[0].Width=hasSecondary?new GridLength(1,GridUnitType.Star):new GridLength(0);homeDue.Margin=new Thickness(hasSecondary?5:0,0,0,0);}
    }
    void StartDueReview(){string key=engine.Data.SelectedBook;var pending=engine.PendingRound("due:"+key);if(pending!=null){engine.ActivateRound(pending);Begin();return;}var title=Books.FirstOrDefault(b=>b[0]==key);if(engine.StartSpacedReview(key,title==null?key:title[1]))Begin();else UpdateHomeActions();}
    void StartSelected(){
        var selected=bookChoice.SelectedItem as ComboBoxItem;if(selected==null)return;
        var words=engine.Book((string)selected.Tag);if(words.Count==0){Alert("请先导入单词。每行一个英文单词即可。");return;}
        int count;
        if(countChoice.Text.Trim()=="全部")count=words.Count;
        else if(!int.TryParse(countChoice.Text.Trim(),out count)||count<1||count>50000){Alert("数量请输入 1—50000，或选择“全部”。");return;}
        string key=(string)selected.Tag;if(!CanReplaceRound(key))return;
        var planned=engine.PlanWords(key,Math.Min(count,words.Count),shuffle.IsChecked==true);if(planned.Count==0){Alert("已全部掌握。取消“跳过已掌握”可再次检测整本词书。");return;}engine.Start(planned,selected.Content.ToString(),key);Begin();
    }
    bool CanReplaceRound(string key){return engine.PendingRound(key)==null||Ask("开始新一轮？未完成的本轮将结束，已测记录保留。");}
    static void Shuffle<T>(List<T> list){for(int i=list.Count-1;i>0;i--){int j=random.Next(i+1);T item=list[i];list[i]=list[j];list[j]=item;}}
    void Begin(){clock.Start(engine.Data.Round.Seconds);Test();Save();}
    void Resume(){Begin();}
    void Test(){
        Page("test");var r=engine.Data.Round;
        var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        var bar=new Grid {Margin=new Thickness(0,0,0,18)};bar.ColumnDefinitions.Add(new ColumnDefinition());bar.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});bar.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var book=Text(r.Book,14);book.VerticalAlignment=VerticalAlignment.Center;Place(bar,book);
        timeText=Text("",17);timeText.FontFamily=new FontFamily(VisualTheme.NumberFont);timeText.VerticalAlignment=VerticalAlignment.Center;timeText.Margin=new Thickness(10,0,18,0);Place(bar,timeText,0,1);
        pauseButton=Btn("暂停",TogglePause);Place(bar,pauseButton,0,2);Place(layout,bar);
        var card=new Grid();card.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});card.RowDefinitions.Add(new RowDefinition());
        var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});countText=Text("",13,Muted);countText.FontFamily=new FontFamily(VisualTheme.NumberFont);Place(top,countText);
        undoButton=Btn("撤销",Undo);undoButton.Padding=new Thickness(11,6,11,6);undoButton.FontSize=14;Place(top,undoButton,0,1);Place(card,top);
        var area=Stack();area.Width=720;area.Margin=new Thickness(0,9,0,0);
        wordText=Text("",58);wordText.FontFamily=new FontFamily(VisualTheme.WordFont);wordText.TextAlignment=TextAlignment.Center;area.Children.Add(wordText);
        var pronunciation=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,12,0,0)};
        phoneticText=Text("",18,Muted);phoneticText.FontFamily=new FontFamily(VisualTheme.PhoneticFont);phoneticText.TextAlignment=TextAlignment.Center;phoneticText.VerticalAlignment=VerticalAlignment.Center;pronunciation.Children.Add(phoneticText);
        speakButton=Btn("",SpeakCurrentWord);speakButton.Name="SpeakWord";speakButton.ToolTip="播放英语发音（V）";System.Windows.Automation.AutomationProperties.SetName(speakButton,"播放当前单词发音");speakButton.Width=38;speakButton.Height=34;speakButton.Padding=new Thickness(8);speakButton.Margin=new Thickness(9,0,0,0);speakButton.Background=Brushes.Transparent;speakButton.BorderThickness=new Thickness(0);speakButton.Content=new System.Windows.Shapes.Path {Data=Geometry.Parse("M 2,7 L 6,7 L 11,3 L 11,17 L 6,13 L 2,13 Z M 14,6 C 18,8 18,12 14,14 M 17,3 C 23,7 23,13 17,17"),Stroke=Brush(Muted),StrokeThickness=1.5,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=20,Height=18,Stretch=Stretch.Uniform};pronunciation.Children.Add(speakButton);area.Children.Add(pronunciation);
        revealButton=Btn("释义",ToggleMeaning);revealButton.HorizontalAlignment=HorizontalAlignment.Center;revealButton.Margin=new Thickness(0,25,0,0);revealButton.Padding=new Thickness(22,9,22,9);area.Children.Add(revealButton);
        meaningText=Text("",16,"#526456");meaningText.TextAlignment=TextAlignment.Center;meaningText.LineHeight=28;
        var scroll=new ScrollViewer {Content=meaningText,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=130};area.Children.Add(new Border {Child=scroll,Margin=new Thickness(0,19,0,0),MinHeight=72,MaxHeight=132});
        Place(card,new Viewbox {Child=area,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly},1);Place(layout,Card(card,new Thickness(27,21,27,20)),1);
        var lower=Stack(18);progress=new ProgressBar {Minimum=0,Maximum=r.Words.Count,Height=3,Foreground=Brush(Green),Background=Brush("#E3EAE4"),BorderThickness=new Thickness(0)};lower.Children.Add(progress);
        var choices=new Grid {Margin=new Thickness(0,17,0,0)};for(int i=0;i<3;i++)choices.ColumnDefinitions.Add(new ColumnDefinition());
        string[] names={"认识","不认识","模糊"},colors={Green,"#B24F47","#977014"},fills={"#EAF5EF","#FCF0ED","#FAF4E5"};
        for(int i=0;i<3;i++){int rating=i;var b=Btn(names[i],delegate {Rate(rating);});b.Height=78;b.FontSize=20;b.FontWeight=FontWeights.SemiBold;b.Foreground=Brush(colors[i]);b.Background=Brush(fills[i]);b.BorderBrush=Brush(fills[i]);b.Margin=new Thickness(i==0?0:7,0,i==2?0:7,0);Place(choices,b,0,i);}lower.Children.Add(choices);
        var end=Btn("结束",FinishEarly);end.Margin=new Thickness(0,12,0,0);end.Background=Brush(Paper);end.BorderThickness=new Thickness(0);end.FontSize=13;end.Padding=new Thickness(14,7,14,7);end.HorizontalAlignment=HorizontalAlignment.Center;lower.Children.Add(end);Place(layout,lower,2);body.Children.Add(layout);ShowWord();
    }
    void ShowWord(){
        speaker.Stop();
        var r=engine.Data.Round;if(r.Answers.Count>=r.Words.Count){Results();return;}var w=r.Words[r.Answers.Count];
        bool paused=clock.ManualPaused;wordText.Text=paused?"暂停":w.Text;wordText.FontSize=w.Text.Length>22?38:58;
        phoneticText.Text=paused?"":Phonetics.Display(w.Phonetic);
        meaningText.Text="";revealButton.Content="释义";revealButton.IsEnabled=!paused;speakButton.IsEnabled=!paused;countText.Text=(r.Answers.Count+1)+"/"+r.Words.Count;progress.Value=r.Answers.Count;undoButton.IsEnabled=r.Answers.Count>0&&!paused;UpdateClock();
    }
    void SpeakCurrentWord(){if(page!="test"||clock.ManualPaused||engine.Data.Round==null||engine.Data.Round.Answers.Count>=engine.Data.Round.Words.Count)return;try{speaker.Speak(engine.Data.Round.Words[engine.Data.Round.Answers.Count].Text);speakButton.ToolTip="重播英语发音（V） · "+speaker.VoiceName;}catch(Exception ex){Alert("发音暂不可用："+ex.Message);}}
    void ToggleMeaning(){if(page!="test"||clock.ManualPaused)return;bool visible=meaningText.Text.Length>0;meaningText.Text=visible?"":engine.Data.Round.Words[engine.Data.Round.Answers.Count].Meaning;revealButton.Content=visible?"释义":"收起";}
    void TogglePause(){if(page!="test")return;clock.ToggleManual();SyncTime();Save();ShowWord();}
    void Rate(int rating){
        if(page!="test"||clock.ManualPaused||(DateTime.UtcNow-lastRate).TotalMilliseconds<180)return;
        clock.Activity();lastRate=DateTime.UtcNow;SyncTime();engine.Rate(rating);
        if(engine.Data.Round.Finished){clock.Stop();Results();}else ShowWord();Save();
    }
    void Undo(){if(clock.ManualPaused||engine.Data.Round.Answers.Count==0)return;SyncTime();engine.Undo();Save();ShowWord();}
    void FinishEarly(){
        Halt();bool manual=clock.ManualPaused;
        if(Ask("结束本轮并查看结果？")){engine.Data.Round.Finished=true;Save();Results();}
        else if(!manual)clock.Resume();
    }
    void Results(){Halt();Page("results");showAll=false;filterFuzzy=false;BuildResults();}
    void BuildResults(){
        body.Children.Clear();var r=engine.Data.Round;
        var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        var heading=new Grid {Margin=new Thickness(0,0,0,19)};heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var summary=Text(r.Answers.Count+"/"+r.Words.Count+"   "+Duration(r.Seconds),24);summary.VerticalAlignment=VerticalAlignment.Center;Place(heading,summary);Place(heading,ColumnPicker(BuildResults),0,1);Place(grid,heading);
        var tabs=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,14)};
        tabs.Children.Add(Tab("生词 "+r.Answers.Count(a=>a.Rating!=0),!showAll&&!filterFuzzy,delegate {showAll=false;filterFuzzy=false;BuildResults();}));
        tabs.Children.Add(Tab("模糊",filterFuzzy,delegate {showAll=false;filterFuzzy=true;BuildResults();}));
        tabs.Children.Add(Tab("全部",showAll,delegate {showAll=true;filterFuzzy=false;BuildResults();}));Place(grid,tabs,1);
        var selected=r.Answers.Where(a=>showAll||filterFuzzy&&a.Rating==2||!filterFuzzy&&a.Rating!=0).Select(a=>new ReviewWord {Word=a.Word,Rating=a.Rating}).ToList();
        Place(grid,selected.Count==0?Empty("暂无单词"):WordList(selected),2);
        var buttons=new Grid {Margin=new Thickness(0,17,0,0)};buttons.ColumnDefinitions.Add(new ColumnDefinition());buttons.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});buttons.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});buttons.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var again=Btn("复测",delegate {RestartWords(r.Answers.Where(a=>a.Rating!=0).Select(a=>a.Word).ToList(),"生词复测");},true);again.HorizontalAlignment=HorizontalAlignment.Left;again.IsEnabled=r.Answers.Any(a=>a.Rating!=0);Place(buttons,again);
        var rewind=Btn("撤销",delegate {engine.Undo();Begin();});rewind.IsEnabled=r.Answers.Count>0;rewind.Margin=new Thickness(0,0,10,0);Place(buttons,rewind,0,1);
        var export=Btn("导出",delegate {Export(r.Answers.Where(a=>a.Rating!=0).Select(a=>new ReviewWord {Word=a.Word,Rating=a.Rating}).ToList());});export.IsEnabled=r.Answers.Any(a=>a.Rating!=0);export.Margin=new Thickness(0,0,10,0);Place(buttons,export,0,2);Place(buttons,Btn("首页",Home),0,3);Place(grid,buttons,3);body.Children.Add(grid);
    }
    Button Tab(string label,bool active,Action action){var b=Btn(label,action);b.Margin=new Thickness(0,0,8,0);b.Padding=new Thickness(13,8,13,8);if(active){b.Background=Brush("#E8F0E5");b.BorderBrush=Brush("#D6E2D0");b.Foreground=Brush(Green);}return b;}
    Border ColumnPicker(Action refresh){
        var picker=new StackPanel {Orientation=Orientation.Horizontal};
        foreach(int c in new[]{2,3}){int columns=c;bool selected=engine.Data.Columns==c;var b=Btn(c==2?"双列":"三列",delegate {engine.Data.Columns=columns;Save();refresh();});b.Padding=new Thickness(13,7,13,7);b.FontSize=12;b.BorderThickness=new Thickness(0);b.Background=selected?Brushes.White:Brushes.Transparent;b.Foreground=Brush(selected?Green:Muted);b.Margin=new Thickness(1,0,1,0);picker.Children.Add(b);}
        return new Border {Child=picker,Background=Brush("#EDF1E9"),BorderBrush=Brush("#E6EBE0"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(3)};
    }
    UIElement Empty(string label){var text=Text(label,21,Muted);text.HorizontalAlignment=HorizontalAlignment.Center;text.VerticalAlignment=VerticalAlignment.Center;return text;}
    UIElement WordList(List<ReviewWord> words){
        int columns=engine.Data.Columns==3?3:2;
        var rows=new List<WordRow>();for(int i=0;i<words.Count;i+=columns)rows.Add(new WordRow {Words=words.Skip(i).Take(columns).ToList()});
        var list=new ListBox {BorderThickness=new Thickness(0),Background=Brush(Paper),HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(0)};
        VirtualizingStackPanel.SetIsVirtualizing(list,true);VirtualizingStackPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);ScrollViewer.SetCanContentScroll(list,true);ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
        var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));itemStyle.Setters.Add(new Setter(Control.MarginProperty,new Thickness(0,0,0,13)));itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));itemStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><ContentPresenter/></ControlTemplate>")));list.ItemContainerStyle=itemStyle;
        string template=@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<ItemsControl ItemsSource='{Binding Words}' HorizontalContentAlignment='Stretch'><ItemsControl.ItemsPanel><ItemsPanelTemplate><UniformGrid Columns='__COLS__'/></ItemsPanelTemplate></ItemsControl.ItemsPanel><ItemsControl.ItemTemplate><DataTemplate>
 <Border x:Name='WordCard' Tag='word-card' Background='White' BorderBrush='#E2E7DE' BorderThickness='1' CornerRadius='12' Padding='21,21,19,20' Margin='0,0,13,0'>
  <Grid>
   <StackPanel>
    <TextBlock Text='{Binding Word.Text}' FontFamily='Georgia' FontSize='24' Foreground='#27392E' TextWrapping='Wrap' Margin='0,0,31,0'/>
    <TextBlock Text='{Binding Word.Phonetic}' FontFamily='__IPA__' FontSize='14' Foreground='#8A958A' Margin='0,10,0,0' TextWrapping='Wrap'/>
    <TextBlock Text='{Binding Word.Meaning}' FontFamily='Segoe UI, Microsoft YaHei UI' FontSize='14' Foreground='#58685B' Margin='0,16,0,0' LineHeight='24' TextWrapping='Wrap'/>
   </StackPanel>
   <Border x:Name='StateBadge' Tag='state-marker' Width='23' Height='23' HorizontalAlignment='Right' VerticalAlignment='Top' CornerRadius='8'>
    <Border.Style><Style TargetType='Border'><Setter Property='Visibility' Value='Collapsed'/><Style.Triggers>
     <DataTrigger Binding='{Binding Rating}' Value='1'><Setter Property='Visibility' Value='Visible'/><Setter Property='Background' Value='#FDF0ED'/><Setter Property='ToolTip' Value='不认识'/></DataTrigger>
     <DataTrigger Binding='{Binding Rating}' Value='2'><Setter Property='Visibility' Value='Visible'/><Setter Property='Background' Value='#FDF6E0'/><Setter Property='ToolTip' Value='模糊'/></DataTrigger>
    </Style.Triggers></Style></Border.Style>
    <Grid>
     <Path Data='M 6,0 L 12,10 L 0,10 Z' Fill='#DCAD2E' Width='12' Height='10' Stretch='Uniform' HorizontalAlignment='Center' VerticalAlignment='Center'>
      <Path.Style><Style TargetType='Path'><Setter Property='Visibility' Value='Collapsed'/><Style.Triggers><DataTrigger Binding='{Binding Rating}' Value='2'><Setter Property='Visibility' Value='Visible'/></DataTrigger></Style.Triggers></Style></Path.Style>
     </Path>
     <Path Data='M 1,1 L 9,9 M 9,1 L 1,9' Width='10' Height='10' Stroke='#C65F50' StrokeThickness='1.8' StrokeStartLineCap='Round' StrokeEndLineCap='Round' HorizontalAlignment='Center' VerticalAlignment='Center'>
      <Path.Style><Style TargetType='Path'><Setter Property='Visibility' Value='Collapsed'/><Style.Triggers><DataTrigger Binding='{Binding Rating}' Value='1'><Setter Property='Visibility' Value='Visible'/></DataTrigger></Style.Triggers></Style></Path.Style>
     </Path>
    </Grid>
   </Border>
  </Grid>
 </Border>
 <DataTemplate.Triggers><Trigger SourceName='WordCard' Property='IsMouseOver' Value='True'><Setter TargetName='WordCard' Property='BorderBrush' Value='#BFCFBA'/></Trigger></DataTemplate.Triggers>
</DataTemplate></ItemsControl.ItemTemplate></ItemsControl></DataTemplate>";
        list.ItemTemplate=(DataTemplate)XamlReader.Parse(template.Replace("__COLS__",columns.ToString()).Replace("__IPA__",VisualTheme.PhoneticFont));list.ItemsSource=rows;return list;
    }
    void RestartWords(List<Word> words,string name){if(words.Count==0||!CanReplaceRound("review"))return;Shuffle(words);engine.Start(words,name,"review",true);Begin();}
    void OpenLibrary(){if(page=="library")return;returnPage=page;returnManuallyPaused=clock.ManualPaused;Halt();Save();Page("library");Library();}
    void Library(){
        body.Children.Clear();var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        var header=new Grid {Margin=new Thickness(0,0,0,21)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});var title=Text("生词本 "+engine.Data.Review.Count,23,Ink,true);title.VerticalAlignment=VerticalAlignment.Center;Place(header,title);Place(header,ColumnPicker(Library),0,1);Place(grid,header);
        var words=engine.Data.Review.Values.OrderByDescending(w=>w.Added).ToList();Place(grid,words.Count==0?Empty("暂无生词"):WordList(words),1);
        var bottom=new Grid {Margin=new Thickness(0,17,0,0)};bottom.ColumnDefinitions.Add(new ColumnDefinition());bottom.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});bottom.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var test=Btn("复测",delegate {RestartWords(engine.Data.Review.Values.Select(w=>w.Word).ToList(),"生词本");},true);test.IsEnabled=words.Count>0;test.HorizontalAlignment=HorizontalAlignment.Left;Place(bottom,test);
        var export=Btn("导出",delegate {Export(words);});export.IsEnabled=words.Count>0;export.Margin=new Thickness(0,0,10,0);Place(bottom,export,0,1);
        Place(bottom,Btn("返回",delegate {if(returnPage=="test"){if(!returnManuallyPaused)clock.Resume();Test();}else if(returnPage=="results")Results();else Home();}),0,2);Place(grid,bottom,2);body.Children.Add(grid);
    }
    void Export(List<ReviewWord> words){
        var d=new SaveFileDialog {Title="导出",Filter="CSV (*.csv)|*.csv|TXT (*.txt)|*.txt",FileName="VocXuan_"+DateTime.Now.ToString("yyyyMMdd_HHmm"),InitialDirectory=AppDomain.CurrentDomain.BaseDirectory};
        if(d.ShowDialog(this)!=true)return;
        try{string data;if(d.FilterIndex==1)data="单词,音标,状态,释义\r\n"+string.Join("\r\n",words.Select(w=>string.Join(",",new[]{Csv(w.Word.Text),Csv(w.Word.Phonetic),Csv(w.Rating==1?"不认识":"模糊"),Csv(w.Word.Meaning)})));
            else data=string.Join("\r\n\r\n",words.Select(w=>(w.Rating==1?"× ":"△ ")+w.Word.Text+"  "+w.Word.Phonetic+"\r\n"+w.Word.Meaning.Replace("\n","\r\n")));File.WriteAllText(d.FileName,data,new UTF8Encoding(true));}
        catch(Exception ex){Alert("导出失败："+ex.Message);}
    }
    static string Csv(string value){string s=value??"";if(s.Length>0&&"=+-@".Contains(s[0]))s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
    void Import(){
        var d=new OpenFileDialog {Title="导入单词",Filter="TXT / CSV / TSV (*.txt;*.csv;*.tsv)|*.txt;*.csv;*.tsv|所有文件 (*.*)|*.*",InitialDirectory=AppDomain.CurrentDomain.BaseDirectory};if(d.ShowDialog(this)!=true)return;
        try{
            if(new FileInfo(d.FileName).Length>8*1024*1024){Alert("文件需小于 8 MB。");return;}byte[] bytes=File.ReadAllBytes(d.FileName);string text;
            if(bytes.Length>=2&&bytes[0]==255&&bytes[1]==254)text=Encoding.Unicode.GetString(bytes);else if(bytes.Length>=2&&bytes[0]==254&&bytes[1]==255)text=Encoding.BigEndianUnicode.GetString(bytes);else{try{text=new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){text=Encoding.GetEncoding(936).GetString(bytes);}}
            int unknown,skipped;var words=engine.ParseImport(text,out unknown,out skipped);if(words.Count==0){Alert("未找到有效单词。请每行写一个英文单词。");return;}if(words.Count>50000){Alert("每次最多导入 50000 词。");return;}
            if(engine.PendingRound("import")!=null&&!Ask("替换导入词书？该词书的未完成一轮会重置，已测记录保留。"))return;
            engine.ArchiveRound();if(engine.Data.Round!=null&&engine.Data.Round.BookKey=="import")engine.Data.Round=null;engine.Data.Rounds.Remove("import");
            engine.Data.Imported=words;bookChoice.SelectedIndex=8;engine.Data.SelectedBook="import";preferActiveReview=false;UpdateHomeActions();Save();if(unknown>0)Alert(unknown+" 词未收录释义，可在导入文件中补充。");
        }catch(Exception ex){Alert("导入失败："+ex.Message);}
    }
    void Keys(object sender,KeyEventArgs e){
        if(page!="test"||e.IsRepeat)return;Activity();if(Keyboard.Modifiers!=ModifierKeys.None)return;
        if(e.Key==Key.Space){ToggleMeaning();e.Handled=true;}else if(e.Key==Key.D1||e.Key==Key.NumPad1){Rate(0);e.Handled=true;}else if(e.Key==Key.D2||e.Key==Key.NumPad2){Rate(1);e.Handled=true;}else if(e.Key==Key.D3||e.Key==Key.NumPad3){Rate(2);e.Handled=true;}else if(e.Key==Key.P){TogglePause();e.Handled=true;}else if(e.Key==Key.Back){Undo();e.Handled=true;}else if(e.Key==Key.V){SpeakCurrentWord();e.Handled=true;}
    }
    bool Dialog(string message,bool confirm){
        var d=new Window {Title="VocXuan",Owner=this,Width=420,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush(Paper),FontFamily=new FontFamily(VisualTheme.UiFont),Icon=RomanLogo.Image(),Resources=Resources};
        var content=new StackPanel {Margin=new Thickness(26)};var copy=Text(message,17);copy.LineHeight=27;content.Children.Add(copy);var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
        if(confirm){var cancel=Btn("取消",delegate {d.DialogResult=false;});cancel.Margin=new Thickness(0,0,10,0);cancel.IsCancel=true;buttons.Children.Add(cancel);}var ok=Btn("确定",delegate {d.DialogResult=true;},true);ok.IsDefault=true;buttons.Children.Add(ok);content.Children.Add(buttons);d.Content=content;return d.ShowDialog()==true;
    }
    bool Ask(string message){return Dialog(message,true);}
    void Alert(string message){Dialog(message,false);}

    public void Screenshot(string path){
        UpdateLayout();double width=outer.ActualWidth+outer.Margin.Left+outer.Margin.Right,height=outer.ActualHeight+outer.Margin.Top+outer.Margin.Bottom;
        var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRectangle(Brush(Paper),null,new Rect(0,0,width,height));d.DrawRectangle(new VisualBrush(outer),null,new Rect(outer.Margin.Left,outer.Margin.Top,outer.ActualWidth,outer.ActualHeight));}
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width),(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);
    }
    int qaChecks;
    void Check(bool value,string name){if(!value)throw new Exception("UI check failed: "+name);qaChecks++;}
    void Click(string label){
        UpdateLayout();var queue=new Queue<DependencyObject>();queue.Enqueue(body);
        while(queue.Count>0){var n=queue.Dequeue();var b=n as Button;if(b!=null&&b.Content is string&&((string)b.Content)==label){b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));return;}for(int i=0;i<VisualTreeHelper.GetChildrenCount(n);i++)queue.Enqueue(VisualTreeHelper.GetChild(n,i));}throw new Exception("Missing button: "+label);
    }
    void CheckFonts(DependencyObject root){
        var text=root as TextBlock;if(text!=null)Check(new[]{VisualTheme.UiFont,VisualTheme.WordFont,VisualTheme.NumberFont,VisualTheme.PhoneticFont}.Contains(text.FontFamily.Source),"font role: "+text.Text);
        var control=root as Control;if(control!=null)Check(control.FontFamily.Source==VisualTheme.UiFont,"system UI font");
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)CheckFonts(VisualTreeHelper.GetChild(root,i));
    }
    int CheckMarkers(DependencyObject root){
        int count=0;var card=root as Border;
        if(card!=null&&(string)card.Tag=="word-card"){
            var queue=new Queue<DependencyObject>();queue.Enqueue(card);
            while(queue.Count>0){var item=queue.Dequeue();var badge=item as Border;
                if(badge!=null&&(string)badge.Tag=="state-marker"&&badge.Visibility==Visibility.Visible){var point=badge.TranslatePoint(new Point(0,0),card);Check(point.X>card.ActualWidth*0.65&&point.Y<card.ActualHeight*0.25,"marker in top right");Check(card.ActualWidth-point.X-badge.ActualWidth>=15,"marker has right inset");count++;}
                for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)queue.Enqueue(VisualTreeHelper.GetChild(item,i));
            }
        }else for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)count+=CheckMarkers(VisualTreeHelper.GetChild(root,i));return count;
    }
    async void Qa(){
        try{
            Directory.CreateDirectory(qaFolder);
            bookChoice.SelectedIndex=3;var checkEngine=new Engine(engine.FilePath);checkEngine.Load();Check(checkEngine.Data.SelectedBook=="ielts","book selection saved immediately");Home();Check(((ComboBoxItem)bookChoice.SelectedItem).Tag.ToString()=="ielts","selected book restored on home");
            Screenshot(Path.Combine(qaFolder,"01-首页.png"));CheckFonts(body);
            await Task.Delay(80);bookChoice.IsDropDownOpen=true;await Task.Delay(80);UpdateLayout();var popup=bookChoice.Template.FindName("PART_Popup",bookChoice) as Popup;Check(popup!=null&&popup.IsOpen,"restyled book picker opens");Screenshot(Path.Combine(qaFolder,"09-词书选择.png"));bookChoice.IsDropDownOpen=false;
            countChoice.Text="37";UpdateLayout();var entry=countChoice.Template.FindName("PART_EditableTextBox",countChoice) as TextBox;Check(entry!=null&&entry.Text=="37","restyled editable count");entry.Text="50";Check(countChoice.Text=="50","edited count reaches combo model");countChoice.Text="20";
            var words=new[]{"resilient","curiosity","ambiguous","preserve","subtle","reluctant","evidence","consequence","heritage","persist","precise","conventional"}.Select(w=>engine.Dictionary[w]).ToList();
            engine.Start(words.Take(4).ToList(),"雅思 IELTS");Begin();Screenshot(Path.Combine(qaFolder,"02-检测.png"));Check(countText.Text=="1/4","compact counter");
            var ipaFace=new Typeface(phoneticText.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);GlyphTypeface ipaGlyphs;bool ipaLoaded=ipaFace.TryGetGlyphTypeface(out ipaGlyphs);File.WriteAllText(Path.Combine(qaFolder,"font-check.txt"),ipaLoaded?string.Join(";",ipaGlyphs.FamilyNames.Values)+"\n"+ipaGlyphs.FontUri.ToString():"Typeface unavailable");Check(ipaLoaded&&ipaGlyphs.FamilyNames.Values.Contains("Noto Sans"),"embedded IPA font loaded");Check(new[]{0x0259,0x025c,0x02d0,0x02c8,0x0283}.All(c=>ipaGlyphs.CharacterToGlyphMap.ContainsKey(c)),"IPA glyph coverage");
            Check(body.ActualHeight>0,"layout ready");Click("释义");Check(meaningText.Text==words[0].Meaning,"definition");Screenshot(Path.Combine(qaFolder,"03-释义.png"));
            Click("暂停");Check(clock.ManualPaused&&!clock.Running,"manual pause");clock.Activity();Check(clock.ManualPaused&&!clock.Running,"activity cannot undo manual pause");Click("继续");Check(!clock.ManualPaused&&clock.Running,"manual resume");
            lastRate=DateTime.MinValue;Click("认识");Check(wordText.Text=="curiosity"&&meaningText.Text=="","known advances and conceals definition");
            lastRate=DateTime.MinValue;Click("不认识");lastRate=DateTime.MinValue;Click("模糊");Check(engine.Data.Review.Count==2&&wordText.Text=="preserve","both states saved");Click("撤销");Check(engine.Data.Review.Count==1&&wordText.Text=="ambiguous","undo");lastRate=DateTime.MinValue;Click("模糊");lastRate=DateTime.MinValue;Click("认识");Check(page=="results"&&engine.Data.Round.Finished,"completion");Screenshot(Path.Combine(qaFolder,"04-复习.png"));CheckFonts(body);
            for(int i=0;i<words.Count;i++)engine.Data.Review[words[i].Text]=new ReviewWord {Word=words[i],Rating=i%2==0?2:1,Added="2026-10-27 00:"+i.ToString("00")+":00"};
            OpenLibrary();Click("双列");Screenshot(Path.Combine(qaFolder,"05-生词本双列.png"));Check(engine.Data.Columns==2,"two columns");Check(CheckMarkers(body)>0,"two-column markers visible");Click("三列");Screenshot(Path.Combine(qaFolder,"06-生词本三列.png"));Check(CheckMarkers(body)>0,"three-column markers visible");checkEngine.Load();Check(checkEngine.Data.Columns==3,"three columns persisted");CheckFonts(body);
            Width=800;Height=680;UpdateLayout();Screenshot(Path.Combine(qaFolder,"07-小窗口三列.png"));Width=1040;Height=780;
            checkEngine.Load();var restoreWindow=new ReaderWindow(checkEngine,null);Check(checkEngine.Data.SelectedBook=="ielts"&&((ComboBoxItem)restoreWindow.bookChoice.SelectedItem).Tag.ToString()=="ielts","book survives app restart");Check(checkEngine.Data.Columns==3,"columns survive restart");restoreWindow.Close();
            var legacyEngine=new Engine(Path.Combine(qaFolder,"legacy-ui","progress.json"));legacyEngine.Start(words,"雅思 IELTS");legacyEngine.Data.Round.Seconds=91.25;var legacyWindow=new ReaderWindow(legacyEngine,null);Check(legacyEngine.Data.SelectedBook=="ielts","infer last book from old version");Check(legacyEngine.Data.Round.Seconds==91.25,"old elapsed time remains intact on home");legacyWindow.Close();
            engine.Start(words.Take(4).ToList(),"雅思 IELTS");Begin();await Task.Delay(6200);double stopped=clock.Seconds;Check(clock.IdlePaused&&!clock.Running&&Math.Abs(stopped-5)<0.1,"real UI pauses after five seconds");Check(wordText.Text=="resilient","idle retains visible word");Screenshot(Path.Combine(qaFolder,"08-空闲计时.png"));await Task.Delay(1100);Check(Math.Abs(clock.Seconds-stopped)<0.05,"real UI time stays stopped");Activity();await Task.Delay(350);Check(clock.Running&&clock.Seconds>stopped+0.25,"real UI activity resumes timer");
            SyncTime();var unfinished=engine.Data.Round;Home();Check(homePrimary.Content.ToString()=="继续"&&homeResume==unfinished,"home primary resumes pending round");bookChoice.SelectedIndex=0;Check(homePrimary.Content.ToString()=="开始","new book has independent start");countChoice.Text="2";shuffle.IsChecked=false;Click("开始");lastRate=DateTime.MinValue;Click("不认识");Home();bookChoice.SelectedIndex=3;Check(homeResume==unfinished&&homePrimary.Content.ToString()=="继续","switching back restores pending book");Click("继续");Check(engine.Data.Round==unfinished&&engine.Data.Round.Words.All(w=>!engine.MasteredWords("ielts").Contains(w.Text)),"continue retains round and skips mastered words");
            engine.Start(new List<Word>{words[0]},"生词本","review",true);Begin();lastRate=DateTime.MinValue;Click("认识");Check(!engine.Data.Review.ContainsKey(words[0].Text)&&page=="results","review recognition removes last word");Click("撤销");Check(engine.Data.Review.ContainsKey(words[0].Text)&&page=="test","results undo restores removed last word");
            engine.Data.Round=null;engine.Data.Rounds.Clear();engine.Data.SelectedBook="ielts";var sample=engine.Book("ielts");engine.Data.Tested["ielts"]=sample.Take(2860).Select(w=>w.Text).ToList();engine.Data.Mastered["ielts"]=sample.Take(2140).Select(w=>w.Text).ToList();Home();Check(homeStudied.Text=="已学 2860/5040"&&homeMastered.Text=="已掌握 2140 · 剩余 2900","book mastery totals shown");Check(homeProgress.Value==2140&&homeProgress.Maximum==5040,"book progress bar reflects mastered fraction");Screenshot(Path.Combine(qaFolder,"10-累计学习进度示例.png"));
            countChoice.Text="3";Click("开始");Check(engine.Data.Round.Words.Count==3&&engine.Data.Round.Words.All(w=>!engine.MasteredWords("ielts").Contains(w.Text)),"UI starts only remaining words");lastRate=DateTime.MinValue;Click("认识");Home();Check(homeMastered.Text=="已掌握 2141 · 剩余 2899","home progress updates after recognition");Click("继续");Click("撤销");Home();Check(homeMastered.Text=="已掌握 2140 · 剩余 2900","undo restores home totals");
            engine.Data.Round=null;engine.Data.Rounds.Clear();engine.Data.Mastered["ielts"]=sample.Select(w=>w.Text).ToList();engine.Data.Tested["ielts"]=sample.Select(w=>w.Text).ToList();Home();Check(!homePrimary.IsEnabled&&homePrimary.Content.ToString()=="已全部掌握","completed book clearly stops regular rounds");skipMastered.IsChecked=false;skipMastered.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Check(homePrimary.IsEnabled&&!engine.Data.SkipMastered,"optional retest reopens completed book");
            engine.Data.Imported=words.Take(4).ToList();engine.Data.SelectedBook="import";engine.Data.Mastered["import"]=new List<string>();engine.Data.Tested["import"]=new List<string>();engine.Data.SkipMastered=true;Home();countChoice.Text="4";shuffle.IsChecked=false;Click("开始");lastRate=DateTime.MinValue;Click("认识");lastRate=DateTime.MinValue;Click("模糊");Home();Check(homeStudied.Text=="已学 2/4"&&homeMastered.Text=="已掌握 1 · 剩余 3","small imported book shows exact counts");
            engine.Start(new List<Word>{engine.Dictionary["world"],engine.Dictionary["bird"]},"音标显示示例","import");Begin();Check(phoneticText.Text=="/ wɜːld /","normalized IPA appears in test view");Screenshot(Path.Combine(qaFolder,"11-按钮与音标示例.png"));CheckFonts(body);
            await QaPronunciationAndDue();
            await QaDaily();
            File.WriteAllText(Path.Combine(qaFolder,"qa-complete.txt"),"PASS: "+qaChecks+" UI assertions. Offline pronunciation, opt-in playback, spaced review, source book, resume, schedule undo, previous mastery and UI flows. Screenshots 10 through 13 use demonstration data.");Close();
        }catch(Exception ex){File.WriteAllText(Path.Combine(qaFolder,"qa-error.txt"),ex.ToString());Close();}
    }
    async Task QaPronunciationAndDue(){
        engine.Data.Round=null;engine.Data.Rounds.Clear();engine.Data.Spaced.Clear();engine.Data.SelectedBook="ielts";var book=engine.Book("ielts");engine.Data.Mastered["ielts"]=book.Take(2140).Select(w=>w.Text).ToList();engine.Data.Tested["ielts"]=book.Take(2860).Select(w=>w.Text).ToList();engine.InitializeSchedules();
        foreach(var word in book.Take(12))engine.Data.Spaced["ielts"][word.Text]=new ReviewSchedule {Stage=0,LastReviewed=engine.Today.AddDays(-3).ToString("yyyy-MM-dd"),DueDate=engine.Today.AddDays(-1).ToString("yyyy-MM-dd")};
        Home();Check(homeDue.IsEnabled&&homeDue.Content.ToString()=="到期复习 · 12","home shows due count");Screenshot(Path.Combine(qaFolder,"12-到期复习入口.png"));
        engine.Start(new List<Word>{book[2141],book[2142]},"雅思 IELTS","ielts");engine.Data.Round.Seconds=17;var normal=engine.Data.Round;Home();homeDue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(page=="test"&&engine.Data.Round.IsSpacedReview&&engine.Data.Round.Words.Count==10,"due entry opens capped review");Check(engine.PendingRound("ielts")==normal&&normal.Seconds==17,"UI due review retains normal round");
        var target=engine.Data.Round.Words[0];var previous=engine.Data.Spaced["ielts"][target.Text].DueDate;Check(speakButton.IsEnabled&&speaker.LastWord==null,"voice button is available without autoplay");speakButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(250);for(int i=0;i<80&&speaker.Speaking;i++)await Task.Delay(50);Check(speaker.LastWord==target.Text&&!speaker.Speaking&&speaker.VoiceName.Contains("Zira"),"voice button generates English pronunciation");
        Screenshot(Path.Combine(qaFolder,"13-发音与延时复测.png"));Click("暂停");Check(!speakButton.IsEnabled,"manual pause disables playback");Click("继续");Check(speakButton.IsEnabled,"resume restores voice button");
        lastRate=DateTime.MinValue;Click("认识");Check(engine.Data.Spaced["ielts"][target.Text].DueDate==engine.Today.AddDays(7).ToString("yyyy-MM-dd")&&engine.Progress("ielts").Mastered==2140,"correct due answer reschedules without double counting");
        Click("撤销");Check(engine.Data.Spaced["ielts"][target.Text].DueDate==previous&&wordText.Text==target.Text,"UI undo restores exact schedule");lastRate=DateTime.MinValue;Click("模糊");Check(engine.Progress("ielts").Mastered==2139&&engine.Data.Review.ContainsKey(target.Text)&&!engine.Data.Spaced["ielts"].ContainsKey(target.Text),"fuzzy due word returns to learning");
        Home();Check(homePrimary.Content.ToString()=="继续复习"&&homeDue.Content.ToString()=="继续到期复习","unfinished due review has explicit continuation");var reloaded=new Engine(engine.FilePath);reloaded.LoadDictionary();reloaded.Load();Check(reloaded.PendingRound("due:ielts")!=null&&reloaded.PendingRound("due:ielts").SourceBookKey=="ielts","pending due review persists with source");
        bookChoice.SelectedIndex=0;Check(homePrimary.Content.ToString()=="开始","due review does not take over another book");bookChoice.SelectedIndex=3;homeDue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(engine.Data.Round.IsSpacedReview&&engine.Data.Round.Answers.Count==1,"due continuation restores position");
        Click("撤销");Check(engine.Progress("ielts").Mastered==2140&&engine.Data.Spaced["ielts"][target.Text].DueDate==previous,"undo after continuation restores mastery and due date");
        string spoken=speaker.LastWord;while(page=="test"){lastRate=DateTime.MinValue;Click("认识");}Check(page=="results"&&engine.DueWords("ielts",50000).Count==2,"only completed due words leave the current queue");Check(speaker.LastWord==spoken&&!speaker.Speaking,"advancing words never autoplays");Check(engine.PendingRound("ielts")==normal&&engine.Progress("ielts").Studied==2860,"review keeps cumulative learned count and normal progress");
    }
}
}
