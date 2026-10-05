# 플랫폼 타일 사용법

## 원본 시트

- 검정: [PlatformTiles_Dark.png](../../Assets/Sprite/Map/Platforms/PlatformTiles_Dark.png)
- 아이보리: [PlatformTiles_Ivory.png](../../Assets/Sprite/Map/Platforms/PlatformTiles_Ivory.png)

두 시트는 Sprite / Multiple, 128 PPU로 분할돼 있다. 기존 타일의 픽셀·좌표·Sprite ID를 보존한다. 128px 셀 바깥에 2px 패딩이 있으므로 전체를 128px 격자로 다시 자동 분할하지 않는다.

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
| Automatic_Dark.asset | 검정 자동 타일 |
| Automatic_Ivory.asset | 아이보리 자동 타일 |
| Manual_Dark.asset | 검정 수동 타일 |
| Manual_Ivory.asset | 아이보리 수동 타일 |

자동 타일은 기본 사각과 곡선으로 구성된다. 수동 타일을 자동으로 교체하지는 않는다. 자동 타일의 외곽선 조합 참조도 수동 라이브러리 안의 타일을 사용한다.

## 안쪽 꼭짓점 마감

자동 `Square`는 인접한 두 면이 연결되고 대각선이 비어 있으면 해당 꼭짓점에 마감을 표시한다. 대각선을 채우면 마감이 사라지고, 지우면 다시 나타난다. 네 방향과 여러 꼭짓점의 동시 마감을 지원하며, 검정·아이보리의 기존 선 색과 두께를 따른다. 타일의 충돌 도형은 기존 외곽선 조합과 같다.

수동으로 지정하려면 **MainMap_TilePalette 맨 아래 Inner Join** 두 행을 사용한다. 각 행은 오른쪽 위 → 오른쪽 아래 → 왼쪽 아래 → 왼쪽 위 순서다. 검정은 y=-38, 아이보리는 y=-41이며 x=0, 3, 6, 9다. 기존 타일을 해당 마감 타일로 교체해 칠한다. 자동 처리 대상은 사각 타일이며, 곡선 부품의 형태를 바꾸지는 않는다.

## 수동 외곽선

특정 부분의 선을 직접 지정하려면 **CurveBorders_Dark / CurveBorders_Ivory** 팔레트를 사용한다.

T=위, R=오른쪽, B=아래, L=왼쪽, C=곡선이다. None은 선 없음이다. 아치의 C는 안쪽 원호를 뜻한다. 예를 들어 TLC는 곡선과 위·왼쪽에 선을 넣는다.

각 형태의 외곽선 32조합은 **8열 × 4줄**로 배치했다. `T=1, R=2, B=4, L=8, C=16`을 합한 값이 왼쪽부터 0~7, 다음 줄 8~15, 16~23, 24~31 순서다. None은 첫 줄 첫 번째, C는 세 번째 줄 첫 번째, TLC는 네 번째 줄 두 번째, 전체 경계는 마지막이다. 회전하면 방향도 함께 회전한다.

각 형태마다 32개 이름을 반복 표시하는 TextMesh 라벨은 팔레트에서 멀리 스크롤한 뒤에도 조합을 찾기 위한 것이다. 실제 지형 타일의 중복본은 아니다.

형태는 위에서부터 OuterSmall, OuterR1, OuterR2, InnerR1, InnerR2, InnerR3, RoundCap, RoundIsolated, SlimStraight, SlimCap, SlimIsolated, ArchR1, ArchR2, ArchR3 순서다. 부품 사이에는 한 칸 여백이 있다.

## 접합 범위

- 색이 달라도 맞닿는 면이면 선을 생략한다. 같은 Grid의 활성 TilemapCollider2D가 있는 지형끼리는 Tilemap이 달라도 연결을 판정한다. 충돌이 없는 배경은 제외한다.
- 얇은 부품이 넓은 면의 일부에만 닿으면 전체 선을 유지한다. 이런 부분은 맞는 접합 부품이나 수동 타일로 조정한다. Hierarchy에서 `Tilemap` 컴포넌트가 있는 지형 오브젝트를 선택하고 `Tools > CASTLING > Check Selected Platform Borders`를 실행한다. Console에 부분 접합 위치 경고와 검사한 자동 타일·부분 접합 수 요약만 출력하며, 타일을 수정하지 않는다. 경고 위치는 위의 부품·수동 외곽선 사용법에 따라 직접 조정한다.
- 곡선 형태 자체를 바꾸거나, 큰 부품을 잘라 생긴 새 경계를 만드는 기능은 없다. 곡선 구멍을 다른 Tilemap으로 겹쳐 메우는 방식도 지원하지 않는다.
- 서로 다른 Tilemap의 자동 갱신은 Editor 편집 기준이다.

## 기존 아치와 검증

기존 `OpeningArch_Left/Right`는 Ground 타일맵에 통합된 레거시 부품이다. 기존 4배 크기와 위치를 셀 변환으로 보존했고, `TileMap_Main_43/44`의 Sprite Physics Shape에는 기존 아치의 정밀한 충돌 곡선이 있다. 이를 일반 1×1 타일로 초기화하지 않는다. 새 지형은 위의 모듈형 곡선을 사용한다.

타일 변경 후에는 자동 접합·브러시·Undo/Redo와 실제 씬 충돌 경계를 확인한다. 검사 방법은 [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)을 따른다.
