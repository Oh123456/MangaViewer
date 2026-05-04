# Viewer

[English Version](#viewer-english)

WinForms + SQLite 기반의 Windows 전용 로컬 이미지 폴더 관리/뷰어 앱입니다. 이미지 파일을 앱 내부로 복사하지 않고 원본 폴더 경로를 SQLite DB에 저장해 관리합니다.

## 주요 기능

- 루트 폴더 스캔/빠른 동기화
- SQLite 로컬 DB 기반 이미지 폴더 목록 관리
- 검색, 태그 필터, 제외 태그, 정렬, 빠른 필터
- 즐겨찾기, 보류함, 최근 본 목록
- 이미지 뷰어, 전체화면, 페이지 이동, 묶음 편수 이동
- 묶음 관리와 묶음 목록/랜덤 추천 연동
- 신규등록 루트와 메인 루트 분리 관리
- 신규등록 항목 삭제 및 메인 루트로 이동
- 중복 이름 폴더 확인, 중복 폴더 엑셀 내보내기
- DB/설정 백업 및 복원
- 한국어/영어 다국어 지원
- GitHub Releases 기반 업데이트 확인 및 자동 적용

## 실행 환경

- Windows
- .NET 8 Desktop Runtime 또는 .NET 8 SDK
- Visual Studio 2022 권장

## 사용 방법

1. 앱을 실행합니다.
2. 상단 메뉴의 `설정`을 엽니다.
3. `메인 루트 추가`로 관리할 이미지 루트 폴더를 등록합니다.
4. 필요하면 `신규 루트 추가`로 새로 받은 파일을 임시 등록할 폴더를 추가합니다.
5. 메인 화면에서 `빠른 동기화` 또는 `전체 스캔`을 실행합니다.
6. 목록에서 폴더를 선택해 상세 정보, 태그, 점수, 메모, 썸네일을 수정합니다.
7. `보기` 버튼 또는 목록 더블클릭/Enter로 이미지 뷰어를 엽니다.

## 루트 폴더 개념

- 메인 루트: 일반 라이브러리로 관리할 폴더입니다.
- 신규 루트: 새로 받은 폴더를 임시로 확인하는 공간입니다.
- 신규등록 탭에서는 폴더 삭제와 메인 루트로 이동을 바로 처리할 수 있습니다.
- 메인으로 이동할 때는 대상 메인 루트를 선택하며, 실제 폴더 이동과 DB 경로 갱신을 함께 수행합니다.

## 데이터 저장 위치

앱 실행 폴더에 다음 파일과 폴더가 생성될 수 있습니다.

- `viewer.db`
- `viewer.settings.json`
- `Logs`
- `Exports`
- `Backups`

삭제나 백업이 쉽도록 앱 실행 폴더 기준으로 데이터를 관리합니다.

## 다국어

번역 파일은 실행 폴더의 `Translations` 폴더에 복사됩니다.

- `Translations/kr`
- `Translations/en`
- `Translations/languages.json`

설정 창에서 언어를 변경하면 앱 UI가 즉시 갱신됩니다.

## 빌드

```powershell
dotnet build Viewer.sln
```

릴리즈 배포는 Visual Studio 게시 또는 `dotnet publish`를 사용할 수 있습니다.

## 주의 사항

- 앱은 실제 파일명을 자동 변경하지 않습니다.
- 폴더 삭제 기능은 실제 폴더를 휴지통으로 이동할 수 있으므로 사용 전 경고창을 확인하세요.
- 큰 라이브러리에서는 스캔 시간이 길어질 수 있습니다. 오래 걸리는 작업은 진행도 창으로 표시됩니다.

---

# Viewer English

[한국어 버전](#viewer)

Viewer is a Windows-only local image folder manager and viewer built with WinForms and SQLite. It stores original folder paths in a local SQLite database instead of copying image files into the app.

## Features

- Root folder scan and quick sync
- Local SQLite database
- Search, tag filters, excluded tags, sorting, and quick filters
- Favorites, reserved items, and recently viewed list
- Image viewer with fullscreen, page jump, and series navigation
- Series management with series tab and random recommendation support
- Separate main roots and new-registration roots
- Delete new-registration folders or move them into a main root
- Duplicate name folder checker and duplicate folder Excel export
- DB/settings backup and restore
- Korean and English localization
- GitHub Releases based update checking and automatic apply

## Requirements

- Windows
- .NET 8 Desktop Runtime or .NET 8 SDK
- Visual Studio 2022 recommended

## Usage

1. Run the app.
2. Open `Settings` from the top menu.
3. Add a library folder with `Add Main Root`.
4. Optionally add a temporary incoming folder with `Add New Root`.
5. Run `Quick Sync` or `Full Scan`.
6. Select a folder and edit metadata, tags, score, memo, favorite state, or thumbnail.
7. Open the image viewer with `View`, double-click, or Enter on the list.

## Root Types

- Main Root: folders managed as the main library.
- New Root: temporary incoming folders for newly downloaded content.
- The New tab supports actual folder deletion and moving folders into a main root.
- Moving to main updates both the actual folder path and the database path.

## Data Location

The app may create these files and folders next to the executable.

- `viewer.db`
- `viewer.settings.json`
- `Logs`
- `Exports`
- `Backups`

App data is stored next to the executable so it is easy to delete, move, or back up.

## Localization

Translation files are copied to the `Translations` folder next to the executable.

- `Translations/kr`
- `Translations/en`
- `Translations/languages.json`

Changing the language in Settings refreshes the UI immediately.

## Build

```powershell
dotnet build Viewer.sln
```

For release builds, use Visual Studio publish or `dotnet publish`.

## Notes

- The app does not automatically rename actual files or folders.
- Folder deletion can move real folders to the Recycle Bin, so check the warning dialog carefully.
- Large libraries can take time to scan. Long-running tasks show a progress window.
