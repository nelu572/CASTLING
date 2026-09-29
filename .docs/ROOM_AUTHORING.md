# Dev_Gameplay 룸 만들기

현재 `Dev_Gameplay`은 한 씬의 `Grid` 아래에 여러 룸을 두는 구조다. 각 룸은 자기 지형, 배경, 카메라 경계, 입장 지점, 출구를 가진다.

```text
Grid
├─ Room_01 (RoomArea)
└─ Room_02 (RoomArea)
   ├─ Ground
   ├─ BackGround
   ├─ Camera
   │  ├─ PlayerGroupCamera
   │  └─ CameraRoomBounds
   ├─ Entries
   │  └─ Entry_FromRoom01 (RoomEntry)
   │     ├─ KingStart
   │     └─ RookStart
   └─ Exits
```

## Room_03을 만드는 순서

1. `Assets/Scenes/_Development/Dev_Gameplay`을 열고 플레이 모드와 `Tools > CASTLING > Live Camera & Parallax Preview`를 끈다. Hierarchy에서 `Room_02` 또는 그 자식을 선택한 뒤 `Tools > CASTLING > 룸 > 선택한 룸 복제`를 누른다. 새 룸은 `Room_03`처럼 다음 번호를 받고, 기존 룸의 화면 요소와 겹치지 않는 오른쪽 공간에 놓인다.
2. `Ground` 타일맵에서 바닥과 천장을 함께 편집한다. `BackGround/BaseColor`와 `Structures_Near`, `Structures_Far`도 새 룸에 맞게 편집한다.
3. `Camera/CameraRoomBounds`의 `BoxCollider2D` 범위를 새 룸에 맞춘다. 복제 도구가 `RoomArea`와 카메라 `CinemachineConfiner2D`를 **같은 새 콜라이더**에 연결하므로 이 박스의 크기와 위치만 조절하면 된다. 지형 크기에서 카메라 범위를 자동 계산하지는 않는다.
4. `Entries/Entry_FromRoom01`을 `Entry_FromRoom02`처럼 실제 진입 경로에 맞게 이름을 바꾼다. 그 아래 `KingStart`, `RookStart`를 플레이어가 도착할 자리로 옮긴다. `Tools > CASTLING > 시작 위치 > 시작 좌표 보기`에서 이 Entry를 선택해 좌표를 수정하거나 씬 뷰로 이동할 수 있다.
5. 출발 룸인 `Room_02/Exits` 아래에 `Exit_ToRoom03` 빈 오브젝트를 만든다. `RoomExit`을 추가하면 `BoxCollider2D`도 추가된다. 콜라이더의 `Is Trigger`를 켜고 출구 영역에 맞게 배치한다. `RoomExit > Destination`에 `Room_03/Entries/Entry_FromRoom02`를 지정한다. 복제된 룸에 원래 있던 출구는 목적지가 비워지므로 필요한 것만 다시 연결한다. 현재 전환 조건은 **킹과 룩의 중심이 동시에 이 콜라이더 안에 들어오는 것**이다.
6. 씬을 저장하고 플레이한다. 두 캐릭터를 출구에 넣어 Room_03 입장 위치, 카메라 경계, 룸별 배경 전환, Console 오류 여부를 확인한다.

## 여러 갈래와 시작 룸

- 한 룸에서 여러 곳으로 갈 때는 그 룸의 `Exits`에 `RoomExit`을 더 만들고, 각각 다른 `RoomEntry`를 `Destination`으로 지정한다.
- 같은 룸으로 들어오는 경로마다 도착 위치가 달라야 하면 대상 룸의 `Entries`에 `RoomEntry`를 더 만든다. 각 Entry에 킹·룩 마커를 배치한다.
- 되돌아가는 길도 대상 룸의 Entry와 출발 룸의 Exit을 같은 방식으로 연결한다.
- 게임 자체의 시작 위치를 바꾸려면 `Cameras/PlayerCameraTarget`의 `RoomTransitionController > Starting Entry`를 원하는 Entry로 바꾼다. 좌표 창의 **킹·룩 이동** 버튼은 편집 중인 씬에서 캐릭터를 옮기는 기능이며, 이 `Starting Entry` 설정은 바꾸지 않는다.
- 새 `RoomArea`를 `Grid` 아래에 두면 전환 컨트롤러가 자동으로 찾는다. 룸 목록을 별도로 등록할 필요는 없다.

## 참조 점검

복제한 룸에서 특히 아래 연결을 확인한다.

| 컴포넌트 | 연결 대상 |
| --- | --- |
| `RoomArea` | 같은 룸의 카메라, 카메라 경계, 배경 |
| `CinemachineConfiner2D` | 같은 룸의 `CameraRoomBounds` |
| `RoomEntry` | 해당 Entry의 `KingStart`, `RookStart` |
| `RoomExit` | 이동할 룸의 `RoomEntry` |
| `BackgroundParallax` | 공통 `Main Camera`, 같은 룸의 `ParallaxOrigin` |

`RoomArea` 참조가 빠졌거나 카메라의 Confiner 경계가 맞지 않으면 플레이 시작 시 Console 오류가 나고 룸 전환이 비활성화된다.
