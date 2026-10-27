# D-Day Widget

윈도우 바탕화면에 중요한 일정의 D-Day를 띄워두는 데스크톱 위젯.

## 만든 이유
컴퓨터를 켜면 중요한 일정들 D-day가 나왔으면 좋겠어서 이런거 지원하는 프로그램(유료라도 살 의향이 있었건만...)을 찾았으나 없어서 그냥 만들었음.

## 주요 기능
- 이벤트별 D-Day / D+ 표시, 이름 아래에 날짜·요일 표시
- 긴급도별 색상 구분: D-Day 당일 강조, D-7 이내 강조, 지난 일정은 회색 처리
- 다크 / 라이트 모드, 포인트 색상 7종
- 현재 날짜·시각 표시, 자정에 D-Day 자동 갱신
- 투명도 조절, 항상 위 표시, 날짜순 정렬
- 윈도우 시작 시 자동 실행
- 드래그로 위치 이동, 위치 기억

## 설치 및 실행
.NET Framework 4.x가 기본 탑재된 Windows 10/11에서 별도 설치 없이 빌드된다.

1. 저장소를 내려받는다 (Code → Download ZIP)
2. `compile.bat` 실행 → `DDayWidget.exe` 생성 (최초 1회)
   - `events.txt`가 없으면 `events.sample.txt`를 복사해 만들어준다
3. `DDayWidget.exe` 실행

## 사용법
| 버튼 | 기능 |
|---|---|
| ↺ | 새로고침 |
| ✎ | `events.txt` 열기 (이벤트 편집) |
| ⚙ | 설정 (색상, 모드, 투명도, 자동 시작 등) |
| × | 종료 |

이벤트는 `events.txt`에 `이름=YYYY-MM-DD` 형식으로 한 줄씩 적는다.

```
새해=2027-01-01
프로젝트 마감=2026-12-15
```

### settings.txt
설정 창에서 바꾸면 자동 저장된다. 직접 수정도 가능.

| 항목 | 값 |
|---|---|
| `theme` | red / orange / yellow / green / blue / indigo / violet |
| `mode` | dark / light |
| `opacity` | 40 ~ 100 (%) |
| `topmost` | true / false |
| `sort` | true / false (날짜순 정렬) |

## 기술 스택
- C# 5 / Windows Forms (.NET Framework 4.x 내장 `csc.exe`로 빌드)
- 외부 라이브러리 없음

## 히스토리
- **2024.11** Electron(Node.js/HTML) 버전으로 처음 제작. Perplexity의 도움을 받음. 해당 버전은 `electron-final` 태그에 보존.
- **2026** C# WinForms로 전면 재작성. Claude와 함께 v6.1까지 개선 (뉴트럴 리디자인, 라이트 모드, 긴급도 위계, 리소스 누수 수정 등).

## 만든 사람
김민수 · with Claude
