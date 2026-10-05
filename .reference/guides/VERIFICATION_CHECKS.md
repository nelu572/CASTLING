# 재사용 검증 방법

## 목적과 적용 범위

타일·충돌·카메라·진행 경로·재시작을 변경할 때 필요한 방법을 골라 사용한다. [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)의 확인 항목을 구체화한 안내이며, 전체 항목을 자동 실행하는 검사 도구는 없다.

## 준비 조건

- Unity MCP로 현재 프로젝트와 씬을 확인한다. 현재 컴포넌트 참조·입력 설정·사용자 튜닝을 기준으로 검사하며 과거 좌표나 수치를 복사하지 않는다.
- 씬 상태, 입력 기기와 PlayerInput 설정, 카메라, 물리, `Time.timeScale` 등 검사에서 바꿀 상태를 먼저 기록한다. 재시작 검사에서는 `VoidRespawnZone`의 활성 상태, `PlayerInput.inputIsActive`, `PlayerMovement.enabled`, 연결된 Fade의 `alpha`도 포함한다. 검사 전부터 비활성인 입력·이동이나 사용자 설정을 통과 조건에 맞춰 바꾸지 않는다.
- 필요한 임시 자료는 [자료 추가 기준](../../.codex/harness/TEMPLATES.md)에 따라 `.work/<날짜>-<작업명>/`에 둔다. 검사 코드 보존이 필요하면 실행 경로·의존성·부작용·확인 범위를 안내와 함께 추가한다.

## 검사 방법

### 타일 접합

- 사각 자동 타일의 8방향 이웃 유무 256조합에서 외곽선과 안쪽 꼭짓점의 기대 상태를 실제 Sprite·셀 변환과 비교한다. 기대 결과는 현재 접합 규칙에서 구하며 검사 대상 함수의 반환값을 그대로 정답으로 쓰지 않는다.
- 같은 Tilemap과 다른 Tilemap, 검정과 아이보리의 접합을 포함한다. 다른 Tilemap은 월드 배치가 달라진 경우도 확인한다.
- 곡선은 네 방향 회전·뒤집기 뒤 형태와 접합 방향을 확인한다. 브러시·삭제·Undo/Redo 뒤의 표시 갱신도 별도로 확인한다.
- `Check Selected Platform Borders` 메뉴는 부분 접합 위치를 경고하는 도구다. 이 전체 검사나 자동 수정의 실행 명령으로 취급하지 않는다.

### 충돌 도형

- Sprite Physics Shape와 실제 TilemapCollider2D·CompositeCollider2D가 같은 월드 지점을 포함하는지 비교한다. Sprite·셀의 기준점, 셀 변환과 Tilemap의 월드 변환을 적용해 좌표계를 맞춘다.
- 경계 근처의 돌출·수치 오차와 도형 내부의 불일치를 구분한다. 허용 오차는 현재 Collider 설정과 검사 해상도에서 정하고 근거를 보고한다.
- 지형 갱신은 [Unity 하네스](../../.codex/harness/UNITY.md)의 절차를 따른다. Play Mode Raycast와 실제 착지를 함께 확인한다.

### 카메라

- Cinemachine이 안정된 뒤 두 몸체 Collider의 월드 경계를 `WorldToViewportPoint`로 변환해 화면 안에 있는지 확인한다. 중심점만 보이는 것으로 몸체 전체의 가시성을 판단하지 않는다.
- 직교 카메라의 반높이는 `orthographicSize`, 반너비는 `orthographicSize × aspect`다. 회전·화면비·viewport 조건을 포함해 경계를 비교한다.
- 여러 룸에서는 `RoomTransitionController.ActiveRoom`과 해당 `RoomArea.CameraBounds`를 사용한다. 최초 `StartingEntry`의 룸을 현재 룸으로 고정하지 않는다.
- 화면 합성·레터박스·UI는 [Unity 하네스](../../.codex/harness/UNITY.md)의 실제 화면 캡처 기준으로 별도 확인한다.

### 진행 경로와 복귀

- 연속 경로는 저장된 시작점부터 실제 PlayerInput 입력으로 검사한다. 사례별 배치 검사는 안전 착지·복귀·단독 우회처럼 확인할 목적과 시작 조건을 따로 보고한다.
- 능력을 일찍 해제한 뒤 안전하게 착지하고 일반 이동·점프로 복귀할 수 있는지 각 캐릭터로 확인한다. CASTLING의 사용 횟수는 정해진 경로의 관찰 결과로 남기며 공통 통과 조건으로 고정하지 않는다.
- 단독 우회 검사는 실제로 확인한 출발 지점과 입력 범위만 결론에 포함한다. 몇 가지 사례를 확인하고 전체 공간에서 우회가 불가능하다고 쓰지 않는다.

### 재시작과 자동 입력

- 암전·타임아웃 대기는 게임 시간 정지를 고려해 unscaled time 또는 Editor 시간으로 측정한다. `RespawnCount`는 검사 시작값에 대한 증가량으로 중복 여부를 확인한다.
- 복귀 위치는 현재 `RespawnEntry.KingPoint`와 `RookPoint`에 비교한다. 입력·이동 활성 상태와 `Time.timeScale`이 검사 전 상태로 돌아왔는지 확인하며 고정값으로 덮어쓰지 않는다.
- 암전 도중 `VoidRespawnZone` 컴포넌트 또는 오브젝트를 비활성화해 재시작을 중단하는 사례도 확인한다. `inputIsActive`·`PlayerMovement.enabled`·`Time.timeScale`이 재시작 전 상태로 돌아오고, 연결된 Fade의 `alpha`가 0, `IsRespawning`이 false인지 확인한다. 정상 재시작 완료와 중단 복원을 구분해 보고한다.
- 자동 입력에서 바꾼 `PlayerInput.actions.devices`와 `neverAutoSwitchControlSchemes`를 복원한다. 검사에서 만든 기기 ID와 콜백을 추적해 그것만 제거하고 실제 키보드나 다른 작업의 가상 기기를 삭제하지 않는다.

## 결과 보고와 한계

실제로 실행한 조건, 기대 결과와 관찰값, 실패·미실행 항목을 작업 보고에 남긴다. 이 문서의 방법을 적었다는 이유로 검증 완료를 기록하지 않는다. 자동 입력과 화면 검사는 사람이 직접 플레이하며 판단하는 발견성·난이도 검토를 대신하지 않는다.

## 상태 복원과 자료 정리

실제로 검사할 때 바꾼 `VoidRespawnZone` 활성 상태, 입력·이동, Fade, 카메라·물리·`Time.timeScale` 등은 검사 전 기록한 값으로 복원하고, 검사에서 만든 임시 오브젝트·입력 기기·콜백만 제거한다. 검사 전부터 있던 사용자 상태는 유지한다. Play Mode를 Stop한 뒤에도 검사 기기가 다시 남는지 확인한다. 확인이 끝난 임시 파일과 빈 작업 폴더를 [.work 안내](../../.work/README.md)에 따라 정리하고, 다음 작업에도 필요한 방법만 이 안내와 관련 지침에 반영한다.
