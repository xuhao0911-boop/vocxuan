using System;
using System.Text.RegularExpressions;
namespace ShiCi {
public static class Phonetics {
    public static string Normalize(string value) {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string p=value.Trim().Trim('/','[',']').Trim();
        if (Regex.IsMatch(p,@"[\\?@\uFFFD\u4E00-\u9FFF^]")) return "";
        p=p.Replace("ә:","ɜː").Replace("ə:","ɜː").Replace("ә","ə").Replace("є","e").Replace(":","ː").Replace("'","ˈ");
        p=Regex.Replace(p,@"(^|\s)[.,](?=[a-zæɑɔɒəɛʃθʒðŋɪʊʌ])","$1ˌ");
        return p;
    }
    public static string Display(string value) { string p=Normalize(value); return p.Length==0?"":"/ "+p+" /"; }
}
}
