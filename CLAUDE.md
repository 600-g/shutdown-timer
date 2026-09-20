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

## 여러 기기에서 같이 쓸 때 (맥 + 윈도우 + claude.ai)

**진실은 GitHub 의 `main` 하나다.** 맥의 `~/Developer/shutdown-timer` 는 사본일 뿐이다.
claude.ai 가 GitHub 에 바로 커밋하므로 맥 사본은 언제든 뒤처져 있을 수 있다.

- **맥에서 앱 코드를 고치기 전에 반드시 `git pull` 부터.** 안 하면 뒤처진 내용 위에 고치게 되고,
  push 가 거부되거나 다른 기기의 수정을 덮어쓴다.
- 윈도우 `릴리스.bat` 은 시작할 때 알아서 `git pull --rebase --autostash` 를 한다.
- 같은 파일을 두 기기에서 동시에 고치지 않는 것이 가장 확실하다. 한 번에 한 곳에서만.
- 충돌이 나면 억지로 밀지 말 것(`--force` 금지). 릴리스 태그가 커밋과 어긋나면 되돌리기 어렵다.

```bash
# 맥에서 작업 시작할 때
cd ~/Developer/shutdown-timer && git pull
```

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
  - 릴리스 태그의 끝 숫자와 `VERSION` 의 build 번호를 비교한다. 그래서 **태그와 소스 번호가 어긋나면 업데이트가 안 뜬다.**
  - 교체는 임시 배치가 한다(실행 중 exe 는 자기를 못 덮어씀). 배치는 **`Encoding.Default`(시스템 ANSI)로 써야 한다** — ASCII 로 쓰면 한글 사용자명 경로에서 실패한다.
  - `.NET 4.5` 기본 TLS 는 1.0 이라 GitHub 에 연결되지 않는다. `ServicePointManager.SecurityProtocol = 3072` 을 지우지 말 것.
  - 어떤 실패도 앱을 멈추면 안 된다. 전부 try/catch 이고 조용한 확인은 실패를 알리지 않는다.

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
