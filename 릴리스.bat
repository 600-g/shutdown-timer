@echo off
setlocal
cd /d "%~dp0"

rem ── 자동 종료 타이머 릴리스 (더블클릭 한 번) ──
rem  1) src\PhoneShell.cs 의 build 번호를 읽어 v1.0.NN 태그를 만들고
rem  2) 커밋 + push + 태그 push → GitHub Actions 가 빌드해서 Releases 에 zip 자동 업로드

where git >nul 2>nul || (echo [오류] git 이 설치돼 있지 않습니다. https://git-scm.com 에서 설치 후 다시 실행 & pause & exit /b 1)
if not exist ".git" (echo [오류] 이 폴더는 git 저장소가 아닙니다. README.md 의 "최초 업로드" 를 먼저 진행하세요. & pause & exit /b 1)

set BUILD=
for /f "tokens=2 delims=()" %%a in ('findstr /c:"VERSION = " src\PhoneShell.cs') do set VERLINE=%%a
for /f "tokens=2" %%b in ("%VERLINE%") do set BUILD=%%b
if "%BUILD%"=="" (echo [오류] 소스에서 build 번호를 찾지 못했습니다. & pause & exit /b 1)

set TAG=v1.0.%BUILD%
echo.
echo  릴리스 태그 : %TAG%
echo.

git add -A
git commit -m "%TAG%" >nul 2>nul || echo  (변경 사항 없음 - 태그만 갱신)
git tag -f %TAG% >nul
git push || (echo [오류] push 실패. 로그인/원격 저장소 설정을 확인하세요. & pause & exit /b 1)
git push -f origin %TAG% || (echo [오류] 태그 push 실패 & pause & exit /b 1)

for /f "tokens=*" %%u in ('git remote get-url origin') do set REMOTE=%%u
set REMOTE=%REMOTE:.git=%
echo.
echo  완료. 1~2분 뒤 아래에서 확인:
echo   빌드 진행 : %REMOTE%/actions
echo   릴리스     : %REMOTE%/releases/latest
echo.
pause
