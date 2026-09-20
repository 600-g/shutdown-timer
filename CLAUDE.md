# 자동 종료 타이머 — AI 작업 규칙

이 저장소를 고치는 AI(클로드 코드 · claude.ai/code · 기타)는 아래를 반드시 지킨다.

## ★ 1번 규칙: 버전을 올리고 CHANGELOG 를 쓴다

`src/PhoneShell.cs` 를 **조금이라도 고쳤다면** 같은 커밋에서 둘 다 한다.

```csharp
private const string VERSION = "1.0.0";   // → 버그 수정이면 1.0.1, 기능 추가면 1.1.0
```

```markdown
## 1.0.1            ← CHANGELOG.md 의 <!-- RELEASES --> 바로 아래에 추가
- 무엇이 바뀌었는지 사용자 말로 한 줄씩
```

`VERSION` 이 곧 릴리스 태그(`v1.0.1`)이고, CHANGELOG 의 그 절이 **릴리스 본문이자 앱 안 업데이트 창에 그대로 뜬다.**
둘 중 하나라도 빠지면 Actions 가 **실패(빨간 X)** 로 막고 배포가 안 나간다.

올리는 기준: 버그·안정화는 patch(1.0.**1**), 기능 추가는 minor(1.**1**.0), 크게 뒤집으면 major(**2**.0.0).
**끝자리만 비교하면 안 된다** — 앱의 `Updater.ParseVer` 가 자리별로 비교한다(1.1.0 > 1.0.9).

## 배포 구조 — main 에 올리면 끝

```
src/PhoneShell.cs 수정 → main push → Actions(mono 빌드) → GitHub Release v1.0.NN
                                    → 600g.net "종료타이머&알림" 카드가 자동 추적 (최대 10분)
```

- 태그를 직접 만들 필요 없다. Actions 가 번호를 읽어 태그까지 만든다.
- 올리는 방법 아무거나: GitHub 웹 Upload files(권장, git 불필요) · `릴리스.bat` 더블클릭 · `git push`
- `README.md` 나 문서만 고치면 빌드가 돌지 않는다(의도된 동작).

## 어디서 고치든 상관없다 — 단, 로컬 사본만 조심

claude.ai 로 고치는 한 **기기는 상관없다.** 윈도우·폰·맥 브라우저 어디서 하든 똑같이
GitHub 의 `main` 에 직접 커밋되고, 그 뒤 파이프라인도 똑같이 돈다. GitHub 이 순서를 정리해 주므로
서로 엉킬 일도 없다. 계정에 붙은 권한이라 기기마다 설치할 것도 없다.

**위험한 건 오직 로컬 클론이다.** 맥의 `~/Developer/shutdown-timer` 처럼 디스크에 받아둔 사본은
claude.ai 가 GitHub 을 고치는 순간 뒤처지고, 그 상태로 고쳐서 밀면 남의 수정을 덮어쓴다.

- **claude.ai 로 하는 작업**: 기기 제한 없음. 아무 데서나.
- **로컬 클론에서 하는 작업**(맥 터미널, 윈도우 릴리스.bat): 반드시 `git pull` 먼저, 끝나면 바로 push.
- 충돌이 나도 `--force` 로 밀지 말 것. 릴리스 태그가 커밋과 어긋나면 되돌리기 어렵다.
- 가장 안전한 습관은 **로컬 클론을 아예 안 쓰는 것** — claude.ai 만 쓰면 충돌 자체가 없다.

## ⚠️ 맥이 꺼지면 600g.net 다운로드가 멈춘다

관리는 윈도우에서 하더라도 **배포 자체는 맥에 묶여 있다.** 600g.net 의 앱 카드는
`api.600g.net`(맥의 company-hq 서버 + cloudflared)에서 목록을 받아오고, [받기] 도 그 서버를 거쳐
GitHub 으로 302 된다. 맥이 꺼져 있으면 **카드가 아예 안 뜨고 다운로드도 안 된다.**

GitHub 직행 주소는 맥과 무관하게 항상 살아 있다:
`https://github.com/600-g/shutdown-timer/releases/latest/download/AutoShutdownTimer.zip`

맥을 끄고도 배포를 유지하려면 허브가 이 주소로 바로 링크하게 바꿔야 한다(다운로드 집계는 잃는다).

## 건드리면 안 되는 것

- **에셋 이름 `AutoShutdownTimer.zip`** — 600g.net 카드가 이 이름으로 최신 파일을 찾는다. 바꾸면 사이트 다운로드가 끊긴다. `build.sh` 와 `.github/workflows/build.yml` 양쪽에 박혀 있다.
- **허브 관리 화면에 zip 을 직접 올리지 말 것** — 이중 관리가 된다. 파일의 유일한 출처는 이 저장소의 Release 다.
- `릴리스.bat` 은 윈도우 배치라 **CP949 + CRLF** 로 저장해야 한다. UTF-8 로 저장하면 한글이 깨진다.

## 앱에 대해 알아둘 것

- C# WinForms 단일 파일(`src/PhoneShell.cs`, 3,700줄). 리눅스 mono(`mcs`)로 크로스 빌드한다. 윈도우 전용 API를 쓰면 빌드가 깨진다.
- 폰트(Pretendard 서브셋)·아이콘은 `-resource:` 로 exe 안에 박혀 있다. `build.sh` 의 리소스 이름을 바꾸면 런타임에 못 찾는다.
- 관리자 권한 매니페스트가 `src/app_full.res` 에 들어 있다(게임 강제 종료용).
- **자동 업데이트가 들어 있다**(build 72~, 파일 끝 `Updater` 클래스). 시작 6초 뒤 조용히 확인한다.
  - **작업을 막는 팝업은 띄우지 않는다**(build 73~). 새 버전이 있으면 설정 맨 아래 **버전 줄에 배지**만 달고(`ShowUpdateBadge`), 트레이 귀띔은 **하루 한 번**(레지스트리 `Software\ShutdownTimer` 의 `updNotice` = yyyyMMdd). 확인 대화상자는 사용자가 그 줄을 눌렀을 때만 뜬다. 이 방침을 되돌리지 말 것 — 사용자가 명시적으로 요청한 UX다.
  - 릴리스 태그와 `VERSION` 을 `ParseVer` 로 자리별 비교한다. **태그와 소스 번호가 어긋나면 업데이트가 안 뜬다.**
  - `.NET 4.5` 기본 TLS 는 1.0 이라 GitHub 에 연결되지 않는다. `ServicePointManager.SecurityProtocol = 3072` 을 지우지 말 것.
  - 어떤 실패도 앱을 멈추면 안 된다. 전부 try/catch 이고 조용한 확인은 실패를 알리지 않는다.

### ★ `.bat` 은 cmd 로만 실행할 수 있다

`Process.Start` 에 `.bat` 을 `UseShellExecute=false` 로 주면 **윈도우에서 100% 실패**한다
(CreateProcess 는 PE 이미지만 로드 → 오류 193). 반드시 이 형태여야 한다:

```csharp
psi = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                           "/d /c \"\"" + bat + "\"\"");
psi.UseShellExecute = false;   // 환경 변수를 넘기려면 반드시 false
```

`UseShellExecute = true` 로 바꾸는 것은 해법이 아니다 — 그러면 `EnvironmentVariables` 가
`InvalidOperationException` 을 던진다.

⚠️ **정정(2026-09-20 실측)**: 감사 에이전트가 "`.bat` 직접 실행은 오류 193 으로 100% 실패한다" 고 단정했고
그 근거로 1.0.0 의 인앱 업데이트가 동작한 적 없다고 했으나, **실제 윈도우에서 1.0.0 → 1.0.1 인앱 업데이트가
정상 동작했다.** 즉 그 단정은 틀렸다. `cmd /d /c` 로 감싸는 현재 방식은 더 안전하므로 유지하되,
**코드 감사의 "100% 실패" 류 단정은 실측 없이 문서에 확정으로 적지 말 것.**

### ★ 내려받은 exe 의 차단 표시를 반드시 푼다 (1.0.3, 실사용에서 발견)

인터넷에서 받은 zip 에는 Mark of the Web 이 붙고, 압축을 풀면 그 표시가 exe 로 옮겨가
**윈도우가 실행을 막는다.** `보안패치.bat` 이 최초 설치 때 하는 일과 같은데,
업데이트 경로에는 없어서 **1.0.2 로 올린 사용자의 앱이 다시 켜지지 않았다.**
배치에 두 겹으로 넣었으니 지우지 말 것:

```
Get-ChildItem -LiteralPath $env:TMPD -Recurse -File | Unblock-File   (PowerShell 한 줄 끝)
del "%TMPD%\AutoShutdownTimer.exe:Zone.Identifier" 2>nul
```

### ✗ "새 앱이 떴는지 확인하고 안 떴으면 되돌리기" 는 넣지 말 것 (실사고)

감사가 권해서 넣었다가 **실제로 앱을 못 켜게 만들었다.**

⚠️ **교체 배치는 "지금 설치된" 버전의 exe 가 만든다.** 새로 받은 exe 는 교체에 참여하지 않는다.
그래서 배치를 고쳐도 **그 버전이 설치된 뒤 다음 업데이트부터** 적용된다. 자기 치유가 안 된다.
결함 있는 배치를 한 번 배포하면 그 버전 사용자는 **수동 재설치 외에 빠져나올 길이 없다.**
배치를 건드릴 때 특히 조심할 이유가 이것이다.
백신이 새 exe 를 검사하느라 5초 안에 안 뜨는 건 흔한 정상 상황인데,
그때 되돌리려고 **막 시작한 exe 를 덮어쓰려다 실패**해서 `:lost` 로 빠지고 앱을 아예 안 띄웠다.
파일 무결성(존재·크기)은 복사 직후 이미 확인하므로 기동 확인은 불필요하고 위험만 크다.

### 교체 배치 — 되돌리지 말아야 할 안전장치 (1.0.1, 감사 2회로 발견)

실행 중인 exe 는 자기를 못 덮어써서 임시 배치가 교체를 대신한다. 이 배치에는 사용자가 앱을 잃지 않게
하는 장치가 넷 있고, **하나라도 빼면 "업데이트를 눌렀더니 앱이 사라졌다"가 실제로 발생한다.**

1. **배치를 Exit 앞에서 띄우고, 취소는 센티넬 파일로 알린다** — 뒤에서 띄우면 `Process.Start` 가 실패했을 때
   앱이 이미 닫혀 있어 알릴 방법이 없다(MessageBox 의 owner 가 Dispose 됨). 먼저 띄우고, 종료가 취소되면
   `AST_CANCEL` 경로에 파일을 써서 배치가 조용히 물러나게 한다. 배치의 대기 루프가 매 회 이 파일을 확인한다.
2. **`Updater.Updating` 으로 종료 확인 모달을 건너뛴다** — `OnFormClosing` 의 "예약이 진행 중입니다" 모달이 종료를 붙잡으면
   배치가 대기하다 지친다. **저장된 알림이 하나만 켜져 있어도 `alarmRunning` 이 true 라 거의 모든 사용자가 이 경로를 탄다.**
3. **대기 타임아웃은 `goto :fail`** — 60초 안에 프로세스가 안 죽으면 교체를 포기한다. 예전엔 그대로 진행해서 덮어쓰기를 시도했다.
4. **백업 → 교체 → 검증 → 기동 확인 → 그때서야 백업 삭제** — `%EXE%.bak` 로 백업하고 copy 의 `errorlevel`·존재·
   크기(500KB 이상)를 확인한 뒤, **새 exe 가 실제로 실행되는지 `tasklist` 로 확인**하고 나서 백업을 지운다.
   어긋나면 `:rollback` 이 복원하고, **복원까지 실패하면 `:lost` 에서 백업을 남기고 깨진 exe 를 실행하지 않는다.**

배치 본문은 **순수 ASCII** 이고 경로는 환경 변수(`AST_EXE`/`AST_DIR`/`AST_PID`)로 넘긴다.
환경 블록은 유니코드로 전달되므로 한글 사용자명이든 비한국어 윈도우든 안전하다.
**경로를 배치에 글자로 박지 말 것** — 그러면 코드페이지에 묶여 깨진다.

업데이트 확인 타이머는 **`Shown` 이 아니라 생성자**에 있다. `--tray` 자동 실행은 `Shown` 이 오지 않아
트레이 상주 사용자가 영영 새 버전을 못 받게 된다. 첫 확인 6초 뒤, 이후 6시간마다.

## 안내 창은 앱 시트로 (1.0.2~)

윈도우 기본 `MessageBox` 는 이 앱의 아이폰풍 UI 에서 확 튄다. 그래서 **안내성 창은 `AppSheet` 로 띄운다**
(파일 끝 `SheetBody` + `AppSheet`, 약 600줄). `OpenNoteEditor`·`FireAlarm` 의 관습을 일반화한 것이다.

```csharp
AppSheet s = new AppSheet("제목", "v1.0.2", "부제");
s.SetStatus("상태 한 줄", Theme.Ok);
s.Body.AddHead("변경 내역");
AppSheet.AddMarkdown(s.Body, notes);          // 릴리스 본문(=CHANGELOG 절)을 블록으로
s.Body.AddLink("릴리스 페이지 열기", delegate { AppSheet.OpenUrl(RelUrl); });
s.Tell(owner, "닫기");                         // 또는 s.Ask(owner, "지금 업데이트", "나중에") → bool
```

지킬 것:
- **색 리터럴 금지.** 전부 `Theme.*`. 별도 Form 이라 `RemapTree` 가 안 닿으므로 리터럴을 박으면 다크 모드에서 그대로 남는다.
- **폰트는 생성자에서 만들어 필드로 들고 Dispose.** `Fonts.Regular/Semi` 는 호출마다 `new Font` 이고 아무도 Dispose 하지 않아, Paint 안에서 부르면 GDI 핸들이 쌓인다. `Fonts.SemiPx` 는 캐시라 반대로 Dispose 금지.
- **람다 금지.** 이 파일은 `=>` 가 0건이다. 전부 `delegate { }`.
- **모달 유지.** 비모달로 바꾸면 `Updater.busy` 가 먼저 풀려 6시간 자동 확인이 시트를 겹쳐 쌓고, 예약 경고의 개시-시점 평가 보장도 깨진다.
- 스크롤은 휠·드래그·키 3중화. 휠은 포커스가 없으면 안 오므로 드래그가 실질 보장선이다.

**그대로 둬야 하는 MessageBox**: 종료 확인(`DialogResult` 가 `e.Cancel` 을 정한다) · `ReportCrash`(폼·테마가 없을 때도 불린다) · "이미 실행 중"(`Application.Run` 전) · 진단 실패 폴백(시트 자체가 죽었을 때).

진단 창은 **클립보드로 나가는 원문을 바꾸지 않고** 화면만 시트로 바꿨다(`FillDiagBody` 가 원문을 블록으로 옮긴다).
지원 요청에 붙여넣는 텍스트라 형식을 건드리면 안 된다.

## 릴리스 전 검증 (워크플로가 자동으로)

빌드 직후 `산출물 검증` 스텝이 돌고, 하나라도 걸리면 **릴리스되지 않는다**:
zip 3파일 존재 · exe 가 MZ 헤더이고 500KB 초과 · 소스 `VERSION` 과 exe 안 문자열 일치 ·
**자동 업데이트 URL 2종이 exe 에 남아 있음**(빠지면 이후 사용자가 영영 갱신을 못 받으므로) ·
`보안패치.bat` 이 실제 exe 이름을 가리킴.

## dev 브랜치 = 릴리스 없는 컴파일·검증

`dev` 로 push 하면 빌드와 검증만 하고 릴리스는 안 한다. 맥에는 mono 가 없어 로컬 컴파일이 불가능하니,
앱 코드를 고쳤으면 **dev 로 먼저 올려 초록불을 본 뒤 main 에 병합**한다.

⚠️ **줄 끝 `//` 주석 주의** — 이 소스는 한 줄에 선언 여러 개를 붙여 쓴 곳이 많다.
줄 중간에 `//` 주석을 끼우면 **그 뒤 선언이 통째로 주석 처리된다**(2026-09-20 실제 사고: `private Timer etaTimer;` 가 먹혀 컴파일 실패).
주석은 선언 줄 **위**에 따로 놓을 것.

## 확인

```bash
# 현재 릴리스와 사이트 상태
gh release list --repo 600-g/shutdown-timer --limit 3
curl -s https://api.600g.net/api/apps | python3 -m json.tool
```
