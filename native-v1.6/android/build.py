from pathlib import Path
import os, subprocess, zipfile, secrets, json, hashlib

ROOT=Path(__file__).parent.resolve()
TOOLS=Path(r'D:\CodexTools\android-vocxuan')
JDK=Path(r'C:\Program Files\JetBrains\PyCharm Community Edition 2020.3.3\jbr\bin')
BUILD=TOOLS/'build-tools-35.0.0/android-15'
ANDROID=TOOLS/'platforms-android-35/android-35/android.jar'
OUT=ROOT/'build'; OUT.mkdir(exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
DEX=OUT/'dex';DEX.mkdir(exist_ok=True)
FINAL=ROOT.parent.parent/'outputs/VocXuan_Android_v1.5.apk'

def run(args):
    proc=subprocess.run([str(x) for x in args],capture_output=True,text=True,encoding='utf-8',errors='replace')
    if proc.returncode:raise RuntimeError(proc.stdout+'\n'+proc.stderr)
    return proc.stdout+proc.stderr

run([BUILD/'aapt2.exe','compile','--dir',ROOT/'res','-o',OUT/'resources.zip'])
run([BUILD/'aapt2.exe','link','-o',OUT/'base.apk','-I',ANDROID,'--manifest',ROOT/'AndroidManifest.xml','-A',ROOT/'assets',OUT/'resources.zip'])
for old in CLASSES.glob('**/*.class'):old.unlink()
for old in DEX.glob('*.dex'):old.unlink()
run([JDK/'javac.exe','-encoding','UTF-8','--release','8','-classpath',ANDROID,'-d',CLASSES,*ROOT.glob('src/**/*.java')])
run([JDK/'java.exe','-cp',BUILD/'lib/d8.jar','com.android.tools.r8.D8','--lib',ANDROID,'--min-api','26','--output',DEX,*CLASSES.glob('**/*.class')])
with zipfile.ZipFile(OUT/'base.apk','a',compression=zipfile.ZIP_DEFLATED) as archive:
    for dex in DEX.glob('*.dex'):archive.write(dex,dex.name,compress_type=zipfile.ZIP_STORED)
run([BUILD/'zipalign.exe','-f','4',OUT/'base.apk',OUT/'aligned.apk'])
password=TOOLS/'signing.password';keystore=TOOLS/'vocxuan-release.p12'
if not password.exists():password.write_text(secrets.token_urlsafe(32),encoding='ascii')
if not keystore.exists():
    run([JDK/'keytool.exe','-genkeypair','-keystore',keystore,'-storetype','PKCS12','-storepass:file',password,'-keypass:file',password,'-alias','vocxuan','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=VocXuan'])
run([JDK/'java.exe','-jar',BUILD/'lib/apksigner.jar','sign','--ks',keystore,'--ks-key-alias','vocxuan','--ks-pass','file:'+str(password),'--v4-signing-enabled','false','--out',FINAL,OUT/'aligned.apk'])
sidecar=Path(str(FINAL)+'.idsig')
if sidecar.exists():sidecar.unlink()
verification=run([JDK/'java.exe','-jar',BUILD/'lib/apksigner.jar','verify','--verbose','--print-certs',FINAL])
run([BUILD/'zipalign.exe','-c','-P','16','4',FINAL])
assert '87de3eba98fa653c54cb49aa98261ecb53becec48ffc5aa7aa4412777f78dd1c' in verification,'Signing certificate must match v1.3 for in-place upgrades'
badging=run([BUILD/'aapt.exe','dump','badging',FINAL])
(OUT/'apk-verification.txt').write_text(verification+'\n'+badging,encoding='utf-8')
with zipfile.ZipFile(FINAL) as archive:
    assert archive.testzip() is None
    assert archive.read('classes.dex').startswith(b'dex\n')
    for source in (ROOT/'assets').rglob('*'):
        if source.is_file():assert archive.read('assets/'+source.relative_to(ROOT/'assets').as_posix())==source.read_bytes()
    assert archive.getinfo('resources.arsc').compress_type==zipfile.ZIP_STORED
    assert not any(n.endswith('.so') for n in archive.namelist())
assert "sdkVersion:'26'" in badging
assert "targetSdkVersion:'35'" in badging
assert 'uses-permission:' not in badging
assert "versionCode='10500'" in badging
report={'file':str(FINAL),'bytes':FINAL.stat().st_size,'sha256':hashlib.sha256(FINAL.read_bytes()).hexdigest(),'signatures_verified':True,'minimum_android':'8.0 (API 26)','target_sdk':35,'version':'1.5.0','permissions':[],'same_signing_certificate_as_v1_3':True,'native_libraries':[]}
(OUT/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
