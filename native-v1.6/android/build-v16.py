from pathlib import Path
import argparse, subprocess, zipfile, json, hashlib, tempfile, shutil

ROOT=Path(__file__).resolve().parent
parser=argparse.ArgumentParser(description='Build VocXuan Android 1.6 with the existing signing key.')
parser.add_argument('--tools',type=Path,required=True)
parser.add_argument('--signing-dir',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
TOOLS=args.tools.resolve()
JDK=next((TOOLS/'jdk17').glob('*/bin/javac.exe')).parent
BUILD=next((TOOLS/'build-tools-35.0.0').rglob('aapt2.exe')).parent
ANDROID=next((TOOLS/'platforms-android-35').rglob('android.jar'))
FINAL=args.output.resolve();FINAL.parent.mkdir(parents=True,exist_ok=True)
(ROOT/'build').mkdir(exist_ok=True)
OUT=Path(tempfile.mkdtemp(prefix='v16-',dir=ROOT/'build'))
PACKAGED=OUT/'VocXuan_Android_v1.6.apk'
CLASSES=OUT/'classes';CLASSES.mkdir()
DEX=OUT/'dex';DEX.mkdir()
password=args.signing_dir/'signing.password';keystore=args.signing_dir/'vocxuan-release.p12'
if not password.is_file() or not keystore.is_file():
    raise RuntimeError('The existing signing files are required; no replacement certificate will be generated.')

def run(items):
    result=subprocess.run([str(x) for x in items],capture_output=True,text=True,encoding='utf-8',errors='replace',creationflags=0x08000000)
    if result.returncode:raise RuntimeError(result.stdout+'\n'+result.stderr)
    return result.stdout+result.stderr

run([BUILD/'aapt2.exe','compile','--dir',ROOT/'res','-o',OUT/'resources.zip'])
run([BUILD/'aapt2.exe','link','-o',OUT/'base.apk','-I',ANDROID,'--manifest',ROOT/'AndroidManifest.xml','-A',ROOT/'assets',OUT/'resources.zip'])
run([JDK/'javac.exe','-encoding','UTF-8','--release','8','-classpath',ANDROID,'-d',CLASSES,*ROOT.glob('src/**/*.java')])
run([JDK/'java.exe','-cp',BUILD/'lib/d8.jar','com.android.tools.r8.D8','--lib',ANDROID,'--min-api','26','--output',DEX,*CLASSES.rglob('*.class')])
with zipfile.ZipFile(OUT/'base.apk','a',compression=zipfile.ZIP_DEFLATED) as archive:
    for dex in DEX.glob('*.dex'):archive.write(dex,dex.name,compress_type=zipfile.ZIP_STORED)
run([BUILD/'zipalign.exe','-f','4',OUT/'base.apk',OUT/'aligned.apk'])
run([JDK/'java.exe','-jar',BUILD/'lib/apksigner.jar','sign','--ks',keystore,'--ks-key-alias','vocxuan','--ks-pass','file:'+str(password),'--v4-signing-enabled','false','--out',PACKAGED,OUT/'aligned.apk'])
verification=run([JDK/'java.exe','-jar',BUILD/'lib/apksigner.jar','verify','--verbose','--print-certs',PACKAGED])
run([BUILD/'zipalign.exe','-c','-P','16','4',PACKAGED])
certificate='87de3eba98fa653c54cb49aa98261ecb53becec48ffc5aa7aa4412777f78dd1c'
assert certificate in verification,'Signing certificate must match previous versions for in-place upgrades'
badging=run([BUILD/'aapt.exe','dump','badging',PACKAGED])
(OUT/'apk-verification.txt').write_text(verification+'\n'+badging,encoding='utf-8')
with zipfile.ZipFile(PACKAGED) as archive:
    assert archive.testzip() is None
    assert archive.read('classes.dex').startswith(b'dex\n')
    for source in (ROOT/'assets').rglob('*'):
        if source.is_file():assert archive.read('assets/'+source.relative_to(ROOT/'assets').as_posix())==source.read_bytes()
    assert archive.getinfo('resources.arsc').compress_type==zipfile.ZIP_STORED
    assert not any(n.endswith('.so') for n in archive.namelist())
assert "sdkVersion:'26'" in badging
assert "targetSdkVersion:'35'" in badging
assert "versionCode='10600'" in badging and "versionName='1.6.0'" in badging
assert 'uses-permission:' not in badging
shutil.copy2(PACKAGED,FINAL)
report={'file':str(FINAL),'bytes':FINAL.stat().st_size,'sha256':hashlib.sha256(FINAL.read_bytes()).hexdigest(),
        'version':'1.6.0','versionCode':10600,'minimum_android':'8.0 (API 26)','target_sdk':35,
        'signature_verified':True,'certificate_sha256':certificate,'same_signing_certificate_as_previous':True,
        'assets_verified_against_source':True,'permissions':[], 'physical_phone_tested':False}
(OUT/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
FINAL.with_suffix('.build.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
