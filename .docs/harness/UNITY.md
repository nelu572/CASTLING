# Unity 하네스

## 에셋에 남겨야 하는 변경

- 씬 배치, 프리팹 구성, 컴포넌트와 Inspector 값은 실제 `.unity` 또는 `.prefab` 에셋에 저장한다.
- `Awake`·`Start` 코드로 씬의 기본 상태를 조립하지 않는다. 런타임 생성은 일회성 효과나 절차적 생성처럼 런타임 상태가 필요한 경우에만 사용한다.
- 재사용되는 킹·룩·퍼즐 오브젝트는 프리팹으로, 특정 스테이지에만 필요한 배치와 연출은 씬으로 관리한다.

## 시작 구조와 확장

```text
Assets/
├─ Art/                  # 모델, 스프라이트, 머티리얼 등 시각 에셋
├─ Audio/                # BGM·효과음
├─ Prefabs/              # 재사용 오브젝트
├─ Scenes/               # 게임 씬과 개발용 실험 씬
├─ Scripts/              # 기능·도메인별 코드
└─ Settings/             # Unity·렌더링 설정
```

- `Characters`, `Gameplay`, `UI`는 현재 추가된 시작 폴더일 뿐, 고정된 최상위 분류가 아니다. 기능이 생길 때 그 기능의 이름으로 하위 폴더를 추가한다.
- 공용 기반 코드는 실제로 두 영역 이상에서 재사용될 때만 `Scripts/Shared`처럼 별도 폴더로 분리한다. 초기에 범용 계층을 미리 만들지 않는다.
- 플레이 실험은 `Scenes/_Development/Dev_Gameplay`에서 한다. 새 씬을 추가·이름 변경·삭제하면 `SceneNames` 상수와 Build Settings 등록 여부를 함께 검토한다.

## 씬·레이어·태그 이름

- 스크립트에서 참조하는 씬 이름은 `Scripts/Values/SceneNames.cs`, 커스텀 레이어는 `Scripts/Values/Layers.cs`, 태그는 `Scripts/Values/Tags.cs`에 둔다.
- 현재 사용자 레이어는 `Player`, `Environment`, `Interactable`이며, 태그는 `Player`를 사용한다. `MainCamera`는 Unity 기본 태그를 상수로만 참조한다.
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
- 지형이 이어지는 한 공간은 퍼즐 구간 수와 관계없이 하나의 `RoomArea`로 유지한다. Chapter_01은 `Grid/Room_01` 하나를 사용한다.
- 챕터 전용 완료 오브젝트는 `Room_01/ChapterGoal`처럼 기능 이름을 쓴다. 배경 장식과 퍼즐 오브젝트는 해당 역할 아래에 설명 가능한 이름으로 추가한다.
- 룸의 장치는 `Puzzles` 아래에 둔다. 스위치 통로는 `Puzzles/PressureGatePair` 프리팹의 `Switch_Left`, `Switch_Right`, `Gate`처럼 역할별로 구분한다. 스위치·문 참조와 시각 오브젝트는 프리팹에 저장하고 런타임에 기본 구성을 조립하지 않는다.
- 공허 판정은 `Puzzles/VoidFallZone` 프리팹 인스턴스에 둔다. Chapter_01의 구간 재시작 위치는 `Entries/RookGapStartPositions/KingStart·RookStart`로 관리한다. 판정 범위와 캐릭터·RoomEntry·Fade 참조는 씬에 저장하며, 기존 최초 RoomEntry 참조를 바꾸지 않는다.
- 배경은 `Structures_Near/Visual/Platforms_01`처럼 레이어의 Visual 아래에 둔다. Chapter_01의 먼 플랫폼은 기존 모듈형 타일을 원래 크기로 연결하며, Tilemap Transform Scale은 1로 유지한다. 패럴랙스는 Visual 루트만 이동한다. 배경에는 Collider를 추가하지 않는다.
- Chapter_01 배경의 시각 설정은 Dev_Gameplay Room_01을 따른다. Near는 RGB 0.8·알파 1·정렬 10·가로/세로 스크롤 0.7, Far는 RGB 1·알파 1·Background_Far 머티리얼·정렬 5·가로/세로 스크롤 0.35다. 기물 구조물은 Far/Visual 아래에 흰색·알파 1·같은 머티리얼·정렬 6으로 둔다. Main Camera·Visual·ParallaxOrigin 참조는 씬에 저장한다. 각 층의 Tilemap 색은 자식 Tilemap에 자동 상속되지 않으므로 자식 플랫폼도 직접 확인한다.
- 이전 초안을 남길 때는 `Archive_Original`, `Archive_Continuous`처럼 보관용 루트임을 표시하고 비활성 상태를 유지한다. 기존 초안의 컴포넌트 구성은 보존하며, 공통 자식 역할 이름은 위 기준을 따른다.
- `RoomTransitionController.roomsRoot`에는 플레이에 사용하는 `Grid`만 연결한다. 비활성 초안까지 검색하므로 보관용 루트는 이 `Grid` 밖에 둔다.
- 이름·부모 변경 시 월드 배치, Grid 설정, 타일, 카메라·시작점·완료 조건의 참조를 보존한다. 검증 도구도 가능한 한 컴포넌트 참조를 사용하며 과거 루트 이름에 의존하지 않는다.

## 안전한 변경과 확인

- Unity가 만든 `.unity`, `.prefab`, `.asset`의 YAML과 `.meta` 파일을 임의로 편집하지 않는다.
- 새 에셋·이동한 에셋은 대응 `.meta` 파일을 함께 관리한다.
- 씬·프리팹 변경 뒤에는 Unity Editor에서 누락된 참조와 컴포넌트 경고를 확인한다.
- 지형 타일을 대량 변경하면 Play Mode에서 실제 충돌 높이를 Raycast로 확인한다. RefreshAllTiles·ProcessTilemapChanges·GenerateGeometry 후에도 이전 윤곽이 남으면 TilemapCollider2D를 다시 활성화하고 maximumTileChangeCount를 일시적으로 0으로 두어 전체를 재생성한 뒤 기존 값을 복원한다. 갱신한 충돌 도형도 씬에 저장한다.
- 레터박스가 있는 플레이 화면은 screenshot에서 camera를 지정하지 않고 Game View를 캡처한다. 카메라를 별도 RenderTexture로 렌더하면 원래 viewport와 다른 비율로 화면 밖 지형까지 보일 수 있다.
