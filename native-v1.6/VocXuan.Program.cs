using System;
using System.IO;
using System.Linq;
using System.Windows;
namespace ShiCi {
public static class Program {
    public static void Migrate(string folder,string parent){
        string target=Path.Combine(folder,"progress.json");if(File.Exists(target))return;
        string[] paths={Path.Combine(parent,"VocXuan_v1.4_电脑样本","学习数据","progress.json"),Path.Combine(parent,"VocXuan_v1.3","学习数据","progress.json"),Path.Combine(parent,"VocXuan_v1.2","学习数据","progress.json"),Path.Combine(parent,"VocXuan_v1.1","学习数据","progress.json"),Path.Combine(parent,"识词客户端_v1.0","学习数据","progress.json"),Path.Combine(parent,"识词客户端","学习数据","progress.json")};
        string previous=paths.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();if(previous==null)return;
        // Copy only: the older program and all original records remain intact.
        Directory.CreateDirectory(folder);File.Copy(previous,target,false);
        if(File.Exists(previous+".bak"))File.Copy(previous+".bak",target+".bak",false);
    }
    [STAThread] public static int Main(string[] args){
        try{
            if(args.Length>1&&args[0]=="--export-icon"){RomanLogo.Export(args[1]);return 0;}
            if(args.Length>0&&args[0]=="--self-test")return Tests.Run(args.Length>1?args[1]:Path.GetTempPath());
            string qa=args.Length>1&&args[0]=="--qa"?args[1]:null;
            string home=AppDomain.CurrentDomain.BaseDirectory;
            string folder=qa!=null?Path.Combine(qa,"test-data"):Path.Combine(home,"学习数据");
            if(qa==null)Migrate(folder,Path.GetDirectoryName(home.TrimEnd(Path.DirectorySeparatorChar)));
            var engine=new Engine(Path.Combine(folder,"progress.json"));engine.LoadDictionary();engine.Load();
            var app=new Application();app.Run(new ReaderWindow(engine,qa));return 0;
        }catch(Exception ex){if(args.Length>1&&args[0]=="--qa"){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"qa-error.txt"),ex.ToString());return 1;}MessageBox.Show("VocXuan：\n"+ex.Message,"VocXuan");return 1;}
    }
}
}
