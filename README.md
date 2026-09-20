# 자동 종료 타이머 (ⓒ 600g)

설정한 시간이 지나면 PC 종료 · 특정 프로그램 종료 · 알림을 띄우는 Windows 앱.

---

## 릴리스 = `main` 에 새 소스 올리기 (그게 전부)

`src/PhoneShell.cs` 가 `main` 에 올라오면 GitHub Actions 가 알아서:
1. 소스의 `VERSION = "v1.0 (build NN)"` 을 읽어 **`v1.0.NN` 태그가 아직 없으면** 빌드 시작
2. 리눅스에서 mono 로 exe 빌드 → 보안패치.bat · 사용설명서.txt 와 zip 으로 묶기
3. **Releases 에 `v1.0.NN` 자동 생성 + `AutoShutdownTimer.zip` 첨부**
4. 600g.net 의 "종료타이머&알림" 카드가 최신 Release 를 자동으로 따라옴 (최대 10분)

번호가 이미 릴리스된 것과 같은데 소스만 바뀌었으면 **실패(빨간 X)** 로 표시해 알려줍니다 → 번호를 올려서 다시 올리면 됩니다.

### 방법 A — GitHub 웹에서 업로드 (맥·git 필요 없음) ★ 기본

1. Claude 등에서 새 `PhoneShell.cs` 를 만들 때 **`VERSION` 의 build 번호를 하나 올리라고** 같이 지시
2. https://github.com/600-g/shutdown-timer/tree/main/src 열기 → **Add file → Upload files**
3. 새 `PhoneShell.cs` 를 끌어다 놓고 **Commit changes** (main 에 직접)
4. 1~2분 뒤 **Releases** 탭에 새 버전, 이어서 600g.net 카드 갱신

### 방법 B — 윈도우에서 `릴리스.bat` 더블클릭

새 `PhoneShell.cs` 를 `src\` 에 덮어쓴 뒤 실행. 커밋·push 만 하고 태그·Release 는 Actions 가 만듭니다.
번호가 이미 릴리스된 것이면 push 전에 멈추고 알려줍니다.

### 방법 C — 터미널

```bash
git add -A && git commit -m "v1.0.70" && git push
```

태그는 안 만들어도 됩니다 (Actions 가 만듦). 예전처럼 `git tag v1.0.70 && git push origin v1.0.70` 해도 똑같이 동작합니다.

### 빌드만 확인하고 싶을 때

저장소 **Actions** 탭 → `build-and-release` → **Run workflow**. zip 이 그 실행의 **Artifacts** 에 올라옵니다.
"태그가 없으면 Release 까지 생성" 을 켜면 방법 A 와 같은 릴리스가 됩니다.

> 최신 릴리스 고정 다운로드 주소:
> `https://github.com/600-g/shutdown-timer/releases/latest/download/AutoShutdownTimer.zip`

### 600g.net 연동 (2026-09-15 완료)

600g.net 허브의 **"종료타이머&알림" 카드는 이 저장소의 최신 Release 에 연결**돼 있다
(두근컴퍼니 백엔드 `apps.json` — `source_repo: 600-g/shutdown-timer`, `source_asset: AutoShutdownTimer.zip`).

- 카드의 버전·용량은 최신 Release 에서 자동으로 읽는다 (10분 캐시)
- [받기] 버튼은 항상 `…/releases/latest/download/AutoShutdownTimer.zip` 으로 보낸다
- 허브 관리 화면에 따로 올리지 말 것 (이중 관리)
- 에셋 이름 `AutoShutdownTimer.zip` 을 바꾸면 허브 연결이 끊긴다 (`build.sh` · `build.yml` 과 함께 유지)
- Actions 는 `src/` · `dist-extra/` · `build.sh` · 워크플로 파일이 바뀔 때만 돈다 (README 만 고치면 안 돎)

### 처음 한 번만 세팅 (2026-09-15 완료)

저장소 → **Settings → Actions → General → Workflow permissions** 가 **Read and write** 여야 Release 를 만들 수 있다. 현재 설정돼 있음.

---

## 최초 업로드 (저장소가 비어있을 때)

```bash
cd (이 폴더)
git init
git add -A
git commit -m "최초 커밋"
git branch -M main
git remote add origin https://github.com/<사용자명>/<저장소명>.git
git push -u origin main
```

이미 저장소가 있다면 이 폴더의 파일들을 그 저장소에 복사해 넣고 커밋하면 됩니다.

---

## 폴더 구조

```
src/                소스 + 빌드 리소스
  PhoneShell.cs       메인 소스 (C# WinForms)
  SubR.ttf SubSB.ttf  내장 폰트(Pretendard 서브셋)
  app_full.res        Win32 리소스(관리자 매니페스트 + 아이콘)
  AppIconEmbed.ico    창/작업표시줄 아이콘
dist-extra/         배포 zip에 함께 넣는 파일
  보안패치.bat        최초 1회 실행용 (다운로드 차단 해제 + 백신 예외)
  사용설명서.txt
build.sh            빌드 스크립트 (로컬/Actions 공통)
릴리스.bat          윈도우에서 더블클릭 → 커밋·push (태그·Release 는 Actions 가)
.github/workflows/build.yml   자동 빌드·배포 설정
```

## 로컬에서 직접 빌드 (선택)

mono가 깔린 환경에서:

```bash
./build.sh
```

→ `AutoShutdownTimer.exe` 와 `AutoShutdownTimer.zip` 이 생깁니다.

---
ⓒ 600g · 600g.net
