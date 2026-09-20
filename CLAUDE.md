# 자동 종료 타이머 — AI 작업 규칙

이 저장소를 고치는 AI(클로드 코드 · claude.ai/code · 기타)는 아래를 반드시 지킨다.

## ★ 1번 규칙: build 번호를 올려라

`src/PhoneShell.cs` 를 **조금이라도 고쳤다면** 같은 커밋에서 버전 상수를 올린다.

```csharp
private const string VERSION = "v1.0 (build 70)";   // → 71 로
```

이 번호가 곧 릴리스 태그(`v1.0.NN`)다. 안 올리면 GitHub Actions 가 **실패(빨간 X)** 로 막고 배포가 안 나간다.
번호만 올리고 코드를 안 고치는 것은 괜찮다(재빌드용). 반대는 안 된다.

## 배포 구조 — main 에 올리면 끝

```
src/PhoneShell.cs 수정 → main push → Actions(mono 빌드) → GitHub Release v1.0.NN
                                    → 600g.net "종료타이머&알림" 카드가 자동 추적 (최대 10분)
```

- 태그를 직접 만들 필요 없다. Actions 가 번호를 읽어 태그까지 만든다.
- 올리는 방법 아무거나: GitHub 웹 Upload files(권장, git 불필요) · `릴리스.bat` 더블클릭 · `git push`
- `README.md` 나 문서만 고치면 빌드가 돌지 않는다(의도된 동작).

## 건드리면 안 되는 것

- **에셋 이름 `AutoShutdownTimer.zip`** — 600g.net 카드가 이 이름으로 최신 파일을 찾는다. 바꾸면 사이트 다운로드가 끊긴다. `build.sh` 와 `.github/workflows/build.yml` 양쪽에 박혀 있다.
- **허브 관리 화면에 zip 을 직접 올리지 말 것** — 이중 관리가 된다. 파일의 유일한 출처는 이 저장소의 Release 다.
- `릴리스.bat` 은 윈도우 배치라 **CP949 + CRLF** 로 저장해야 한다. UTF-8 로 저장하면 한글이 깨진다.

## 앱에 대해 알아둘 것

- C# WinForms 단일 파일(`src/PhoneShell.cs`, 3,700줄). 리눅스 mono(`mcs`)로 크로스 빌드한다. 윈도우 전용 API를 쓰면 빌드가 깨진다.
- 폰트(Pretendard 서브셋)·아이콘은 `-resource:` 로 exe 안에 박혀 있다. `build.sh` 의 리소스 이름을 바꾸면 런타임에 못 찾는다.
- 관리자 권한 매니페스트가 `src/app_full.res` 에 들어 있다(게임 강제 종료용).
- **현재 네트워크 기능이 전혀 없다**(`System.Net` 미사용). 앱 내 자동 업데이트는 아직 없다.

## 확인

```bash
# 현재 릴리스와 사이트 상태
gh release list --repo 600-g/shutdown-timer --limit 3
curl -s https://api.600g.net/api/apps | python3 -m json.tool
```
