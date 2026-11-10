package com.vocxuan.app;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.util.AtomicFile;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import org.json.JSONObject;
import java.io.*;
import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.nio.charset.Charset;
import java.util.Collections;

public final class MainActivity extends Activity {
    private WebView web;
    private AtomicFile state;
    private String exportText;
    private WordSpeech speech;
    private static final int IMPORT=10, EXPORT=11;
    private static final int PAPER=0xfff7f8f5;

    @Override public void onCreate(Bundle saved){
        super.onCreate(saved);
        state=new AtomicFile(new File(getFilesDir(),"progress.json"));
        speech=new WordSpeech(this,message->tell(message),playing->js("window.VocXuanNative&&window.VocXuanNative.speechState("+playing+")"));
        getWindow().setStatusBarColor(PAPER);getWindow().setNavigationBarColor(PAPER);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE|View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN|View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION|View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);
        if(Build.VERSION.SDK_INT>=30)getWindow().setDecorFitsSystemWindows(false);
        FrameLayout root=new FrameLayout(this);root.setBackgroundColor(PAPER);
        root.setOnApplyWindowInsetsListener((v,insets)->{
            if(Build.VERSION.SDK_INT>=30){android.graphics.Insets bars=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());android.graphics.Insets ime=insets.getInsets(WindowInsets.Type.ime());v.setPadding(bars.left,bars.top,bars.right,Math.max(bars.bottom,ime.bottom));}
            else v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());
            return insets;
        });
        web=new WebView(this);web.setBackgroundColor(PAPER);web.setOverScrollMode(View.OVER_SCROLL_NEVER);
        WebSettings settings=web.getSettings();settings.setJavaScriptEnabled(true);settings.setDomStorageEnabled(true);settings.setDefaultTextEncodingName("UTF-8");settings.setAllowFileAccess(false);settings.setAllowContentAccess(false);settings.setAllowFileAccessFromFileURLs(false);settings.setAllowUniversalAccessFromFileURLs(false);settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setTextZoom(Math.min(160,Math.max(90,Math.round(getResources().getDisplayMetrics().scaledDensity/getResources().getDisplayMetrics().density*100))));
        web.addJavascriptInterface(new Bridge(),"AndroidBridge");
        web.setWebViewClient(new WebViewClient(){
            @Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest request){return !local(request.getUrl().toString());}
            @Override public boolean shouldOverrideUrlLoading(WebView view,String url){return !local(url);}
            @Override public WebResourceResponse shouldInterceptRequest(WebView view,WebResourceRequest request){
                if(local(request.getUrl().toString())||request.getUrl().toString().startsWith("data:image/"))return null;
                return new WebResourceResponse("text/plain","UTF-8",403,"Local content only",Collections.emptyMap(),new ByteArrayInputStream(new byte[0]));
            }
        });
        root.addView(web,new FrameLayout.LayoutParams(-1,-1));setContentView(root);root.requestApplyInsets();
        web.loadUrl("file:///android_asset/index.html");
        if(Build.VERSION.SDK_INT>=33)getOnBackInvokedDispatcher().registerOnBackInvokedCallback(android.window.OnBackInvokedDispatcher.PRIORITY_DEFAULT,()->handleBack());
    }
    private boolean local(String url){return url.startsWith("file:///android_asset/");}
    private void js(String code){if(web!=null)web.evaluateJavascript(code,null);}
    private void tell(String message){js("window.VocXuanNative&&window.VocXuanNative.alert("+JSONObject.quote(message)+")");}
    private void handleBack(){js("window.VocXuanNative&&window.VocXuanNative.back()");}
    @Override public void onBackPressed(){handleBack();}
    @Override protected void onPause(){if(speech!=null)speech.visible(false);js("window.VocXuanNative&&window.VocXuanNative.lifecycle(false)");if(web!=null)web.onPause();super.onPause();}
    @Override protected void onResume(){super.onResume();if(speech!=null)speech.visible(true);if(web!=null){web.onResume();js("window.VocXuanNative&&window.VocXuanNative.lifecycle(true)");}}
    @Override protected void onDestroy(){if(speech!=null)speech.close();if(web!=null){web.removeJavascriptInterface("AndroidBridge");web.destroy();web=null;}super.onDestroy();}

    private final class Bridge {
        @JavascriptInterface public synchronized String getState(){
            if(!state.getBaseFile().exists()&&!new File(state.getBaseFile().getPath()+".bak").exists())return "";
            try(InputStream in=state.openRead()){String text=new String(read(in,64*1024*1024),StandardCharsets.UTF_8);new JSONObject(text);return text;}
            catch(Exception error){
                try(InputStream in=new FileInputStream(state.getBaseFile());OutputStream out=new FileOutputStream(new File(getFilesDir(),"progress-preserved-"+System.currentTimeMillis()+".json"))){byte[] block=new byte[8192];int n;while((n=in.read(block))>=0)out.write(block,0,n);}catch(Exception ignored){}
                return "{\"__loadError\":true}";
            }
        }
        @JavascriptInterface public synchronized boolean putState(String text){
            FileOutputStream out=null;
            try{if(text.length()>64*1024*1024)return false;JSONObject parsed=new JSONObject(text);if(!parsed.has("Review")||!parsed.has("Imported"))return false;out=state.startWrite();out.write(text.getBytes(StandardCharsets.UTF_8));state.finishWrite(out);return true;}
            catch(Exception error){if(out!=null)state.failWrite(out);return false;}
        }
        @JavascriptInterface public void importFile(){runOnUiThread(()->{Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("*/*");startActivityForResult(intent,IMPORT);});}
        @JavascriptInterface public void exportFile(String name,String text){runOnUiThread(()->{exportText=text;Intent intent=new Intent(Intent.ACTION_CREATE_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("text/csv");intent.putExtra(Intent.EXTRA_TITLE,name.replaceAll("[\\\\/:*?\"<>|]","_"));startActivityForResult(intent,EXPORT);});}
        @JavascriptInterface public void closeApp(){runOnUiThread(()->finish());}
        @JavascriptInterface public void speakWord(String word){runOnUiThread(()->{if(speech!=null)speech.speak(word);});}
        @JavascriptInterface public void stopSpeech(){runOnUiThread(()->{if(speech!=null)speech.stop();});}
    }
    private static byte[] read(InputStream in,int limit)throws IOException{ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] block=new byte[8192];int n;while((n=in.read(block))>=0){if(out.size()+n>limit)throw new IOException("文件过大");out.write(block,0,n);}return out.toByteArray();}
    private static String decode(byte[] bytes)throws CharacterCodingException{
        if(bytes.length>1&&(bytes[0]&255)==255&&(bytes[1]&255)==254)return new String(bytes,StandardCharsets.UTF_16LE);
        if(bytes.length>1&&(bytes[0]&255)==254&&(bytes[1]&255)==255)return new String(bytes,StandardCharsets.UTF_16BE);
        try{return StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(bytes)).toString();}
        catch(CharacterCodingException error){return new String(bytes,Charset.forName("GB18030"));}
    }
    @Override protected void onActivityResult(int request,int result,Intent data){
        super.onActivityResult(request,result,data);if(result!=RESULT_OK||data==null||data.getData()==null){if(request==EXPORT)exportText=null;return;}Uri uri=data.getData();
        if(request==IMPORT){try(InputStream in=getContentResolver().openInputStream(uri)){String text=decode(read(in,8*1024*1024));js("window.VocXuanNative.importText("+JSONObject.quote(text)+")");}catch(Exception error){tell("导入失败："+error.getMessage());}}
        else if(request==EXPORT&&exportText!=null){try(OutputStream out=getContentResolver().openOutputStream(uri)){out.write(exportText.getBytes(StandardCharsets.UTF_8));}catch(Exception error){tell("导出失败："+error.getMessage());}finally{exportText=null;}}
    }
}
