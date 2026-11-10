# VocXuan 1.6 每日统计源码

Windows: 在PowerShell运行 `./build-vocxuan.ps1`，输出vocxuan-build/VocXuan.exe。
检查使用独立目录，不要指向正式学习数据：
`./vocxuan-build/VocXuan.exe --self-test C:/temp/vocxuan-checks`
`node android/test-daily.cjs C:/temp/vocxuan-checks`
`node android/test-spaced.cjs C:/temp/vocxuan-checks/spaced.json`
再次运行self-test会追加Android到Windows的格式互读检查。
界面检查：`./vocxuan-build/VocXuan.exe --qa C:/temp/vocxuan-ui-checks`。

Android: Python 3、JDK 17、Android Build Tools 35.0.0及android-35/android.jar。
build-v16.py的--tools目录下使用以下子目录：jdk17、build-tools-35.0.0、platforms-android-35。
`python android/build-v16.py --tools YOUR_SDK_FOLDER --signing-dir PRIVATE_SIGNING_FOLDER --output VocXuan_Android_v1.6.apk`
签名目录须含既有vocxuan-release.p12和signing.password，不会生成替代证书。
SDK/JDK及私人签名材料不在源码包中。

统计规则：每日按本地日历分桶，WordCounts记录每词有效作答次数，词数由正计数字典去重计算。
每个新答案携带StudyDay，撤销只回退原日期；旧答案没有日期，不补算也不扣当天。
计时器只上报新累计的前台有效区间，按零点切分；旧轮次总时长不作为每日统计基数。
v5存档升级到v6时保留进度，不编造历史；Daily与DailyStartedOn在C#和JS之间兼容。
