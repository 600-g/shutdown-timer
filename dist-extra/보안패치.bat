@echo off
title 자동 종료 타이머 - 보안 패치

REM ===== 관리자 권한으로 자기 자신을 다시 실행 =====
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo 관리자 권한이 필요합니다.
    echo 권한 요청 창에서 "예"를 눌러주세요...
    powershell -Command "Start-Process -Verb RunAs -FilePath '%~f0'"
    exit /b
)

cd /d "%~dp0"

echo ============================================================
echo   자동 종료 타이머 - 보안 패치
echo   (이 창은 처음 한 번만 실행하면 됩니다)
echo ============================================================
echo.

set "EXE=%~dp0자동종료타이머.exe"

if not exist "%EXE%" (
    echo [오류] 자동종료타이머.exe 를 찾을 수 없습니다.
    echo        이 배치파일과 exe 를 같은 폴더에 두고 실행하세요.
    echo.
    pause
    exit /b
)

echo [1/3] 다운로드 차단 표시 제거 중...
powershell -Command "Unblock-File -Path '%EXE%'" 2>nul
if exist "%EXE%:Zone.Identifier" del "%EXE%:Zone.Identifier" 2>nul
echo       완료.
echo.

echo [2/3] 보안 프로그램 예외(신뢰) 등록 중...
powershell -Command "Add-MpPreference -ExclusionPath '%EXE%'" 2>nul
powershell -Command "Add-MpPreference -ExclusionPath '%~dp0'" 2>nul
echo       완료. (다른 백신을 쓰신다면 그 백신에도 직접 예외 등록을 권장합니다)
echo.

echo [3/3] 준비 완료!
echo.
echo   이제 자동종료타이머.exe 를 실행하시면 됩니다.
echo   설정과 알림 저장도 정상 동작합니다.
echo.
echo ============================================================
echo.
choice /C YN /M "지금 바로 자동 종료 타이머를 실행할까요"
if errorlevel 2 goto end
start "" "%EXE%"

:end
echo.
echo 창을 닫아도 됩니다.
pause >nul
