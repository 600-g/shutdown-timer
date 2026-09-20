@echo off
setlocal
cd /d "%~dp0"

rem ==== 자동 종료 타이머 릴리스 (더블클릭 한 번) ====
rem  main 에 push 만 하면 GitHub Actions 가 소스의 build 번호를 읽어
rem  v1.0.NN 태그 + Release(zip) 를 자동으로 만든다. 600g.net 카드도 자동으로 따라온다.
rem  (번호가 이미 릴리스된 것이면 push 전에 멈추고 알려준다)

where git >nul 2>nul || (echo [오류] git 이 설치돼 있지 않습니다. https://git-scm.com 에서 설치 후 다시 실행 & pause & exit /b 1)
if not exist ".git" (echo [오류] 이 폴더는 git 저장소가 아닙니다. README.md 의 "최초 업로드" 를 먼저 진행하세요. & pause & exit /b 1)

set VERLINE=
for /f "tokens=2 delims==" %%a in ('findstr /c:"VERSION = " src\PhoneShell.cs') do set VERLINE=%%a
for /f "tokens=1 delims=;" %%b in ("%VERLINE%") do set VER=%%~b
set VER=%VER: =%
set VER=%VER:"=%
if "%VER%"=="" (echo [오류] 소스에서 버전을 찾지 못했습니다. & pause & exit /b 1)

set TAG=v%VER%
git ls-remote --exit-code --tags origin refs/tags/%TAG% >nul 2>nul
if not errorlevel 1 (
    echo [중지] %TAG% 는 이미 릴리스돼 있습니다.
    echo        src\PhoneShell.cs 의 VERSION 을 올리고 CHANGELOG.md 에 항목을 추가한 뒤 다시 실행하세요.
    pause
    exit /b 1
)

echo.
echo  릴리스할 버전 : %TAG%
echo.

git add -A
git commit -m "%TAG%" >nul 2>nul || echo  (변경 사항 없음 - push 만 진행)
git push || (echo [오류] push 실패. 로그인/네트워크 상태를 확인하세요. & pause & exit /b 1)

for /f "tokens=*" %%u in ('git remote get-url origin') do set REMOTE=%%u
set REMOTE=%REMOTE:.git=%
echo.
echo  완료. GitHub Actions 가 빌드해서 1~2분 뒤 Release 가 생깁니다:
echo   진행 상황 : %REMOTE%/actions
echo   다운로드   : %REMOTE%/releases/latest
echo   600g.net 카드는 최대 10분 안에 새 버전으로 바뀝니다.
echo.
pause
