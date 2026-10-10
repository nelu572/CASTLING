# 플랫폼 타일 사용법

## 원본 시트

- 검정: [PlatformTiles_Dark.png](../../Assets/Sprite/Map/Platforms/PlatformTiles_Dark.png)
- 아이보리: [PlatformTiles_Ivory.png](../../Assets/Sprite/Map/Platforms/PlatformTiles_Ivory.png)
- 흑백 혼합: [PlatformTiles_Mixed.png](../../Assets/Sprite/Map/Platforms/PlatformTiles_Mixed.png)

검정·아이보리 두 원본 시트는 Sprite / Multiple, 128 PPU로 분할돼 있다. 기존 타일의 픽셀·좌표·Sprite ID를 보존한다. 128px 셀 바깥에 2px 패딩이 있으므로 전체를 128px 격자로 다시 자동 분할하지 않는다. 혼합 시트도 128 PPU와 2px 패딩을 사용한다.

## 맵 칠하기

새 외곽선 계산 방식은 Unity Tile Palette에서 **Ground_NoOutline_TilePalette**를 선택한다. 왼쪽 검정, 가운데 아이보리, 오른쪽 혼합색이다. 아래의 기존 자동·수동 팔레트도 유지한다.

지형 팔레트는 선 없는 모양·기존 자동·수동 모양·수동 외곽선의 4개로 구분한다. 이름은 `영역_용도_변형_TilePalette` 형식이며 지형은 `Ground_`, 배경은 `BG_`로 시작한다. 변형이 없으면 해당 부분은 생략한다. 팔레트는 편집용 프리팹이며 실제 지형 타일은 아래 라이브러리 6개에 저장된다.

| 팔레트 | 용도 |
| --- | --- |
| Ground_NoOutline_TilePalette | 기본 발판·곡선·혼합색을 선 없이 칠하고, Grid에서 외곽선을 계산 |
| Ground_Auto_TilePalette | 위쪽 `Basic Platforms`: 기본 사각 발판·블록·기둥. 아래쪽 `Curves`: 자동 곡선 |
| Ground_Manual_Shapes_TilePalette | 왼쪽 `Manual Shapes`: 수동 곡선·기본 부품. 가운데 `Base / Inner Join`: 기존 타일과 안쪽 꼭짓점 마감. 오른쪽 `Mixed`: 흑백 혼합 곡선 |
| Ground_Manual_CurveBorders_TilePalette | 왼쪽 `Dark`, 오른쪽 `Ivory`: 수동 외곽선 32조합 |

수동 기본·곡선에서 픽셀·크기·색·충돌·잠금·셀 변환이 같은 낱개 모양은 한 번만 둔다. 여러 셀로 된 곡선 묶음과 빈 공간은 유지하며, 자동 타일과 수동 타일은 구분한다. 외곽선 마스크는 결과가 같아 보여도 선 선택 용도가 다르므로 32조합을 유지한다. 팔레트를 통합해도 기존 씬에서 사용하는 Tile·Sprite의 ID와 설정은 바꾸지 않는다.

`Ground_Manual_Shapes_TilePalette`는 **Cell Sizing: Manual**, Grid의 **Cell Size: (1, 1, 1)**을 유지한다. 가는 부품과 2×2·3×3 단일 Sprite가 함께 있어 Automatic에서는 셀이 3×3으로 계산되고 1×1 조각들이 떨어져 보인다. 팔레트 통합·수정 뒤에는 저장된 프리팹뿐 아니라 실제 Tile Palette 창을 다시 열어 셀 크기와 곡선 묶음의 접합을 확인한다.

- 사각 지형은 위쪽 `Basic Platforms`의 `Square 1x1` 한 칸을 집어 칠한다. 왼쪽 x=0은 검정, 오른쪽 x=14는 아이보리다.
- 기본 영역에는 `Platform 4x1`, `Platform 8x1`, `Block 4x2`, `Column 2x4` 묶음도 있다. 원하는 묶음 전체를 선택해 한 번에 찍고, 1×1 사각 타일로 길이·높이를 늘리거나 줄인다. 같은 기존 자동 사각 Tile을 조립한 선택 예시이므로 모양마다 새 Tile·Sprite를 만들지 않는다.
- 곡선·아치는 원하는 형태의 묶음 전체를 집어 칠한다. 빈 칸도 선택 영역에 포함한다.
- 회전·뒤집기는 기본 GridBrush를 사용한다. 크기는 부품 종류로 고르고 Transform Scale로 늘리지 않는다.
- 기존 자동 팔레트에서는 배치·지우기·Undo/Redo에 따라 노출 면에는 선이 생기고, 실제로 맞닿은 면에서는 선이 빠진다. 선 없는 팔레트의 갱신 방식은 아래 외곽선 계산 절차를 따른다.

| 부품 | 묶음 크기 |
| --- | --- |
| OuterSmall / OuterR1 | 1×1 |
| OuterR2 | 2×2 |
| InnerR1 / ArchR1 | 2×2 |
| InnerR2 / ArchR2 | 3×3 |
| InnerR3 / ArchR3 | 4×4 |
| RoundCap / RoundIsolated | 1×1 |
| SlimStraight / SlimCap / SlimIsolated | 1×1 |

타일은 `Assets/TilePalette/Main/Tiles/`의 라이브러리 6개에 하위 에셋으로 묶여 있다. Project 창에서 파일 왼쪽 화살표를 펼치면 개별 타일을 볼 수 있다.

| 라이브러리 | 포함 타일 |
| --- | --- |
| Automatic_Dark.asset | 검정 자동 타일 |
| Automatic_Ivory.asset | 아이보리 자동 타일 |
| Manual_Dark.asset | 검정 수동 타일 |
| Manual_Ivory.asset | 아이보리 수동 타일 |
| Manual_Mixed.asset | 검정·아이보리를 한 칸에 함께 넣은 수동 곡선 타일 28개 |
| NoOutline.asset | 선 없는 일반 Tile과 기존 타일에서의 변환 관계 |

자동 타일은 기본 사각과 곡선으로 구성된다. 수동 타일을 자동으로 교체하지는 않는다. 자동 타일의 외곽선 조합 참조도 수동 라이브러리 안의 타일을 사용한다.

## 선 없는 팔레트와 외곽선 계산

1. **Ground_NoOutline_TilePalette**에서 모양을 골라 Ground에 칠한다. 기본·곡선은 기존 자동 팔레트와 같은 배치이며 혼합색은 x=48·64에 있다. Cell Sizing은 Manual, Cell Size는 (1, 1, 1)이다.
2. Ground 또는 부모 Grid를 선택하고 **Tools > CASTLING > Platform Outlines > Rebuild Selected Grid**를 실행한다. Grid에 `PlatformOutlineBake`가 있으면 Inspector의 **외곽선 다시 계산** 버튼도 사용할 수 있다.
3. 기존 지형 Tile은 선 없는 Tile로 바꾸고, 전체 지형과 빈 공간 사이의 경계에 별도 Sprite 외곽선을 저장한다. 색·셀 변환·Sprite Physics Shape·Collider Type은 보존한다. Chapter_01의 현재 Ground에는 이 방식을 적용해 두었다.
4. Grid의 **Rebuild While Editing**은 기본으로 켜져 있으며, Chapter_01에도 켜 두었다. 칠하기·지우기가 끝나면 외곽선이 자동 갱신되고 타일 편집과 함께 Undo/Redo된다. 대량 편집 중 갱신을 잠시 끄면 지형 수정 뒤 **외곽선 다시 계산** 버튼을 누른다.

- **Width Pixels**는 128 PPU 기준 두께이며 1~64px이다. 초기값 14px은 기존 플랫폼 그림의 선 두께를 따른다.
- 색이 달라도 내부의 맞닿은 면에는 선이 생기지 않는다. 부분 접합과 다른 Tilemap으로 메운 곡선도 전체 Sprite 알파 실루엣의 합집합으로 계산한다. 충돌 없는 배경은 기본 계산 대상에서 제외한다.
- **Sources**가 비어 있으면 같은 Grid의 활성 TilemapCollider2D가 있는 Tilemap을 찾는다. 다른 지형 Tilemap을 추가·제외했으면 Sources를 비우거나 직접 갱신하고 다시 계산한다. 일반 Tile과 PlatformBorderTile을 지원하며, 다른 종류의 타일은 지형을 바꾸기 전에 계산을 중단한다.
- Grid 아래 `PlatformOutlines`는 생성 결과다. 직접 칠하거나 Collider를 추가하지 않는다. 런타임에는 계산하지 않으며 저장된 SpriteRenderer만 표시한다. Play 전에 필요한 재계산을 끝내고 씬을 저장한다.
- 결과 PNG는 `Assets/Sprite/Map/Platforms/GeneratedOutlines/`에 저장한다. 비어 있는 부분을 잘라 텍스처 크기를 줄이고, 같은 내용은 재사용한다. 이전 결과를 덮어쓰지 않아 Editor Undo가 이전 이미지를 참조할 수 있다. 이 폴더는 게임 에셋이며 일회성 캡처 폴더가 아니다. 오래된 파일의 정리는 씬·프리팹 참조와 필요한 Undo 상태가 끝난 뒤 Unity AssetDatabase를 통해 수행한다.
- 기존 원본의 선을 제거해 새 Sprite가 필요한 경우 `GeneratedFills/`에 PNG와 원본 Physics Shape를 함께 Import한다. 원본 Sprite·Tile ID는 유지한다. 새 팔레트 최초 생성은 **Create No Outline Palette** 메뉴를 사용하며, 이미 존재하는 팔레트는 덮어쓰지 않는다.
- 큰 지형에서는 전체 계산 시간이 늘어나므로 수동 버튼으로 묶어서 갱신할 수 있다. Tilemap의 배치·색·변환이나 선 두께를 Inspector에서 직접 바꿨을 때도 버튼으로 재계산한다. 자동 갱신은 Tilemap의 타일 변경 이벤트를 대상으로 한다.

Editor 구현은 `Assets/Editor/PlatformFillAuthoring.cs`와 `PlatformOutlineAuthoring.cs`에 있고, 저장용 컴포넌트·라이브러리는 `Assets/Scripts/Environment/PlatformOutlineBake.cs`와 `PlatformFillLibrary.cs`에 있다. 충돌·브러시·화면 확인은 [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)을 따른다.

## 흑백 사이의 곡선 경계

**Ground_Manual_Shapes_TilePalette 오른쪽 Mixed 영역**을 사용한다. x=76은 검정 곡선/아이보리 채움, x=92는 아이보리 곡선/검정 채움이다. 기존 곡선의 투명 부분을 반대 색으로 채웠으며, 내부 색 경계에는 외곽선을 넣지 않는다.

| 부품 | 선택 크기 | 시작 y |
| --- | --- | --- |
| OuterSmall | 1×1 | 0 |
| OuterR1 | 1×1 | -4 |
| OuterR2 | 2×2 | -8 |
| InnerR1 | 2×2 | -13 |
| InnerR2 | 3×3 | -18 |
| InnerR3 | 4×4 | -24 |

- 곡선 묶음 전체를 선택해 흑백이 맞닿는 계단 모양의 색 경계 위에 칠한다. 기본 GridBrush의 회전·뒤집기를 사용할 수 있다.
- 혼합 칸의 그림과 충돌은 모두 **가득 찬 사각 1×1**이다. 색만 곡선으로 나뉘므로 플랫폼 안쪽의 색 경계에 사용한다. 빈 공간과 닿는 지형의 외곽은 기존 자동·수동 곡선 타일로 만든다.
- 묶음의 단색 칸은 기존 선 없는 수동 Tile을 재사용한다. 혼합 타일은 기존 자동 타일의 접합 판정에도 가득 찬 사각으로 인식된다.
- 기존 씬을 자동으로 바꾸지는 않는다. 혼합 타일 제작 도구와 최초 생성 절차는 [아트 원본과 생성 도구](../../Tools/Art/README.md)를 따른다.

## 안쪽 꼭짓점 마감

자동 `Square`는 인접한 두 면이 연결되고 대각선이 비어 있으면 해당 꼭짓점에 마감을 표시한다. 대각선을 채우면 마감이 사라지고, 지우면 다시 나타난다. 네 방향과 여러 꼭짓점의 동시 마감을 지원하며, 검정·아이보리의 기존 선 색과 두께를 따른다. 타일의 충돌 도형은 기존 외곽선 조합과 같다.

수동으로 지정하려면 **Ground_Manual_Shapes_TilePalette 오른쪽 Base / Inner Join 영역의 맨 아래 Inner Join** 두 행을 사용한다. 각 행은 오른쪽 위 → 오른쪽 아래 → 왼쪽 아래 → 왼쪽 위 순서다. 검정은 y=-38, 아이보리는 y=-41이며 x=44, 47, 50, 53이다. 기존 타일을 해당 마감 타일로 교체해 칠한다. 자동 처리 대상은 사각 타일이며, 곡선 부품의 형태를 바꾸지는 않는다.

## 수동 외곽선

특정 부분의 선을 직접 지정하려면 **Ground_Manual_CurveBorders_TilePalette**를 사용한다. `Dark` 영역은 x=0에서, `Ivory` 영역은 x=44에서 시작하며 색별 부품·마스크의 상대 배치는 같다.

T=위, R=오른쪽, B=아래, L=왼쪽, C=곡선이다. None은 선 없음이다. 아치의 C는 안쪽 원호를 뜻한다. 예를 들어 TLC는 곡선과 위·왼쪽에 선을 넣는다.

각 형태의 외곽선 32조합은 **8열 × 4줄**로 배치했다. `T=1, R=2, B=4, L=8, C=16`을 합한 값이 왼쪽부터 0~7, 다음 줄 8~15, 16~23, 24~31 순서다. None은 첫 줄 첫 번째, C는 세 번째 줄 첫 번째, TLC는 네 번째 줄 두 번째, 전체 경계는 마지막이다. 회전하면 방향도 함께 회전한다.

각 형태마다 32개 이름을 반복 표시하는 TextMesh 라벨은 팔레트에서 멀리 스크롤한 뒤에도 조합을 찾기 위한 것이다. 실제 지형 타일의 중복본은 아니다.

형태는 위에서부터 OuterSmall, OuterR1, OuterR2, InnerR1, InnerR2, InnerR3, RoundCap, RoundIsolated, SlimStraight, SlimCap, SlimIsolated, ArchR1, ArchR2, ArchR3 순서다. 부품 사이에는 한 칸 여백이 있다.

## 기존 자동 타일의 접합 범위

- 색이 달라도 맞닿는 면이면 선을 생략한다. 같은 Grid의 활성 TilemapCollider2D가 있는 지형끼리는 Tilemap이 달라도 연결을 판정한다. 충돌이 없는 배경은 제외한다.
- 얇은 부품이 넓은 면의 일부에만 닿으면 전체 선을 유지한다. 이런 부분은 맞는 접합 부품이나 수동 타일로 조정한다. Hierarchy에서 `Tilemap` 컴포넌트가 있는 지형 오브젝트를 선택하고 `Tools > CASTLING > Check Selected Platform Borders`를 실행한다. Console에 부분 접합 위치 경고와 검사한 자동 타일·부분 접합 수 요약만 출력하며, 타일을 수정하지 않는다. 경고 위치는 위의 부품·수동 외곽선 사용법에 따라 직접 조정한다.
- 곡선 형태 자체를 바꾸거나, 큰 부품을 잘라 생긴 새 경계를 만드는 기능은 없다. 곡선 구멍을 다른 Tilemap으로 겹쳐 메우는 방식도 지원하지 않는다.
- 서로 다른 Tilemap의 자동 갱신은 Editor 편집 기준이다.

## 기존 아치와 검증

기존 `OpeningArch_Left/Right`는 Ground 타일맵에 통합된 레거시 부품이다. 기존 4배 크기와 위치를 셀 변환으로 보존했고, `TileMap_Main_43/44`의 Sprite Physics Shape에는 기존 아치의 정밀한 충돌 곡선이 있다. 이를 일반 1×1 타일로 초기화하지 않는다. 새 지형은 위의 모듈형 곡선을 사용한다.

타일 변경 후에는 자동 접합·브러시·Undo/Redo와 실제 씬 충돌 경계를 확인한다. 검사 방법은 [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)을 따른다.
