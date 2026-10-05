# Unity 하네스

## 에셋에 남겨야 하는 변경

- 씬 배치, 프리팹 구성, 컴포넌트와 Inspector 값은 실제 `.unity` 또는 `.prefab` 에셋에 저장한다.
- `Awake`·`Start` 코드로 씬의 기본 상태를 조립하지 않는다. 런타임 생성은 일회성 효과나 절차적 생성처럼 런타임 상태가 필요한 경우에만 사용한다.
- 재사용되는 킹·룩·퍼즐 오브젝트는 프리팹으로, 특정 스테이지에만 필요한 배치와 연출은 씬으로 관리한다.

## 시작 구조와 확장

- 기능이 생길 때 기존 구조에 맞춰 하위 폴더를 추가한다. 시작 템플릿의 폴더 구성을 이유로 현재 에셋 구조를 재편하지 않는다.
- 공용 기반 코드는 실제로 두 영역 이상에서 재사용될 때만 `Scripts/Shared`처럼 별도 폴더로 분리한다. 초기에 범용 계층을 미리 만들지 않는다.
- 플레이 실험은 `Scenes/_Development/Dev_Gameplay`에서 한다. 새 씬을 추가·이름 변경·삭제하면 `SceneNames` 상수와 Build Settings 등록 여부를 함께 검토한다.

## 씬·레이어·태그 이름

- 스크립트에서 참조하는 씬 이름은 `Scripts/Values/SceneNames.cs`, 커스텀 레이어는 `Scripts/Values/Layers.cs`, 태그는 `Scripts/Values/Tags.cs`에 둔다.
- 현재 레이어·태그는 Unity Project Settings와 대응 상수에서 확인한다. `MainCamera`처럼 Unity 기본 이름을 사용하는 경우에도 코드에서는 상수를 참조한다.
- 레이어·태그를 추가·이름 변경·삭제할 때는 Unity Project Settings와 대응 `Values` 상수를 같은 변경에 반영한다.

## 게임플레이 씬 계층 이름

`Assets/Scenes/_Development/Dev_Gameplay.unity`의 역할별 이름과 대소문자를 기준으로 게임플레이 씬을 구성한다.

```text
Cameras
├─ Main Camera
└─ PlayerCameraTarget
Grid                         # Grid 컴포넌트
└─ Room_01                   # RoomArea 컴포넌트
   ├─ Ground                 # 충돌 지형 Tilemap
   ├─ BackGround
   │  ├─ BaseColor
   │  ├─ ParallaxOrigin
   │  ├─ Structures_Far
   │  │  └─ Visual
   │  ├─ Structures_Mid      # 선택적인 중간 거리 구조물
   │  │  └─ Visual
   │  └─ Structures_Near
   │     └─ Visual
   ├─ Camera
   │  ├─ PlayerGroupCamera
   │  └─ CameraRoomBounds
   ├─ Entries
   │  └─ PlayerStartPositions # 최초 RoomEntry
   │     ├─ KingStart
   │     └─ RookStart
   └─ Exits
King
Rook
RoomTransitionCanvas
└─ Fade
```

- 룸 이름은 `Room_01`, `Room_02`처럼 두 자리 순번을 쓴다. `Opening`, `Exchange` 같은 퍼즐 설명은 공통 역할 이름에 붙이지 않는다.
- `Terrain`, `WorldBackground`, `WorldCamera`, `Entry_ChapterStart`처럼 같은 역할의 다른 이름을 만들지 않는다. 각각 `Ground`, `BackGround`, `Camera`, `Entries/PlayerStartPositions`를 사용한다.
- 별도 룸으로 들어오는 시작점은 `Entries/Entry_FromRoom01`, 출구는 `Exits/Exit_ToRoom02`처럼 연결 대상을 쓴다. `Exits`가 비어 있다는 이유로 전환 기능을 추가하지 않는다.
- 지형이 이어지는 한 공간은 퍼즐 구간 수와 관계없이 하나의 `RoomArea`로 유지한다.
- 챕터 전용 완료 오브젝트는 `Room_01/ChapterGoal`처럼 기능 이름을 쓴다. 배경 장식과 퍼즐 오브젝트는 해당 역할 아래에 설명 가능한 이름으로 추가한다.
- 룸의 장치는 `Puzzles` 아래에 둔다. 스위치 통로는 `Puzzles/PressureGatePair` 프리팹의 `Switch_Left`, `Switch_Right`, `Gate`처럼 역할별로 구분한다. 스위치·문 참조와 시각 오브젝트는 프리팹에 저장하고 런타임에 기본 구성을 조립하지 않는다.
- 공허 판정은 `Puzzles` 아래의 전용 장치로 관리한다. 재시작 위치는 별도 `RoomEntry`의 `KingStart`·`RookStart`를 사용한다. 판정 범위와 캐릭터·RoomEntry·Fade 참조는 씬에 저장하며, 기존 최초 RoomEntry 참조를 임의로 바꾸지 않는다.
- 배경은 `Structures_Near/Visual/Platforms_01`처럼 레이어의 Visual 아래에 둔다. 타일 그림은 원래 크기로 사용하고 Tilemap의 Transform Scale은 1로 유지한다. 패럴랙스는 Visual 루트만 이동한다. 배경에는 Collider를 추가하지 않는다.
- 이전 초안을 남길 때는 `Archive_Original`, `Archive_Continuous`처럼 보관용 루트임을 표시하고 비활성 상태를 유지한다. 기존 초안의 컴포넌트 구성은 보존하며, 공통 자식 역할 이름은 위 기준을 따른다.
- `RoomTransitionController.roomsRoot`에는 플레이에 사용하는 `Grid`만 연결한다. 비활성 초안까지 검색하므로 보관용 루트는 이 `Grid` 밖에 둔다.
- 이름·부모 변경 시 월드 배치, Grid 설정, 타일, 카메라·시작점·완료 조건의 참조를 보존한다. 검증 도구도 가능한 한 컴포넌트 참조를 사용하며 과거 루트 이름에 의존하지 않는다.

## 안전한 변경과 확인

- Unity가 만든 `.unity`, `.prefab`, `.asset`의 YAML과 `.meta` 파일을 임의로 편집하지 않는다.
- 새 에셋·이동한 에셋은 대응 `.meta` 파일을 함께 관리한다.
- 씬·프리팹 변경 뒤에는 Unity Editor에서 누락된 참조와 컴포넌트 경고를 확인한다.
- 지형 타일을 대량 변경하면 Play Mode에서 실제 충돌 높이를 Raycast로 확인한다. RefreshAllTiles·ProcessTilemapChanges·GenerateGeometry 후에도 이전 윤곽이 남으면 TilemapCollider2D를 다시 활성화하고 maximumTileChangeCount를 일시적으로 0으로 두어 전체를 재생성한 뒤 기존 값을 복원한다. 갱신한 충돌 도형도 씬에 저장한다.

## 룸·타일 작업 절차

- 룸 생성·전환 연결은 [룸 만들기](../../.reference/guides/ROOM_AUTHORING.md)를 따른다.
- 배경 타일 조립은 [배경 구조물 조립](../../.reference/guides/BG_STRUCTURE_MODULAR.md), 지형 타일 사용은 [플랫폼 타일 사용법](../../.reference/guides/PLATFORM_SHEETS.md)을 따른다.
- 특정 챕터의 배치·색·카메라·물리 수치는 현재 씬과 컴포넌트에서 확인한다. 과거 제작 기록의 수치를 공통 작업 규칙으로 적용하지 않는다.

## 스크린샷과 임시 산출물

- 구도·배경·카메라·UI처럼 화면 결과를 확인해야 하는 변경에서 필요한 장면만 캡처한다. 관련 화면 변화가 없는 작업에는 스크린샷을 의무로 만들지 않는다.
- 플레이 화면은 실제 Game View를 캡처한다. 현재 Coplay의 inline 합성 캡처는 Play Mode에서 `include_image=true`, `camera` 미지정으로 사용한다. Edit Mode의 inline 캡처와 지정 카메라 캡처는 카메라 렌더이며, 합성 캡처도 실패하면 카메라 렌더로 대체될 수 있다. 반환된 `captureSource=game_view`만으로 실제 화면 캡처라고 판단하지 말고 레터박스·viewport·UI 합성을 이미지에서 확인한다. 패키지가 바뀌면 이 동작을 다시 확인한다.
- 캡처·참고 이미지 복사본·시안 SVG/HTML·일회성 검사 코드·JSON·로그는 [.work 안내](../../.work/README.md)에 따라 `.work/<날짜>-<작업명>/`에 모으고 용도·확인 조건·정리 시점을 적는다. 캡처의 `output_folder`도 이 경로로 지정하고 반환 경로를 확인해 `Assets/Screenshots`에 임시 에셋이 생기지 않게 한다.
- 재사용할 아트 편집 원본·생성 도구는 [Tools/Art](../../Tools/Art/README.md)에 사용 방법·출력 대상·검증 상태와 함께 보존한다. 파일 확장자만으로 편집 원본을 임시 시안과 같이 삭제하지 않는다.
- 임시 자료는 [.work 안내](../../.work/README.md)의 시점에 맞춰 작업 중에 정리한다. 검사·구현 단락이 끝나거나 검토가 완료되면 필요 없어진 파일과 빈 작업 폴더를 별도 정리 요청 없이 정리한다. 사용자 검토 대기·문제 미해결·보존 자료는 필요한 것만 유지하고 작업별 안내와 보고에 경로·이유·정리 조건을 남긴다. 로그 잠금 처리와 삭제 범위도 해당 안내를 따르며, `.work/README.md`와 루트는 남긴다.
- 결과는 작업 보고에 확인 범위와 한계를 짧게 남기고, 다음 작업에도 필요한 방법·주의점만 하네스·작업 절차·사용 안내에 반영한다. `.codex`와 `.reference`에 캡처 묶음이나 일회성 검사 결과를 상시 보관하지 않는다. 세부 확인 절차는 [Unity 변경 검증](../skills/UNITY_VERIFY.md)을 따른다.
