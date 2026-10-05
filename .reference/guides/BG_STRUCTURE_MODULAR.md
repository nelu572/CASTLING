# 배경 구조물 조립 타일

## 팔레트

- `BG_Structure_Modular_Near_TilePalette` / `BG_Structure_Modular_Far_TilePalette`: 기존 Near·Far 색을 가진 타일이다. 새 배경의 명도 기준으로 고정하지 않는다.
- `BG_Structure_Neutral_TilePalette`: 기존 모듈형 Sprite를 쓰는 중성 타일이다. 셀별 색을 편집할 때 사용한다.
- 팔레트 위쪽 왼쪽은 바깥 모서리, 오른쪽은 안쪽 곡선이다. 각 줄은 TL / TR / BR / BL 방향 순서다.
- 위에서 아래로 반경 ¼, ½, 1, 2, 3칸의 원호, 가로 3×세로 1칸 타원, 가로 1×세로 3칸 타원 순서다.
- 그 아래에는 채움 사각형과 ½칸·1칸 사선 모서리가 있다.
- 맨 아래에는 사각 기둥, 둥근 기둥, 둥근 돔, 아치, 원, 납작한 돔 조합 예시가 있다.

## 사용

- 기본 단위는 1×1칸이다. Sprite는 128×128px / 128 PPU / 중앙 Pivot이다.
- 큰 곡선은 1×1 타일 여러 개로 나뉜다. 곡선 전체를 찍으려면 해당 조각의 직사각형 영역을 빈 칸까지 포함해 선택한다.
- 채움 사각형을 반복해서 폭과 높이를 늘리고, 가장자리에 모서리 조각을 붙인다.
- 위쪽 둥근 지붕은 같은 반경의 TL/TR 조각을 맞댄다. 아치 안쪽은 BR/BL 안쪽 곡선을 맞댄다.
- 고정 색 Near·Far Tile은 `TileFlags.LockAll`, Neutral Tile은 색 잠금 없이 변환만 잠근다. 팔레트의 방향별 조각을 선택하고 Tilemap Transform Scale은 1로 유지한다.
- 배경 전용이며 Collider는 없다. 고정 색 팔레트는 같은 색 계열의 Tile로 편집한다.
- Neutral Tilemap은 `Tilemap.color`를 흰색으로 두고 셀별 `SetColor`로 색을 저장한다. 현재 셀 색과 Inspector 튜닝을 먼저 확인하고, 과거 기록의 색상으로 덮어쓰지 않는다.

## 에셋과 Import

- Sprite atlas: [BG_Structure_Modular.png](../../Assets/Sprite/Map/Background/Structures/Modular/BG_Structure_Modular.png)
- Unity Tile: `Assets/TilePalette/Background/ModularTiles/Near`, `Far`, `Neutral.asset`
- Palette prefab: `Assets/TilePalette/Background/BG_Structure_Modular_{Near,Far}_TilePalette.prefab`, `BG_Structure_Neutral_TilePalette.prefab`
- Texture는 Uncompressed / Point / Full Rect / mipmap 없음 / Sprite 간 2px 여백이다.
- GridPalette.CellSizing은 Manual, Grid.cellSize는 (1,1,1)이다.

## 편집 원본

[기하 원본 SVG](../../Tools/Art/BG_Structure_Modular_Source.svg)는 모듈의 원·타원·사선 형태를 수정할 때 사용한다. 도면의 배치는 게임 atlas의 배치와 다르며, 현재 atlas를 동일하게 재생성하는 전체 실행 경로는 없다. 편집과 보존 방법은 [아트 원본 안내](../../Tools/Art/README.md)를 따른다.

기존 제작 방식은 원·타원 수식에 따른 내부 4×4 샘플링으로 픽셀의 알파를 구하고, 동일한 1×1 마스크는 같은 Sprite를 공유하는 방식이다. 마스크는 흰색이며 명도·색은 Tile 색상으로 분리한다. 재제작 시에는 현재 PNG·Sprite ID·분할·패딩을 확인하고 보존한다.

현재 배경의 합성 화면과 거리층별 화면을 실제 Game View에서 확인한다. 큰 면의 가림·여백과 플레이 영역의 가독성을 먼저 보고, 이동·확대 중에도 확인한다. 검증 범위는 [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)을 따른다.
