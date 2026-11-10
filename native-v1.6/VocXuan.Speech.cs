using System;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
namespace ShiCi {
public sealed class WordSpeaker : IDisposable {
    SpeechSynthesizer synth;
    readonly string outputFile;
    public string VoiceName { get; private set; }
    public string LastWord { get; private set; }
    public WordSpeaker(string testOutputFile=null) { outputFile=testOutputFile; }
    void Prepare() {
        if(synth!=null)return;
        var candidate=new SpeechSynthesizer();
        try {
            var voice=candidate.GetInstalledVoices().Where(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en").OrderBy(v=>v.VoiceInfo.Culture.Name=="en-US"?0:1).FirstOrDefault();
            if(voice==null)throw new InvalidOperationException("未找到英语语音。请在 Windows 语言设置中添加英语语音。");
            VoiceName=voice.VoiceInfo.Name;candidate.SelectVoice(VoiceName);candidate.Rate=-1;
            if(outputFile==null)candidate.SetOutputToDefaultAudioDevice();else{Directory.CreateDirectory(Path.GetDirectoryName(outputFile));candidate.SetOutputToWaveFile(outputFile);}
            synth=candidate;
        }catch{candidate.Dispose();throw;}
    }
    public void Speak(string word) { if(string.IsNullOrWhiteSpace(word))return;Prepare();synth.SpeakAsyncCancelAll();LastWord=word;synth.SpeakAsync(word); }
    public bool Speaking { get { return synth!=null&&synth.State!=SynthesizerState.Ready; } }
    public void Stop() { if(synth!=null)synth.SpeakAsyncCancelAll(); }
    public void Dispose() { if(synth==null)return;try{synth.SpeakAsyncCancelAll();}finally{synth.Dispose();synth=null;} }
}
}
