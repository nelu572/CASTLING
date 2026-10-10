# 아트 원본과 생성 도구

배경·플랫폼 에셋의 편집 원본과 생성 도구를 보존한다. 형태를 수정하거나 재제작할 때 읽는다. 게임에 사용되는 PNG와 Unity Import 설정은 `Assets/`에서 관리한다.

## 파일과 저장 기준

| 파일 | 역할 | 결과물 관계 |
| --- | --- | --- |
| [BG_Structure_Modular_Source.svg](BG_Structure_Modular_Source.svg) | 원·타원·사선 모듈의 편집 가능한 기하 원본 | [BG_Structure_Modular.png](../../Assets/Sprite/Map/Background/Structures/Modular/BG_Structure_Modular.png)의 형태를 수정할 때 참고 |
| [generate_background_chess.py](generate_background_chess.py) | 체스말 실루엣과 체커 패턴을 생성하는 기존 Python 도구 | `Assets/Sprite/Map/Background/Landmarks/`, `Patterns/`의 아래 PNG를 덮어씀 |
| [create_platform_transitions.cs](create_platform_transitions.cs) | 기존 곡선의 빈 부분을 반대 색으로 채우는 Unity Editor 최초 생성 코드 | `PlatformTiles_Mixed.png`, `Manual_Mixed.asset` 생성과 기존 수동 모양 팔레트의 Mixed 영역 추가 |

이 폴더의 원본·도구·안내는 Git 보존 대상이다. 사용 안내는 [배경 구조물 조립](../../.reference/guides/BG_STRUCTURE_MODULAR.md), 추가 기준은 [자료 추가 양식](../../.codex/harness/TEMPLATES.md)을 따른다.

## SVG 편집 방법

1. SVG 편집기에서 원본을 열고 필요한 모듈의 경로를 수정한다. 1칸은 128px이며 방향별 모듈은 대칭 변환으로 구성된다.
2. 원본은 형태를 모아 둔 도면이다. PNG atlas의 조각 배치와 동일한 파일이 아니며, SVG를 그대로 PNG로 내보내 현재 atlas를 교체하지 않는다.
3. 게임 atlas 변경이 요청되면 별도로 래스터화·1×1 조각 분할·패킹 방법을 정하고, 기존 Sprite ID와 참조를 보존해 Unity Editor에서 확인한다. 현재 원본만으로 atlas와 Import 정보를 동일하게 재생성하는 실행 경로는 없다.

## 체스 배경 생성 도구

Python과 Pillow가 필요하다. 모듈을 import할 때는 파일이나 폴더를 만들지 않는다. 명령으로 실행하면 선택한 파일을 생성·덮어쓰며, `--only`를 생략하면 다음 7개를 모두 생성한다.

- `Landmarks/BG_PawnLandmark.png`
- `Landmarks/BG_BishopLandmark.png`
- `Landmarks/BG_KnightLandmark.png`
- `Landmarks/BG_QueenLandmark.png`
- `Landmarks/BG_KingLandmark.png`
- `Patterns/BG_CheckerTile_A.png`
- `Patterns/BG_CheckerTile_B.png`

재생성이 요청된 경우에만 현재 PNG와 사용자 수정 여부, 출력 폴더, 코드에 고정된 색상·크기를 확인한 뒤 저장소 루트에서 실행한다. 코드의 색상은 현재 씬의 색상 기준을 대신하지 않는다.

- `--output-dir`: `Landmarks`와 `Patterns`를 둘 출력 루트. 생략하면 기존 `Assets/Sprite/Map/Background`에 저장한다.
- `--only`: `pawn`, `bishop`, `knight`, `queen`, `king`, `checker-a`, `checker-b` 중 필요한 출력만 지정한다. 선택하지 않은 파일은 유지한다.

먼저 [.work 안내](../../.work/README.md)에 따라 임시 경로에 생성해 결과를 확인한다. 다음 예시는 Pawn과 패턴 A만 만든다.

```powershell
python Tools/Art/generate_background_chess.py --output-dir .work/20261006-chess-check --only pawn checker-a
```

기존 에셋을 직접 재생성할 때도 요청된 파일만 `--only`로 지정한다. 출력 선택 없이 실행하면 7개 모두 덮어쓴다.

## 검증 상태와 한계

- SVG는 이전에 Git에 보존된 기하 원본을 그대로 옮긴 것이다. 기존 PNG를 재생성하지 않았다.
- Python 도구는 임시 경로에서 기본 7개 출력이 기존 생성 코드와 동일한지 비교하고, import 시 출력 없음·선택 생성·기존 비선택 파일 보존·잘못된 선택 거부를 확인했다. 실제 게임 PNG를 재생성하거나 Unity Import를 검증하지 않았다.
- 생성 후에는 바뀐 PNG의 픽셀·참조·Import 설정과 실제 화면을 [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)에 따라 확인한다.

## 흑백 혼합 플랫폼 생성 도구

- Unity MCP `execute_code`에서 파일의 전체 내용을 C# 메서드 본문으로 실행한다. `Assets/`의 런타임·Editor 스크립트로 Import하는 파일이 아니다. Unity 6000.0.68f1, 2D Sprite/Tilemap 패키지와 현재 `PlatformTileLibrary`·`PlatformBorderTile`이 필요하다.
- 기존 `Automatic_Dark.asset`·`Automatic_Ivory.asset`의 선 없는 곡선 Sprite를 현재 Import 좌표로 읽고, PNG 원본의 픽셀과 반대 색의 채움 타일을 합성한다. 원본 PNG·Sprite ID·기존 Tile과 열린 씬은 변경하지 않는다.
- 최초 생성 시 `Assets/Sprite/Map/Platforms/PlatformTiles_Mixed.png`와 `Assets/TilePalette/Main/Tiles/Manual_Mixed.asset`를 만든다. Unity Sprite Data Provider로 128 PPU, 128px 셀, 2px 색 확장 패딩, 완전한 사각 Physics Shape를 설정한다.
- `Ground_Manual_Shapes_TilePalette.prefab`의 x=76·92에 6종의 곡선 묶음과 라벨을 추가한다. 묶음에 필요한 단색 칸은 기존 수동 타일을 재사용한다. 그림이 실제로 혼합된 타일만 새 라이브러리에 저장한다.
- 기존 출력 파일이나 새 팔레트 영역의 타일이 있으면 실행을 중단한다. 재제작은 사용자 변경과 기존 Sprite·Tile ID를 먼저 확인하고 별도의 갱신 코드를 사용한다. 이 코드는 현재 출력을 덮어쓰는 재생성 도구가 아니다.
- 최초 생성으로 28개 혼합 Sprite·Tile과 70칸의 팔레트 묶음을 확인했다. 임시 Preview Scene에서 사각 충돌·회전·뒤집기·기존 자동 타일의 같은/다른 Tilemap 접합과 기본 GridBrush의 칠하기·지우기·Undo/Redo를 확인했다. 실제 Tile Palette 창도 다시 열어 Manual 셀 크기 (1, 1, 1)과 곡선 묶음의 접합을 확인했다. 실제 씬에는 칠하지 않았으므로 배치 후 Play Mode 충돌 확인은 [플랫폼 타일 사용법](../../.reference/guides/PLATFORM_SHEETS.md)과 [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)을 따른다.
- 형태·출력·팔레트 배치가 변경되면 이 코드와 사용 안내를 함께 갱신한다. 검사 코드·비교 화면은 `.work/`에만 두고 검사·검토 종료 시 정리한다.

## 보존과 정리

원본이나 도구의 입출력·사용 방법이 바뀌면 이 안내도 갱신한다. 시안·캡처·일회성 출력은 `.work/<날짜>-<작업명>/`에 두고 검토 뒤 정리한다. 실제 에셋이나 편집 원본을 임시 결과물로 취급해 삭제하지 않는다.
