# 플랫폼 타일 사용법

## 원본 시트

- 검정: [PlatformTiles_Dark.png](../../Assets/Sprite/Map/PlatformTiles_Dark.png)
- 아이보리: [PlatformTiles_Ivory.png](../../Assets/Sprite/Map/PlatformTiles_Ivory.png)

두 시트는 각각 3960×2548px이며 Sprite / Multiple, 128 PPU로 분할돼 있다. 기존 배치 위에 안쪽 꼭짓점 마감 조합 31종씩을 추가했다. 기존 타일의 픽셀·좌표·Sprite ID는 유지했다. 128px 셀 바깥에 2px 패딩이 있으므로 전체를 128px 격자로 다시 자동 분할하지 않는다.

## 맵 칠하기

Unity Tile Palette에서 **AutoPlatforms_TilePalette**를 선택한다. 왼쪽 검정, 오른쪽 아이보리다.

팔레트 5개는 서로 다른 타일을 포함하므로 역할별로 유지한다. 팔레트는 편집용 프리팹이며 실제 지형 타일은 아래 라이브러리 4개에 저장된다.

| 팔레트 | 용도 |
| --- | --- |
| AutoPlatforms_TilePalette | 자동 사각 지형과 자동 곡선 |
| Curves_TilePalette | 수동 곡선 부품 |
| MainMap_TilePalette | 기존 기본 타일과 안쪽 꼭짓점 마감 |
| CurveBorders_Dark / CurveBorders_Ivory | 검정·아이보리 수동 외곽선 조합 |

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
| Manual_Dark.asset | 검정 수동 타일 492종 |
| Manual_Ivory.asset | 아이보리 수동 타일 490종 |

자동 타일은 기본 사각 흑백 2종과 곡선 셀 118종이다. 수동 타일을 자동으로 교체하지는 않는다. 기존 1,040종에 꼭짓점 마감 62종을 더한 1,102종을 라이브러리 4개에 저장한다. 자동 타일의 외곽선 조합 참조도 수동 라이브러리 안의 타일을 사용한다.

## 안쪽 꼭짓점 마감

자동 `Square`는 인접한 두 면이 연결되고 대각선이 비어 있으면 해당 꼭짓점에 마감을 표시한다. 대각선을 채우면 마감이 사라지고, 지우면 다시 나타난다. 네 방향과 여러 꼭짓점의 동시 마감을 지원하며, 검정·아이보리의 기존 선 색과 두께를 따른다. 타일의 충돌 도형은 기존 외곽선 조합과 같다.

수동으로 지정하려면 **MainMap_TilePalette 맨 아래 Inner Join** 두 행을 사용한다. 각 행은 오른쪽 위 → 오른쪽 아래 → 왼쪽 아래 → 왼쪽 위 순서다. 검정은 y=-38, 아이보리는 y=-41이며 x=0, 3, 6, 9다. 기존 타일을 해당 마감 타일로 교체해 칠한다. 자동 처리 대상은 사각 타일이며, 곡선 부품의 형태를 바꾸지는 않는다.

## 수동 외곽선

특정 부분의 선을 직접 지정하려면 **CurveBorders_Dark / CurveBorders_Ivory** 팔레트를 사용한다.

T=위, R=오른쪽, B=아래, L=왼쪽, C=곡선이다. None은 선 없음이다. 아치의 C는 안쪽 원호를 뜻한다. 예를 들어 TLC는 곡선과 위·왼쪽에 선을 넣는다.

각 형태의 외곽선 32조합은 **8열 × 4줄**로 배치했다. `T=1, R=2, B=4, L=8, C=16`을 합한 값이 왼쪽부터 0~7, 다음 줄 8~15, 16~23, 24~31 순서다. None은 첫 줄 첫 번째, C는 세 번째 줄 첫 번째, TLC는 네 번째 줄 두 번째, 전체 경계는 마지막이다. 회전하면 방향도 함께 회전한다.

각 형태마다 32개 이름을 반복 표시하는 TextMesh 라벨은 팔레트에서 멀리 스크롤한 뒤에도 조합을 찾기 위한 것이다. 검정·아이보리 팔레트에 각각 462개가 있으며, 타일이나 게임 오브젝트의 중복본은 아니다.

형태는 위에서부터 OuterSmall, OuterR1, OuterR2, InnerR1, InnerR2, InnerR3, RoundCap, RoundIsolated, SlimStraight, SlimCap, SlimIsolated, ArchR1, ArchR2, ArchR3 순서다. 부품 사이에는 한 칸 여백을 두고 크기에 맞춰 간격을 줄였다. 수동 외곽선 팔레트의 가로 폭은 190칸에서 39칸으로 줄었다.

자동 팔레트는 40×42칸, 수동 곡선 팔레트는 40×44칸이다. 기본 팔레트는 큰 스프라이트의 실제 표시 크기를 고려해 기존 27×35칸 배치 아래에 꼭짓점 마감 두 행을 추가했다. 기존 타일·회전·색·묶음 내부 배치는 유지했다.

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

꼭짓점 마감 추가 후 두 색상·같은/다른 Tilemap의 256가지 이웃 배치를 포함한 자동 연결 7,942개 검사와 충돌 경계 37,056개 검사를 통과했다. 추가한 62종의 충돌 도형은 대응하는 기존 타일과 같고, 두 시트의 기존 픽셀도 전부 동일하다. 개발 씬의 마감 6곳은 Play Mode에서도 확인했다.

기존 수동 타일로 남아 있던 접합부 7곳도 같은 외곽선·충돌을 가진 자동 Square로 교체했다. Ground의 (-11, -6), (-9, -6) 흑백 T자 접합부가 포함된다. 큰 아치가 이미 덮고 있는 모서리는 제외했다. 자동 타일은 총 586칸이며 충돌 경계 비교 37,504곳에서 불일치가 없었다.

Unity MCP에서 다시 실행할 검증 본문:
- [자동 연결·브러시·Undo/Redo](verification/VerifyAutomaticBorders.cs.txt)
- [실제 씬 충돌 경계 비교](verification/VerifyAutomaticCollision.cs.txt)

두 `.cs.txt` 파일은 Unity MCP `execute_code`에 넣는 검사 본문이며 게임 코드나 Unity Test Runner 테스트 에셋은 아니다.
