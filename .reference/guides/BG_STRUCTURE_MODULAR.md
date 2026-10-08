# 배경 구조물 조립 타일

## 배경 바탕

- `BG_Base_TilePalette`를 선택하고 룸의 `BackGround/BaseColor`를 Active Tilemap으로 지정한다.
- 기존 베이지·청회색 타일 오른쪽 `(1, 1)`의 `TileMap_BackGroundBase_Gray`는 그림 자체에 회색을 넣은 1×1 단색 채움 타일이다. [PNG](../../Assets/Sprite/Map/Background/Base/TileMap_BackGroundBase_Gray.png)와 [Tile](../../Assets/TilePalette/Background/Tiles/TileMap_BackGroundBase_Gray.asset)을 사용한다.
- Tilemap과 셀 색은 흰색으로 유지하면 팔레트에 보이는 원본 색으로 칠할 수 있다. Transform Scale은 1이며 Collider는 추가하지 않는다.
- 기본색은 타일로 편집한다. 그라데이션이 필요하면 기본색 위에서 명도를 변화시키는 별도 효과로 다룬다. 현재 회색 타일에는 그라데이션이 없다.
- `Chapter_01`의 `BackGround/BrightnessGradient`는 [흑백 알파 PNG](../../Assets/Sprite/Map/Background/Base/BG_BrightnessGradient.png)를 BaseColor 위·구조물 뒤에 한 장으로 표시한다. 위쪽은 밝고 아래쪽은 어두우며, 타일마다 반복하지 않는다.
- `BrightnessGradient`의 SpriteRenderer Color는 RGB를 흰색으로 유지하고 A로 강도를 조절한다. A=0이면 효과가 꺼지고, A=1이면 PNG의 명도 차이가 그대로 적용된다. Size로 적용 범위를 맞추고 Transform Scale은 1로 유지한다. 끄면 원래 BaseColor만 보인다.
- 그라데이션 PNG는 Bilinear / Clamp / Uncompressed / mipmap 없음으로 Import한다. 단색 타일·모듈의 Point 설정과 구분한다.

## 팔레트

- 이름은 `영역_용도_변형_TilePalette` 형식을 사용하며 배경은 `BG_`로 시작한다. 변형이 없으면 해당 부분은 생략한다.
- `BG_Structure_Modular_TilePalette`: 왼쪽 `Near`는 x=0, 오른쪽 `Far`는 x=52에서 시작한다. 같은 모양의 두 색 계열을 한 팔레트에서 고른다. 기존 Near·Far 색은 새 배경의 명도 기준으로 고정하지 않는다.
- `BG_Structure_Modular_Neutral_TilePalette`: 셀별 색 편집용 중성 타일이다. 현재 팔레트에는 채움 사각형과 ¼칸 바깥 곡선 4방향을 둔다.
- `BG_Structure_Pieces_TilePalette`: 기존 배경 낱개 부품 38개다. `Pieces`에서 같은 픽셀·크기·색·셀 설정의 중복을 제외했으며, 오른쪽 `Custom Shapes`의 조합과 셀 색을 보존한다.
- `BG_Structure_Stamps_TilePalette`: 큰 크기의 고정 부품과 조합 예시다. 낱개 부품과 사용 단위가 달라 별도 팔레트로 유지한다.
- Modular 팔레트의 각 색 영역 위쪽 왼쪽은 바깥 모서리, 오른쪽은 안쪽 곡선이다. 각 줄은 TL / TR / BR / BL 방향 순서다.
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
- Palette prefab: `Assets/TilePalette/Background/BG_Structure_Modular_TilePalette.prefab`, `BG_Structure_Modular_Neutral_TilePalette.prefab`
- 기존 부품·고정 조합: `Assets/TilePalette/Background/BG_Structure_Pieces_TilePalette.prefab`, `BG_Structure_Stamps_TilePalette.prefab`. 팔레트에서 중복을 정리해도 씬이 사용하는 원본 Tile·Sprite 참조는 유지한다.
- Texture는 Uncompressed / Point / Full Rect / mipmap 없음 / Sprite 간 2px 여백이다.
- GridPalette.CellSizing은 Manual, Grid.cellSize는 (1,1,1)이다.

## 편집 원본

[기하 원본 SVG](../../Tools/Art/BG_Structure_Modular_Source.svg)는 모듈의 원·타원·사선 형태를 수정할 때 사용한다. 도면의 배치는 게임 atlas의 배치와 다르며, 현재 atlas를 동일하게 재생성하는 전체 실행 경로는 없다. 편집과 보존 방법은 [아트 원본 안내](../../Tools/Art/README.md)를 따른다.

기존 제작 방식은 원·타원 수식에 따른 내부 4×4 샘플링으로 픽셀의 알파를 구하고, 동일한 1×1 마스크는 같은 Sprite를 공유하는 방식이다. 마스크는 흰색이며 명도·색은 Tile 색상으로 분리한다. 재제작 시에는 현재 PNG·Sprite ID·분할·패딩을 확인하고 보존한다.

현재 배경의 합성 화면과 거리층별 화면을 실제 Game View에서 확인한다. 큰 면의 가림·여백과 플레이 영역의 가독성을 먼저 보고, 이동·확대 중에도 확인한다. 검증 범위는 [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)을 따른다.
