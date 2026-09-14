# 자동 종료 타이머 (ⓒ 600g)

설정한 시간이 지나면 PC 종료 · 특정 프로그램 종료 · 알림을 띄우는 Windows 앱.

---

## 자동 배포 (패치마다 자동으로 Releases에 올리기)

이 저장소는 **버전 태그를 push하면 GitHub이 알아서 빌드해서 Release에 zip을 올려줍니다.**
매번 직접 파일을 올릴 필요가 없습니다.

### 처음 한 번만 세팅

1. 이 폴더 전체를 GitHub 저장소에 올립니다 (아래 "최초 업로드" 참고).
2. 저장소 → **Settings → Actions → General → Workflow permissions** 에서
   **"Read and write permissions"** 를 켜고 저장. (Release 자동 생성에 필요)

### 패치할 때마다 (이게 전부)

새 `PhoneShell.cs` 를 `src\` 에 덮어쓴 뒤 **`릴리스.bat` 더블클릭.**
소스 안의 build 번호를 읽어 `v1.0.NN` 태그를 만들고 커밋·push·태그 push 까지 한 번에 합니다.

(터미널로 직접 하려면)
```bash
git add -A
git commit -m "v1.0.55"
git tag v1.0.55
git push && git push --tags
```

`git push --tags` 를 하는 순간 GitHub Actions가:
1. 리눅스에서 mono로 exe 빌드
2. 보안패치.bat · 사용설명서.txt 와 함께 zip으로 묶기
3. **Releases 페이지에 v1.0.12 로 자동 업로드**

끝나면 저장소 **Releases** 탭에 새 버전이 생기고,
`자동종료타이머.zip` 다운로드 링크가 자동으로 만들어집니다.
600g.net 에는 그 링크(항상 최신을 가리키는 주소)만 걸어두면 됩니다.

> 최신 릴리스 고정 다운로드 주소:
> `https://github.com/<사용자명>/<저장소명>/releases/latest`

### 태그 없이 빌드만 확인하고 싶을 때

저장소 **Actions** 탭 → 왼쪽 `build-and-release` → **Run workflow** 클릭.
빌드된 zip이 그 실행의 **Artifacts** 에 올라옵니다 (Release는 안 만듦).

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
릴리스.bat          윈도우에서 더블클릭 → 커밋·태그·push 한 번에
.github/workflows/build.yml   자동 빌드·배포 설정
```

## 로컬에서 직접 빌드 (선택)

mono가 깔린 환경에서:

```bash
./build.sh
```

→ `자동종료타이머.exe` 와 `자동종료타이머.zip` 이 생깁니다.

---
ⓒ 600g · 600g.net
