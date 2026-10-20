# 레벨 설계의 현재 프로젝트 근거

## 용도와 관리

[레벨 디자인 절차](../../.codex/skills/LEVEL_DESIGN.md)에서 현재 프로젝트의 제약을 조사할 때 사용하는 탐색 안내다. 현재 수치를 고정하는 명세나 개별 레벨 설계안이 아니다. 작품 관찰은 [레퍼런스 분석 자료](LEVEL_DESIGN_REFERENCES.md)에서 별도로 관리한다. 2026-10-15에 저장소 코드·안내·Notion 원문과 MCP 도구 노출을 확인했다. 시스템·에셋 경로·기획이 바뀌면 관련 항목을 갱신하고, 수치·배치·활성 상태는 작업 때마다 Editor에서 확인한다. 개별 설계·테스트는 [.work](../../.work/README.md), 승인된 장기 결정은 [자료 추가 기준](../../.codex/harness/TEMPLATES.md)을 따른다.

## 기준 기획

- [Notion 기획서](https://app.notion.com/p/3dcb3e6e776c81f08abde9fa6cefdb80): 킹·룩의 2인 협동, 함께 이동/분리/재합류, 준비한 위치와 진행을 CASTLING으로 넘기는 경험. 양쪽 모두 직접 움직이고 판단해야 한다. 4방향 룩 이동과 능력 제약의 일부는 프로토타입 후보·미확정이므로 구현된 기능으로 가정하지 않는다.
- [Notion 아트 레퍼런스](https://app.notion.com/p/3dcb3e6e776c81f2bb63c68f908c5ec5): 평면 2D·미니멀·흑백 플랫폼·체스 실루엣, 낮은 대비의 후경. 이미지 속 이동 장치·버튼·노란 상자 등은 기능 확정안이 아니다. 레벨 레퍼런스를 바꿔도 아트 방향을 자동 변경하지 않는다.
- [Chapter_01 기록](../records/CHAPTER_01.md): 해당 챕터에서 승인된 조작·퍼즐·카메라 방향과 뒤에 갱신된 결정. 과거 첫 통로 구성을 후속 튜토리얼 변경보다 우선하지 않는다. 과거 물리·좌표·검증 결과를 현재 상태에 적용하지 않는다.
- [레벨 보관소](../levels/README.md): Chapter_01과 별개인 독립 후보 목록과 레벨별 핵심 아이디어·해법·대안·복구·확인 범위. 새 보존 아이디어를 추가할 때도 여기서 시작한다. 제작 승인·플레이 평가는 [별도 결정 기록](../records/LEVEL_COLLECTION.md)을 연결한다. 선행 학습에 대한 평가와 챕터 귀속·등장 순서·최종 채택은 별도 결정한다.
- [이동식 발판 실험 종료 기록](../records/MOVABLE_STEP_EXPERIMENT.md): 2026-10-16 종료한 독립 프로토타입의 사용자 평가·보존 결정·검증 한계. 후속 실험의 필수 기믹이나 공간 공식으로 적용하지 않는다.

현재 게임의 기준은 2인 협동 물리 플랫포머/퍼즐이다. 이것이 모든 레벨을 평지 스위치 퍼즐로 만들라는 뜻은 아니다. 이번 요청의 이동 숙련·탐험·학습 등 목표와 기획의 양쪽 기여를 함께 검토한다. Dev_PressureRelay와 D1·D2·D3 및 후속안은 테스트 사례이며 상시 제작 목표·기능 목록·필수 공간형이 아니다.

## 프로젝트 구조와 확인 경로

환경은 [ProjectVersion](../../ProjectSettings/ProjectVersion.txt)의 Unity 6000.0.68f1, [manifest](../../Packages/manifest.json)의 URP·Input System·Cinemachine·Test Framework·Coplay 구성을 확인했다. 패키지 버전은 manifest와 lock을 다시 확인한다. 주요 구조는 `Assets/Scripts`의 MonoBehaviour·설정 ScriptableObject와 `Assets/Editor`의 편집 도구다. 저장소 조사에서 별도 자체 asmdef·전용 레벨 테스트 러너는 발견하지 못했다. Test Framework 설치만으로 테스트가 구현됐다고 보지 않는다.

| 확인할 대상 | 현재 조사에서 확인한 기능과 다음 탐색 위치 |
| --- | --- |
| 씬/시작 | [SceneNames](../../Assets/Scripts/Values/SceneNames.cs)와 `Assets/Scenes/`에서 대상 경로를 찾고 [Build Settings](../../ProjectSettings/EditorBuildSettings.asset), 실제 열린 씬·런타임 Entry를 대조한다. 개발 씬의 존재와 빌드 등록·챕터 완료는 구분한다. |
| 플레이어 | [PlayerMovement](../../Assets/Scripts/Characters/PlayerMovement.cs)는 Rigidbody2D·PolygonCollider2D와 연결 Settings를 사용한다. Environment·Player를 접지 대상으로 삼고 움직이는 받침의 수평 속도를 반영한다. 몸체 형상과 실제 밟기/밀기 가능 범위는 Editor/플레이에서 확인한다. |
| 이동 수치 | [PlayerMovementSettings](../../Assets/Scripts/Characters/PlayerMovementSettings.cs)에 가감속·점프·중력·하강 배율·코요테/버퍼가 있다. `Assets/SO/PlayerMovementSettings.asset`의 존재만으로 현재 플레이어 연결·유효 수치를 확정하지 않는다. |
| 입력 | [PlayerInputHandler](../../Assets/Scripts/Characters/PlayerInputHandler.cs), [CastlingInputActions](../../Assets/Input/CastlingInputActions.inputactions)에 킹 A/D·W·F, 룩 좌/우·위·오른쪽 Shift 바인딩이 있다. 다른 InputActions 파일도 있으므로 현재 PlayerInput 참조와 action map을 대조한다. |
| CASTLING | [CastlingAbility](../../Assets/Scripts/Characters/CastlingAbility.cs)는 참조된 두 Rigidbody2D의 위치를 교환하며 각자의 속도를 바꾸지 않는다. 코드에 거리·시야·횟수 제한이 없으므로 그런 제한으로 퍼즐의 우회를 막았다고 가정하지 않는다. 실제 부착·참조와 교환 후 충돌 여유는 별도 확인한다. |
| 룩 이동 | 일반 점프와 [RookSlideAbility](../../Assets/Scripts/Characters/RookSlideAbility.cs)의 좌우 직선 이동은 별도 동작이다. 직선 이동은 공중에서도 발동하며 방향 고정·중력0이고, Shift 해제/앞쪽 Environment 충돌로 정지한다. 이동 중 점프로 전환하거나 착지까지 스킬 속도를 고정하는 기능은 없다. 실제 속도·유지/재발동·충돌·다른 능력과의 조합을 확인한다. Notion 후보의 상/하 이동은 이 코드에 없다. |
| 스위치/문 | [PressurePlateSwitch](../../Assets/Scripts/Gameplay/Puzzles/PressurePlateSwitch.cs), [PressureGate](../../Assets/Scripts/Gameplay/Puzzles/PressureGate.cs): 밟는 동안 유지, 연결 스위치 중 하나라도 눌리면 열림, 열린 문 안의 몸체가 빠져나가야 닫힘. `Assets/Prefabs/Gameplay/PressureGatePair.prefab`이 있지만 대상 씬의 연결 상태는 확인해야 한다. |
| 이동식 발판 | 2026-10-16 추가한 [PushablePlatform](../../Assets/Scripts/Gameplay/Puzzles/PushablePlatform.cs)·`Assets/Prefabs/Gameplay/PushablePlatform.prefab`. 물리 접촉으로 밀고 탑승하며 압력판과 문 보호 대상이다. [사용 안내](PUSHABLE_PLATFORM.md)에서 복구·문 받치기 대안·기존 낙사 복귀의 물체 미복원 한계를 확인한다. |
| 실패/복귀 | [VoidRespawnZone](../../Assets/Scripts/Gameplay/VoidRespawnZone.cs): 참조된 한 명이 닿으면 양쪽을 지정 RoomEntry로 복귀시키며 암전 중 입력·물리 진행을 멈춘다. `Assets/Prefabs/Gameplay/VoidFallZone.prefab` 존재와 실제 배치를 구분한다. |
| 룸/완료 | [RoomTransitionController](../../Assets/Scripts/Gameplay/Rooms/RoomTransitionController.cs), [RoomEntry](../../Assets/Scripts/Gameplay/Rooms/RoomEntry.cs), [RoomExit](../../Assets/Scripts/Gameplay/Rooms/RoomExit.cs), [ChapterGoal](../../Assets/Scripts/Gameplay/ChapterGoal.cs). 출구와 Goal은 양쪽 중심이 영역 안에 있는지 본다. Goal 코드가 있어도 현재 챕터 완료 판정이 배치됐다는 뜻은 아니다. |
| 카메라 | [RoomArea](../../Assets/Scripts/Gameplay/Rooms/RoomArea.cs)의 BoxCollider2D 경계, RoomTransitionController의 TargetGroup·GroupFraming·Confiner 연동, [CameraAspectRatio](../../Assets/Scripts/Gameplay/CameraAspectRatio.cs)의 viewport 처리. [룸 안내](ROOM_AUTHORING.md#카메라가-보여주는-범위)의 사용자 결정인 기본 최소 직교 크기 8과 좁은 구역 예외, 공유 화면·거리별 줌 확인법을 사용한다. 현재 사용자 튜닝을 일괄 덮어쓰지 않는다. |
| 지형 | `Assets/TilePalette/Main/`의 4종 지형 팔레트와 `Tiles/`의 6개 라이브러리. [PlatformTileLibrary](../../Assets/Scripts/Environment/PlatformTileLibrary.cs)·[PlatformFillLibrary](../../Assets/Scripts/Environment/PlatformFillLibrary.cs)는 Tile 하위 에셋을 묶는다. 개별 Tile은 경로만으로 식별되지 않을 수 있으므로 이름·GUID/local ID도 확인한다. |
| 좌표/충돌 | 팔레트의 1×1 셀이나 Sprite의 128 PPU가 대상 씬 Grid의 1셀=1월드 단위를 증명하지 않는다. 큰 Sprite·곡선·셀 변환과 Sprite Physics Shape가 있어 타일이 찬 칸을 모두 같은 사각 충돌로 가정할 수 없다. 상세 사용법은 [플랫폼 안내](PLATFORM_SHEETS.md)를 읽는다. |

## 기존 편집·검증 수단

| 수단 | 사용할 때와 한계 |
| --- | --- |
| [RoomAuthoring](../../Assets/Editor/RoomAuthoring.cs) | 선택한 현재 씬 룸 복제·참조 연결. [룸 안내](ROOM_AUTHORING.md)를 재사용하며 레벨 풀이를 설계하는 도구로 취급하지 않는다. |
| [PlayerStartPositions](../../Assets/Editor/PlayerStartPositions.cs) | Entry 시작 좌표·윤곽 편집과 미리보기. 현재 캐릭터 이동과 StartingEntry 변경은 구분한다. |
| [SceneFramingPreview](../../Assets/Editor/SceneFramingPreview.cs) | 카메라·패럴랙스 편집 미리보기. 실제 입력 완주·Game View 검증을 대신하지 않는다. |
| [PlatformOutlineAuthoring](../../Assets/Editor/PlatformOutlineAuthoring.cs), [PlatformFillAuthoring](../../Assets/Editor/PlatformFillAuthoring.cs) | 기존 선 없는 타일·외곽선 갱신. `Chapter_01.platformoutlines` 등 관련 원본도 사용자 작업으로 보존한다. 단순 분석 중 생성/재계산 메뉴를 실행하지 않는다. |
| [PlatformBorderRefresh](../../Assets/Editor/PlatformBorderRefresh.cs) | 기존 자동 타일의 부분 접합 검사. 도달성·퍼즐 검사나 자동 지형 수정 기능이 아니다. |
| [Unity 검증](../../.codex/skills/UNITY_VERIFY.md), [검사 안내](VERIFICATION_CHECKS.md) | 타일·충돌·카메라·입력 경로·재시작의 재사용 방법. 전체 항목을 자동 실행하는 도구는 없다. |

배경 타일은 [배경 구조물 조립](BG_STRUCTURE_MODULAR.md)의 기존 팔레트·충돌 제외 원칙을 따른다. 화면의 장식과 플레이 지형을 같은 통과 가능성 자료로 취급하지 않는다.

걷기·점프·밟기/밀기·CASTLING·룩 수평 이동·압력판/문·복귀 중 필요한 재료만 선택한다. **룩 가속 점프는 2026-10-20 삭제된 기능이며, 레벨의 해법·도달 거리·필수 동작으로 가정하거나 복원하지 않는다.** `.work`의 과거 시안·검사 기록·복구용 백업은 당시 자료이며 현재 능력의 근거로 사용하지 않는다. 현행 기능은 위 코드와 대상 씬에서 확인한다. 레퍼런스의 대시·벽타기나 탐험용 지도·수집/저장 시스템 등도 이 목록에 확인된 기능이 아니다. 필요하면 실제 구현·참조를 추가 조사하고, 없으면 미구현 제안으로 남긴다. 새 목표 장치가 필요할 때도 기존 코드 존재와 해당 씬에서의 완료 작동을 구분한다.

## Unity MCP 활용과 현재 한계

설치된 Coplay의 기존 stdio 구성을 사용한다. 과거 연결·플레이 성공은 다음 작업의 연결 보장이나 모든 MCP 도구의 검증이 아니다. 작업 시작 시 연결 상태·현재 프로젝트 경로·필요한 지원 기능을 다시 조회한다.

| 용도 | 확인한 인터페이스 | 다음 작업에서 확인할 내용 |
| --- | --- | --- |
| 대상/상태 확인 | `mcpforunity://instances`, `set_active_instance`, project/info·editor/state 리소스 | 프로젝트 경로 일치·활성 대상 고정·편집 준비 상태 |
| 씬·참조·에셋 조사 | `manage_scene`의 get_active/get_hierarchy, `find_gameobjects`, GameObject 리소스, `manage_asset` | 현재 씬의 실제 참조, Collider·Grid·Tile 하위 에셋. 리소스 URI는 서버 문서를 조회하고 사용 |
| 적용 | `manage_components`, `manage_gameobject`, `manage_prefabs`, `execute_menu_item`, `execute_code` | 대상과 지원 기능 확인 후 승인된 범위만. 메뉴는 현재 목록을 먼저 확인하고 읽기 작업 중 실행하지 않음 |
| 검증 | `read_console`, `manage_physics`, `manage_camera`, `run_tests` 등 | 연결 후 필요한 검사만 사용. 테스트 코드/실행 경로가 실제 있는지 확인 |

이번 조사에서 전용 Tilemap 배치·플레이어 이동 측정·레벨 도달성/퍼즐 검사 도구는 확인하지 못했다. `execute_code`의 노출은 이 도구들의 구현·검증을 뜻하지 않는다. MCP가 필요한 작업에서 연결이 불가능하면 한계를 알리고 중단하며 다른 방식으로 우회하지 않는다. 문서·코드 근거 분석은 계속할 수 있다. 새 패키지 설치·별도 서버·레벨 자동 생성기는 이 하네스가 추가하지 않는다.

## 레퍼런스 연결

작품별 관찰은 [레퍼런스 분석 자료](LEVEL_DESIGN_REFERENCES.md)를 필요한 범위만 읽는다. 현재 요청의 경험·기능 분석에서 채택/제외를 결정하고 과거 테스트의 선택을 기본값으로 사용하지 않는다.
