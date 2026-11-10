package com.vocxuan.app;

import android.app.Activity;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;
import java.util.Locale;
import java.util.Set;
import java.util.function.Consumer;

final class WordSpeech {
    private final Activity owner;
    private final Consumer<String> error;
    private final Consumer<Boolean> playing;
    private TextToSpeech tts;
    private boolean ready,closed,visible=true;
    private String pending,active;
    private long sequence;
    WordSpeech(Activity owner,Consumer<String> error,Consumer<Boolean> playing){this.owner=owner;this.error=error;this.playing=playing;}
    void speak(String word){
        if(closed||!visible||word==null||!word.matches("[A-Za-z][A-Za-z' -]{0,79}"))return;
        pending=word;
        if(tts==null){
            tts=new TextToSpeech(owner,status->owner.runOnUiThread(()->{
                if(closed)return;
                if(status!=TextToSpeech.SUCCESS){pending=null;if(tts!=null){tts.shutdown();tts=null;}error.accept("手机文字转语音服务暂不可用，请在系统设置中启用英语语音。");return;}
                ready=true;tts.setSpeechRate(.85f);
                tts.setOnUtteranceProgressListener(new UtteranceProgressListener(){
                    @Override public void onStart(String id){owner.runOnUiThread(()->{if(id.equals(active)&&visible&&!closed)playing.accept(true);});}
                    @Override public void onDone(String id){owner.runOnUiThread(()->{if(id.equals(active)){active=null;playing.accept(false);}});}
                    @Override public void onError(String id){owner.runOnUiThread(()->{if(id.equals(active)&&visible&&!closed){active=null;playing.accept(false);error.accept("英语发音失败，请检查系统英语语音是否已安装。");}});}
                });
                playPending();
            }));
        }else if(ready)playPending();
    }
    private void playPending(){
        if(closed||!visible||pending==null||!ready)return;
        Voice chosen=null;Set<Voice> voices=tts.getVoices();
        if(voices!=null)for(Voice voice:voices){
            if(!"en".equals(voice.getLocale().getLanguage())||voice.isNetworkConnectionRequired()||voice.getFeatures()!=null&&voice.getFeatures().contains(TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED))continue;
            if(chosen==null||score(voice)>score(chosen))chosen=voice;
        }
        if(chosen==null){
            int available=tts.setLanguage(Locale.US);Voice current=tts.getVoice();
            if(available<TextToSpeech.LANG_AVAILABLE||current==null||current.isNetworkConnectionRequired()||!"en".equals(current.getLocale().getLanguage())){pending=null;error.accept("英语离线语音尚未安装，请在系统“文字转语音”设置中添加英语语音。");return;}
            chosen=current;
        }
        if(tts.setVoice(chosen)!=TextToSpeech.SUCCESS){pending=null;error.accept("英语语音暂不可用，请检查系统文字转语音设置。");return;}
        String word=pending;pending=null;active="vocxuan-"+(++sequence);
        if(tts.speak(word,TextToSpeech.QUEUE_FLUSH,null,active)==TextToSpeech.ERROR){active=null;playing.accept(false);error.accept("英语发音未能开始，请检查系统语音设置。");}
    }
    private int score(Voice voice){return ("US".equals(voice.getLocale().getCountry())?10000:0)+voice.getQuality()-voice.getLatency();}
    void stop(){pending=null;active=null;if(tts!=null)tts.stop();playing.accept(false);}
    void visible(boolean value){visible=value;if(!value)stop();}
    void close(){closed=true;stop();if(tts!=null){tts.shutdown();tts=null;}}
}
