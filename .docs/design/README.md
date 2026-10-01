# Chapter_01 제작 상태

기준은 [Notion 기획서](https://app.notion.com/p/3dcb3e6e776c81f08abde9fa6cefdb80)와 [아트 레퍼런스](https://app.notion.com/p/3dcb3e6e776c81f2bb63c68f908c5ec5)다. 아래 조작·장치 규칙은 사용자가 지정했다.

## 조작과 능력

| 대상 | 규칙 |
| --- | --- |
| 킹 | A/D 이동, W 점프 |
| 룩 | 좌/우 이동, 위 방향키 점프 |
| CASTLING | F로 현재 위치 교환. 거리·시야·공중 사용·재사용 제한 없음. 각자의 속도 유지 |
| 룩 직선 이동 | 오른쪽 Shift + 좌/우. 걷기 속도 2배, 이동 중 방향 고정. 앞쪽 지형 충돌 또는 Shift 해제로 종료 |
| 압력 스위치 | 어느 한쪽을 밟는 동안 문 열림. 문 안에 몸체가 있으면 빠져나간 뒤 닫힘 |
| 공동 목표 | 두 캐릭터가 목표 영역에 도착하면 완료 표시 활성화 |

룩은 Shift와 방향키를 어느 순서로 눌러도 발동한다. 지형에 막힌 뒤에는 Shift를 놓고 다시 눌러야 한다. 기존 걷기 속도 9, 점프 속도 17.5, 기본 중력 배율 3.5와 플레이어 프리팹은 유지했다.

## 현재 배치와 확인 방법

`Assets/Scenes/Chapter_01.unity`를 열고 Play한다. `Grid/Room_01` 하나의 연결된 지형을 걸어서 진행하며, 구간마다 암전하거나 시작점으로 이동하지 않는다. 씬은 Build Settings에 추가했고 기존 첫 빌드 씬의 순서는 유지했다.

- [첫 통로](OPENING_COOPERATION.md): 작은 룩이 먼저 통과하고 바닥에서 F로 킹을 넘겨준 뒤 다시 걸어서 합류한다.
- [후속 협동 구간](FOLLOWUP_COOPERATION.md): 스위치 역할 넘기기 → 룩의 좌우 이동 → 지상 교환 → 재합류. 추락하면 왼쪽 복귀 발판으로 돌아온다.
- [카메라 설정](CHAPTER_CAMERA.md): Dev_Gameplay의 기본·최소값 8을 기준으로 공유 화면을 구성한다.

x80 이후는 기존 연속 지형과 공동 목표를 유지한 구간이다. 챕터 전체의 최종 퍼즐·아트·분량, 두 사람이 처음 플레이할 때의 발견성과 체감 난이도는 아직 확정하지 않았다. 스프링·이동 플랫폼·위험물·체크포인트는 추가하지 않았다.

## 아트와 계층 기준

큰 기반 지형과 연결된 지붕, 기존 검정·아이보리 팔레트, 낮은 대비의 배경 구조물·체스 실루엣을 사용한다. 곡선은 팔레트의 완전한 묶음으로 조립한다. 룩 이동 구간의 오른쪽 착지면은 작은 곡선으로 마감해 킹이 벽을 타고 올라가는 우회를 막았다. 자세한 타일 구성은 각 구간 문서와 [플랫폼 시트](../art/PLATFORM_SHEETS.md)를 따른다.

참고 조사에서는 [ibb & obb](https://ibbandobb.com/)의 빈 공간과 연결 면, [Thomas Was Alone](https://www.nintendo.com/en-gb/Games/Wii-U-download-software/Thomas-Was-Alone-938127.html)의 단순한 덩어리 배치, [Monument Valley](https://www.monumentvalleygame.com/)의 아치·기둥과 배경 톤을 비교했다. 실제 프로젝트 조형 기준은 Dev_Gameplay와 Notion 아트 기획이다.

계층 이름·룸·프리팹 구성은 [Unity 하네스](../harness/UNITY.md)를 따른다.

## 검증 하네스

[후속 구간 문서](FOLLOWUP_COOPERATION.md)의 실제 PlayerInput 검사로 첫 통로·스위치·룩 이동·복귀·단독 점프 우회를 확인한다. 카메라의 추가 배치 검사는 [카메라 문서](CHAPTER_CAMERA.md)를 따른다. 각 본문은 새 Play 세션에서 하나씩 실행하고, Stop 후 임시 입력 기기를 정리한다.

캡처와 일회성 검증 결과 JSON은 로컬 산출물이며 Git에서 제외한다.
