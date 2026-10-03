# 배경 구조물 조립 타일

## 팔레트

- `BG_Structure_Modular_Near_TilePalette`: Structures_Near용, 기존 밝은 구조물 색 #B6B0AA.
- `BG_Structure_Modular_Far_TilePalette`: Structures_Far용, 기존 어두운 구조물 색 #4E5660.
- 팔레트 위쪽 왼쪽은 바깥 모서리, 오른쪽은 안쪽 곡선이다. 각 줄은 TL / TR / BR / BL 방향 순서다.
- 위에서 아래로 반경 ¼, ½, 1, 2, 3칸의 원호, 가로 3×세로 1칸 타원, 가로 1×세로 3칸 타원 순서다.
- 그 아래에는 채움 사각형과 ½칸·1칸 사선 모서리가 있다.
- 맨 아래에는 사각 기둥, 둥근 기둥, 둥근 돔, 아치, 원, 납작한 돔 조합 예시가 있다.

## 사용

- 기본 단위는 1×1칸이다. 모든 Sprite는 128×128px / 128 PPU / 중앙 Pivot이다.
- 큰 곡선도 실제 1×1 타일 여러 개로 나뉜다. 곡선 전체를 찍으려면 해당 조각의 직사각형 영역을 빈 칸까지 포함해 선택한다.
- 채움 사각형을 반복해서 폭과 높이를 늘리고, 가장자리에 모서리 조각을 붙인다.
- 위쪽 둥근 지붕은 같은 반경의 TL/TR 조각을 맞댄다. 아치 안쪽은 BR/BL 안쪽 곡선을 맞댄다.
- 회전·반전·스케일은 TileFlags.LockAll로 잠겨 있다. 팔레트에 있는 방향별 조각을 선택한다.
- 배경 전용이며 Collider는 없다. Dev_Gameplay의 룸1·룸2 배경 구조물은 원래 위치·외곽 크기·Tilemap 색 설정을 유지하면서, 반경 1칸의 원형 모서리와 채움 조각으로 교체했다. 연결된 구조물의 안쪽 모서리도 같은 반경으로 마감한다. 기존 고정 크기 팔레트는 별도로 남아 있다.

## 에셋과 원본

- Sprite atlas: `Assets/Sprite/Map/Background/Structures/Modular/BG_Structure_Modular.png`
- Unity Tile: `Assets/TilePalette/Background/ModularTiles/Near` 및 `Far`
- Palette prefab: `Assets/TilePalette/Background/BG_Structure_Modular_{Near,Far}_TilePalette.prefab`
- 수학적 원본: [BG_Structure_Modular_Source.svg](BG_Structure_Modular_Source.svg). 흰 마스크이며 Near/Far 색은 Tile.color로 적용된다.
- PNG는 원/타원 방정식의 4×4 픽셀 내부 샘플링으로 생성했다. 거울 방향을 동일한 좌표 규칙으로 계산하고, 동일한 1×1 마스크는 하나의 Sprite로 공유한다.
- 총 145개의 고유 1×1 마스크, 색상별 145 Tile, 2개 팔레트다. Texture는 Uncompressed / Point / Full Rect / mipmap 없음 / Sprite 간 2px 여백이다.
- 두 팔레트 모두 GridPalette.CellSizing.Manual 및 Grid.cellSize=(1,1,1)이다.
