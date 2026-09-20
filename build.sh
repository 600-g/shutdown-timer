#!/usr/bin/env bash
# 자동 종료 타이머 빌드 스크립트 (mono / Linux)
# 로컬에서도 GitHub Actions에서도 동일하게 이 스크립트로 빌드한다.
set -euo pipefail

cd "$(dirname "$0")"

OUT_EXE="AutoShutdownTimer.exe"

echo "== 컴파일 =="
mcs -sdk:4.5 -target:winexe -out:"$OUT_EXE" \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll \
  -win32res:src/app_full.res \
  -resource:src/SubR.ttf,PretendardR.ttf \
  -resource:src/SubSB.ttf,PretendardSB.ttf \
  -resource:src/AppIconEmbed.ico,AppIcon.ico \
  src/PhoneShell.cs

echo "== 패키징 (UTF-8 파일명 zip) =="
# zip 하나: AutoShutdownTimer.zip
#  - GitHub Release 자산이자 600g.net 이 찾는 이름. 이 이름을 바꾸면 사이트 다운로드가 끊긴다.
python3 - "$OUT_EXE" <<'PY'
import sys, zipfile, os
exe = sys.argv[1]
members = [(exe, exe),
           ("dist-extra/보안패치.bat", "보안패치.bat"),
           ("dist-extra/사용설명서.txt", "사용설명서.txt")]
def build(zipname):
    with zipfile.ZipFile(zipname, "w", zipfile.ZIP_DEFLATED) as z:
        for src, arc in members:
            zi = zipfile.ZipInfo(arc); zi.flag_bits |= 0x800; zi.compress_type = zipfile.ZIP_DEFLATED
            with open(src, "rb") as fp:
                z.writestr(zi, fp.read())
    print(zipname, "생성:", os.path.getsize(zipname), "bytes")
build("AutoShutdownTimer.zip")
PY

echo "== 완료 =="
ls -la "$OUT_EXE" AutoShutdownTimer.zip
