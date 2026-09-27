# 플랫폼 타일 사용법

## 원본 시트

- 검정: [PlatformTiles_Dark.png](../../Assets/Sprite/Map/PlatformTiles_Dark.png)
- 아이보리: [PlatformTiles_Ivory.png](../../Assets/Sprite/Map/PlatformTiles_Ivory.png)

두 시트는 각각 3960×2284px이며 Sprite / Multiple, 128 PPU로 분할돼 있다. 위에서부터 기존 기본·연결 타일, 기본 곡선, 외곽선 조합, 기존 큰 부품 순서다. 128px 셀 바깥에 2px 패딩이 있으므로 전체를 128px 격자로 다시 자동 분할하지 않는다. Sprite 이름·ID와 물리 도형을 보존한다.

## 맵 칠하기

Unity Tile Palette에서 **AutoPlatforms_TilePalette**를 선택한다. 왼쪽 검정, 오른쪽 아이보리다.

- 사각 지형은 맨 아래 `Square` 한 칸을 집어 칠한다.
- 곡선·아치는 원하는 형태의 묶음 전체를 집어 칠한다. 빈 칸도 선택 영역에 포함한다.
- 회전·뒤집기는 기본 GridBrush를 사용한다. 크기는 부품 종류로 고르고 Transform Scale로 늘리지 않는다.
- 배치·지우기·Undo/Redo에 따라 노출 면에는 선이 생기고, 실제로 맞닿은 면에서는 선이 빠진다.

| 부품 | 묶음 크기 |
| --- | --- |
| OuterSmall / OuterR1 | 1×1 |
| OuterR2 | 2×2 |
| InnerR1 / ArchR1 | 2×2 |
| InnerR2 / ArchR2 | 3×3 |
| InnerR3 / ArchR3 | 4×4 |
| RoundCap / RoundIsolated | 1×1 |
| SlimStraight / SlimCap / SlimIsolated | 1×1 |

타일은 `Assets/TilePalette/Main/Tiles/`의 라이브러리 4개에 하위 에셋으로 묶여 있다. Project 창에서 파일 왼쪽 화살표를 펼치면 개별 타일을 볼 수 있다. 맵 편집은 기존 Tile Palette를 그대로 사용한다.

| 라이브러리 | 포함 타일 |
| --- | --- |
| Automatic_Dark.asset | 검정 자동 타일 60종 |
| Automatic_Ivory.asset | 아이보리 자동 타일 60종 |
| Manual_Dark.asset | 검정 수동 타일 461종 |
| Manual_Ivory.asset | 아이보리 수동 타일 459종 |

자동 타일은 기본 사각 흑백 2종과 곡선 셀 118종이다. 수동 타일을 자동으로 교체하지는 않는다. 1,040종의 형태·외곽선 조합을 보존하면서 별도 타일 파일은 4개로 통합했다. 자동 타일의 외곽선 조합 참조도 수동 라이브러리 안의 타일을 사용한다.

## 수동 외곽선

특정 부분의 선을 직접 지정하려면 **CurveBorders_Dark / CurveBorders_Ivory** 팔레트를 사용한다.

T=위, R=오른쪽, B=아래, L=왼쪽, C=곡선이다. None은 선 없음이다. 아치의 C는 안쪽 원호를 뜻한다. 예를 들어 TLC는 곡선과 위·왼쪽에 선을 넣는다.

각 형태의 외곽선 32조합은 **8열 × 4줄**로 배치했다. `T=1, R=2, B=4, L=8, C=16`을 합한 값이 왼쪽부터 0~7, 다음 줄 8~15, 16~23, 24~31 순서다. None은 첫 줄 첫 번째, C는 세 번째 줄 첫 번째, TLC는 네 번째 줄 두 번째, 전체 경계는 마지막이다. 회전하면 방향도 함께 회전한다.

형태는 위에서부터 OuterSmall, OuterR1, OuterR2, InnerR1, InnerR2, InnerR3, RoundCap, RoundIsolated, SlimStraight, SlimCap, SlimIsolated, ArchR1, ArchR2, ArchR3 순서다. 부품 사이에는 한 칸 여백을 두고 크기에 맞춰 간격을 줄였다. 수동 외곽선 팔레트의 가로 폭은 190칸에서 39칸으로 줄었다.

자동 팔레트는 40×42칸, 수동 곡선 팔레트는 40×44칸이다. 기본 팔레트는 큰 스프라이트의 실제 표시 크기를 고려해 27×35칸에 배치했다. 기존 타일·회전·색·묶음 내부 배치는 유지했다.

## 접합 범위

- 색이 달라도 맞닿는 면이면 선을 생략한다. 같은 Grid의 활성 TilemapCollider2D가 있는 지형끼리는 Tilemap이 달라도 연결을 판정한다. 충돌이 없는 배경은 제외한다.
- 얇은 부품이 넓은 면의 일부에만 닿으면 전체 선을 유지한다. 이런 부분은 맞는 접합 부품이나 수동 타일로 조정한다. `Tools > CASTLING > Check Selected Platform Borders`로 위치를 확인할 수 있다.
- 곡선 형태 자체를 바꾸거나, 큰 부품을 잘라 생긴 새 경계를 만드는 기능은 없다. 곡선 구멍을 다른 Tilemap으로 겹쳐 메우는 방식도 지원하지 않는다.
- 서로 다른 Tilemap의 자동 갱신은 Editor 편집 기준이다.

## 적용·검증

`Dev_Gameplay` 시작 구간 Ground/Ceiling의 x=-62~−32, 579칸에 적용돼 있다. 곡선을 보존하기 위해 사용자 승인으로 CompositeCollider2D Vertex Distance를 0.001로 설정했다. 이동·점프·중력 설정은 유지했다.

기존 `OpeningArch_Left/Right`는 Ground 타일맵의 (-25, -6), (-21, -6) 셀로 통합했다. 기존 4배 크기와 위치를 셀 변환으로 보존한 레거시 부품이다. 별도 자식 오브젝트는 없다. `TileMap_Main_43/44`의 Sprite Physics Shape에는 기존 아치 PolygonCollider2D의 정밀한 곡선을 반영했다. 새 지형을 만들 때는 위의 모듈형 곡선을 사용한다.

시트 통합 시 840개 Sprite의 픽셀·피벗·크기·Border·PPU·충돌 도형을 원본과 비교했다. 자동 연결 3,844개 검사, 실제 충돌 경계 37,056곳 비교, 킹·룩 왕복 점프와 착지 파티클 색 검사도 통과했다.

타일 라이브러리 통합 후 씬과 팔레트 5개의 17,843칸에서 스프라이트·색·변환·충돌 종류가 이전과 같음을 확인했다. 기존 개별 파일 제거 후 참조 누락 0건, 자동 연결 3,844개 및 충돌 경계 37,056개 검사 통과. Play Mode에서도 자동 타일 579칸의 스프라이트가 정상 로드됐다.

Unity MCP에서 다시 실행할 검증 본문:
- [자동 연결·브러시·Undo/Redo](VerifyAutomaticBorders.cs.txt)
- [실제 씬 충돌 경계 비교](VerifyAutomaticCollision.cs.txt)
