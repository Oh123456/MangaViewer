# Viewer

WinForms + SQLite 기반 로컬 이미지 폴더 관리/뷰어 앱.

## 문서

- [프로젝트 현황](Docs/PROJECT_STATUS.md)
- [작업 기록](Docs/WORK_LOG.md)
- [릴리즈 전 회귀 테스트 체크리스트](Docs/REGRESSION_CHECKLIST.md)

## 핵심 기능

- 루트 폴더 스캔/동기화
- SQLite 로컬 DB 저장
- 검색, 태그 필터, 정렬, 즐겨찾기, 보류함
- 묶음 관리와 묶음 연속 보기
- 랜덤 추천
- 중복 이미지 엑셀 내보내기
- DB/설정 백업 및 복원

## 실행 파일 데이터

앱 실행 폴더에 `viewer.db`, `viewer.settings.json`, `Logs`, `Exports`, `Backups`가 생성될 수 있다.
