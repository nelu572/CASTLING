# 실제 입력 플레이테스트 자동화

## 목적과 적용 범위

`Assets/Editor/PlaytestAutomation.cs`는 킹·룩의 걷기·점프를 현재 지형에서 관측하고, 데이터로 작성한 기본 입력 시퀀스를 반복한다. 생성한 Keyboard의 상태를 InputSystem에 큐잉하고 기존 PlayerInput→PlayerInputHandler→게임 물리를 그대로 통과시킨다. 이동 함수 직접 호출, Transform·Rigidbody 위치/속도 쓰기, 씬·프리팹 저장은 하지 않는다.

이 도구의 완료는 **지정한 입력과 관측 단계의 완료**다. 선언한 레벨 조건은 별도로 자동 판정하며, 선언하지 않은 완주·모든 경로의 안전이나 최대 이동 능력·발견성·난이도·재미를 인증하지 않는다. 걸어갈 공간·착지면·천장·다른 몸체의 접촉은 호출자가 현재 씬에서 확인한다. 기본 방향/시간이 지형과 맞지 않으면 실패 결과를 보존하고 계획을 조정한다.

## 준비 조건과 대상

- Unity Editor, Input System, 프로젝트의 `RoomTransitionController`, 연결된 킹/룩의 `PlayerMovement`, `PlayerInput`, Rigidbody2D, Collider2D가 필요하다. 추가 패키지·씬 배선·빌드 포함은 없다.
- `Describe()`로 현재 활성 씬의 컨트롤러·연결된 몸체·입력 바인딩·Settings·콜라이더를 확인한다. 비활성 보관 루트와 다른 로드 씬은 탐색 대상에서 제외한다.
- Edit Mode의 Describe에서 grounded=false·빈 contacts는 아직 Play 물리를 관측하지 않은 자리표시자다. 공중 상태로 판정한 값이 아니다.
- 활성 컨트롤러가 여러 개면 컨트롤러 또는 그 자식을 선택하거나 `Describe(controllerInstanceId)` / `Start(json, controllerInstanceId)`로 명시한다. 다른 씬의 ID는 거절한다. Selection을 자동 변경하지 않는다.
- `Start`는 안정된 **Play Mode**, 일시정지 해제, 양수 timeScale, FixedUpdate 2D 물리, 활성화된 양쪽 입력·이동·Dynamic 몸체에서만 허용한다. 입력·이동이 꺼진 사용자 상태를 자동 활성화하지 않는다.
- 현재 action map의 Move 1DAxis negative/positive와 Jump에 각각 유효한 Keyboard 바인딩 하나가 있어야 한다. effectivePath를 읽어 바인딩 오버라이드를 따른다. 다른 기기, 중복 키, 지원하지 않는 바인딩은 거절한다. mask/processors/interactions가 입력을 막으면 실제 Action·물리 관측이 실패할 수 있다.

## Codex / Unity MCP 호출

기존 stdio MCP의 프로젝트 인스턴스를 먼저 확인·고정한다. `execute_code`는 `action="execute"`와 아래 C# 본문을 사용한다. 호출은 즉시 반환하므로 Status를 별도로 조회한다. 새 Start로 대기하지 않는다.

```csharp
return PlaytestAutomation.Describe();
```

Play Mode 진입은 MCP의 Editor play 도구를 사용한다. 다음 호출은 방향 +1의 기본 걷기·제동·정지 점프를 양쪽에 차례로 실행한다. -1 방향도 가능하며, 현재 지형에 맞는지는 별도로 확인한다.

```csharp
return PlaytestAutomation.StartBasic(1);
```

```csharp
return PlaytestAutomation.Status();
```

```csharp
return PlaytestAutomation.Result();
```

```csharp
return PlaytestAutomation.Cancel("Codex 검사를 명시적으로 중단");
```

`Result()`는 실행 중 현재 메모리 결과, 종료 후 Editor 세션의 최근 결과 JSON을 반환한다. 실행 중 Start 재호출은 기존 실행을 유지하고 거절 이벤트를 남긴다. `Cancel`은 반복해도 새 상태를 만들지 않는다. 호출이 전송 재시도로 중복돼도 진행 중 실행을 교체하지 않는다.

## 데이터 계획

`BasicPlanJson(1)`으로 기본 계획을 얻어 시간·방향·점프를 바꿀 수 있다. 저장한 계획 파일을 읽는 예:

```csharp
return PlaytestAutomation.Start(System.IO.File.ReadAllText(
    System.IO.Path.Combine(UnityEngine.Application.dataPath,
        "../.work/20261015-playtest-automation/my-plan.json")));
```

```json
{
  "name": "short-local-walk-and-jump",
  "timeoutSeconds": 20,
  "outputDirectory": ".work/20261015-playtest-automation",
  "steps": [
    {"name":"settle","durationSeconds":0.5,"check":"observe","subject":"king"},
    {"name":"walk","durationSeconds":0.2,"kingMove":1,"check":"king-walk"},
    {"name":"brake","durationSeconds":0.3,"check":"observe","subject":"king"},
    {"name":"jump","durationSeconds":2,"kingJump":true,"jumpHoldSeconds":0.08,"check":"king-jump","finishOnLanding":true}
  ]
}
```

- 각 step의 `kingMove` / `rookMove`는 -1, 0, +1이다. `kingJump` / `rookJump`는 시작부터 `jumpHoldSeconds` 동안 실제 키를 누른다. 생략한 이동·점프는 해제한다. 연속 같은 키는 계속 누르며 두 단계 사이에 억지로 재입력하지 않는다.
- `durationSeconds`는 고정 물리 시간의 단계 상한이다. 최소 한 물리 샘플과 입력 업데이트 단위로 양자화되며 정확한 실시간 재생을 보장하지 않는다. `finishOnLanding`은 지정한 jump check에서 실제 Jumped/Landed와 안정 접지 2 tick을 관측하면 일찍 종료한다.
- `check`는 observe / king-walk / rook-walk / king-jump / rook-jump다. check가 대상을 명시하면 그 역할을 측정한다. observe는 `subject`(기본 king)를 측정하며 모든 raw sample에는 항상 양쪽 몸체가 들어간다.
- Walk는 가상기기에서 전달된 실제 Move Action 값, 기대 방향의 0.02 초과 변위, 0.1 초과 수평 속도와 전 샘플 접지를 요구한다. 연속 held 입력은 신규 performed callback 없이도 유효하다.
- Jump는 접지 시작, 실제 Jump performed, Jumped/Landed, 0.02 초과 상승과 종료 시 안정 접지를 요구한다. 위 숫자는 정지/수치오차를 구분하는 **관측 문턱**이며 게임 물리·난이도 규칙이 아니다.
- 단계 1..256, 단계 시간 (0,60]초, 전체 Editor 시간 timeout (0,300]초, 최대 20,000 물리 샘플로 제한한다. 기본 timeout은 30초다. 지원하는 능력 입력은 아래 항목을 따르며 임의의 장치 조작은 지원하지 않는다.

## 능력 입력과 레벨 조건

Step의 `kingCastling=true`는 `abilityHoldSeconds`(기본 0.08초) 동안 CASTLING 키를 누른다. `rookSlide=true`는 그 단계 동안 Slide 키를 유지한다. 다음 단계에서 false면 해제한다. 현재 effective Keyboard binding과 실제 활성 `CastlingAbility` / `RookSlideAbility`·action을 요구하며, 컴포넌트가 없으면 실행을 거절한다. 능력을 요구하지 않는 기존 BasicPlan은 해당 컴포넌트가 없는 씬에서도 사용할 수 있다. 입력 없이 능력 메서드를 직접 호출하지 않는다.

실제 Castling performed와 Slide performed/canceled, 각 BodySample의 slideMoving·slideDirection·gravityScale·movementEnabled·simulated를 기록한다. 정상 Slide가 movement를 끄거나 Respawn이 input/movement/timeScale을 잠시 끄는 상태는 구조 참조 오류와 구분한다. 그 상태를 도구가 강제 활성화하지 않는다. 취소 시 소유 키 해제는 기존 PlayerInput 경로로 전달하며, `finalPlayers`에 정리 뒤 실제 몸체 상태를 남긴다. 자원 정리와 게임 월드/능력 상태 복원은 같은 보장이 아니다.

Plan의 `rules`는 실행 조건을 읽는 선언형 검사다. 새 게임 규칙·장치·씬 배선을 만들지 않는다. 최대 128개이며 `id`는 중복 없이 쓴다. Step 이름도 중복 없이 지정한다.

```json
"rules": [
  {"id":"castling-input","kind":"success","timing":"ever","target":"king","metric":"actionPerformed","action":"Castling","op":"ge","value":1},
  {"id":"slide-seen","kind":"trigger","target":"rook","metric":"slideMoving","value":1},
  {"id":"released","kind":"recovery","target":"rook","metric":"slideMoving","value":0,"triggerId":"slide-seen","withinSeconds":2},
  {"id":"input-stable","kind":"success","timing":"always","target":"king","metric":"inputActive","value":1,"holdSeconds":0.1}
]
```

| 필드 | 의미 |
| --- | --- |
| kind=success | 필요한 조건. timing=end(기본)는 마지막 관측, ever는 한 번 이상, always는 모든 관측에서 raw 조건 참을 요구한다. |
| kind=failure | 조건이 참이면 즉시 실패한다. 시간 경과만으로 실패 조건을 만족했다고 판정하지 않는다. |
| kind=trigger | 첫 일치 시각을 기록하는 복구 전제. 그 자체는 합격 조건이 아니며, 관측 오류는 전체 error에 반영한다. |
| kind=recovery | triggerId의 첫 관측 뒤 withinSeconds 안에 조건을 충족해야 한다. 기본 requireFalseBeforeRecovery=true는 trigger 이후 조건의 false→true를 요구한다. 이후의 모든 복구 주기까지 검사하는 규칙은 아니다. |
| target | king / rook / controller / activeRoom / startingEntry 또는 path:전체계층경로. 명시 path는 현재 활성 씬에서 이 controller의 Rooms Root 안의 유일한 오브젝트여야 한다. |
| op/value/tolerance | eq / ne / gt / ge / lt / le. bool은 0/1, eq/ne 기본 허용차는 0.001이다. |
| step | 선택한 단계에서만 평가한다. end는 그 단계의 마지막 관측을 뜻한다. 해당 단계가 관측되지 않으면 not_evaluated다. |
| relativeToStart | 실행 시작의 raw 값 대비 차이를 비교한다. 예: respawnCount에 true, op=ge/value=1이면 기존 누적 횟수로 통과하지 않는다. 새 실행마다 기준점을 다시 잡는다. |
| holdSeconds | 조건이 연속 참인 Editor 벽시계 유지 시간. 기본 0. always는 유지 대기 중 raw 조건이 참이면 실패하지 않지만 실제 false는 즉시 위반이다. 물리 시간이 아니다. |

읽을 수 있는 metric은 `active`, `x`, `y`, `deltaX`, `deltaY`, `velocityX`, `velocityY`, `grounded`, `movementEnabled`, `inputActive`, `simulated`, `slideMoving`, `slideDirection`, `switchPressed`, `gateOpen`, `goalCompleted`, `respawning`, `respawnCount`, `actionPerformed`, `startDistance`다. `timeScale`, `playerDistance`, `exchangeXError`는 연결된 두 몸체/전역 상태를 읽는다. deltaX/Y와 exchangeXError는 실행 시작 몸체 위치 기준이다. 필요한 컴포넌트/대상이 없거나 metric이 틀리면 error이며 false 또는 성공으로 취급하지 않는다. arbitrary C#·임의 프로퍼티 실행은 지원하지 않는다.

조건은 Physics2D 뒤와 Editor update에서 평가해 timeScale=0인 respawn도 관측한다. `ruleEvidence`는 값/일치 상태 변화·시각·단계를 기록한다. rule 상태는 passed / failed / observed(trigger) / error / not_evaluated, 전체 `levelVerdict`는 passed / failed / error / not_evaluated / not_configured다. 조건이 끝까지 false면 failed, 복구 trigger가 안 생기면 not_evaluated, 실행이 중단되면 전체 passed가 되지 않는다. 일부 관측 사실은 중단 뒤에도 개별 rule에 남는다. 판정은 선언한 조건·관측 시간 범위에 한정한다.

## 독립 실행·배치·비교

`Assets/Editor/PlaytestLevelValidation.cs`는 기존 runner를 사용해 **Play Mode 안에서만** 저장된 씬을 Single 로드하고 실행한다. dirty인 편집 씬을 저장·폐기·다시 열지 않는다. 먼저 MCP로 Play에 들어간 뒤 호출한다.

```csharp
return PlaytestLevelValidation.StartFresh(planJson, "Assets/Scenes/Chapter_01.unity");
```

이는 fresh **scene instance**이며 static, ScriptableObject, DontDestroyOnLoad까지 초기화하는 완전 월드 리셋이 아니다. 로드 뒤 최소 한 runtime frame과 Editor 0.1초를 기다리며, 접지까지 기다린다는 보장은 없다. Plan에 settle 단계와 필요한 시작 조건을 넣고 실제 initial body 값을 확인한다. 더 강한 독립 비교는 MCP Stop → 새 Play → StartFresh 순서로 수행한다. 도구는 EnterPlayModeOptions를 바꾸지 않고 실행 결과에 기록한다.

배치 JSON은 `name`, `outputDirectory`, `cases` 배열(1..16개)을 받는다. 각 case는 `name`, `scenePath`, 위 Plan 구조의 `plan`을 가진다. 매 case 전에 Play 전용 Single 로드를 반복한다. 조건·입력 시간이 다른 계획을 case별로 작성할 수 있으며 gameplay 튜닝을 임의 변경하지 않는다.

```csharp
return PlaytestLevelValidation.StartBatch(batchJson);
```

```csharp
return PlaytestLevelValidation.BatchStatus();
```

```csharp
return PlaytestLevelValidation.BatchResultJson();
```

```csharp
return PlaytestLevelValidation.CancelBatch("전체 배치 중단");
```

```csharp
return PlaytestLevelValidation.Compare(leftResultPath, rightResultPath);
```

배치가 실행권을 가진 동안 일반 Start와 다른 배치는 거절한다. `PlaytestAutomation.Cancel`은 현재 child만 취소하고 다음 case를 계속하므로, 전체 중단은 CancelBatch를 쓴다. Stop/리로드도 소유 child의 실제 종료 결과와 후속 not_run을 남긴다. 배치 state=completed는 모든 case를 시도했다는 뜻이고 **verdict와 각 child state/levelVerdict**를 따로 읽어야 한다. cancelled/interrupted/미평가 child가 있으면 배치 전체를 합격 처리하지 않는다. 배치는 끝에 월드를 되감거나 자동 Stop하지 않으며 입력 자원과 배치의 임시 background/callback만 정리한다.

Compare는 씬·계획·isolation mode·기록된 전역 물리·movement settings·초기 몸체·EnterPlay 옵션의 동일 여부, 단계별 변위/높이/속도/샘플 수 차이를 JSON으로 반환한다. 단계는 index/name/role이 같을 때만 수치 짝을 만들고 누락/추가 단계는 unmatched로 남긴다. 초기 몸체 값은 접촉 배열까지 엄격히 비교하므로 작은 관측 차이도 false일 수 있다. 결과가 같다고 결정론을 보장하거나, 다른 조건의 결과를 같은 조건으로 합치지 않는다.

## 결과·실패·측정 한계

실행마다 `.work/<날짜>-playtest-automation/<고유 ID>.json`을 만든다. 출력 폴더를 지정하면 프로젝트 `.work`의 하위 경로만 허용한다. 결과에는 계획, 씬·컨트롤러·몸체 경로, 시작 Settings 값, 원래 asset/map 기기 필터·pairing·neverAutoSwitch, Collider·중력·고정 간격·입력모드·timeScale, Action의 실제 control/device와 이벤트, 고정 물리 뒤의 양쪽 위치·속도·접지·접촉점/법선, 단계별 변위·속도·높이와 정리 결과가 포함된다.

- `completed`는 모든 요청 단계를 관측했다는 뜻이다. `failed`는 관측 실패·대상/조건 변화·예외, `error`는 레벨 조건의 대상/관측 오류, `not_evaluated`는 완료되지 않은 레벨 판정이다. `timed_out`은 Editor 벽시계 초과, `cancelled`는 명시 취소·Play Stop, `interrupted`는 리로드·Editor 종료, `rejected`는 실행 조건 거절이다.
- 단계 verdict는 observed / failed / partial / not_run이다. 중단 단계와 후속 미실행 단계를 누락하지 않는다. `reason`과 raw sample을 함께 읽는다. 일부 단계 실패 뒤의 관측값을 일반 능력으로 승격하지 않는다.
- PlayerLoop의 Physics2DFixedUpdate 직후 샘플이다. 최고점은 고정 간격으로 관측한 하한이며, `observedAirSeconds`는 비접지 샘플 수 × 고정 간격이다. 입력은 Dynamic/Fixed InputSystem 업데이트에 맞춰 처리된다. 단계 전환에서 다음 입력이 처리되기 전 물리 tick은 `step=-1`로 남긴다. Contact buffer 16개가 가득 찼을 가능성도 기록한다.
- fixedDeltaTime, 중력, 2D simulationMode, InputSystem update mode가 실행 중 바뀌면 측정 조건 변화로 중단한다. 외부에서 바뀐 튜닝을 도구가 원복하지 않는다. timeScale은 건드리지 않으며 일시정지·timeScale=0도 Editor timeout으로 끝낼 수 있다.
- 같은 입력을 반복해도 지형/몸체 접촉·실제 업데이트 시점 때문에 값이 다를 수 있다. 안정된 비교에는 같은 시작 상태를 사용한다. 필요하면 Play 재시작 또는 Play 전용 씬 로드로 독립 실행을 만들고 기록한다. 위치를 별도 배치했다면 연속 경로 완주와 구분한다.

## 상태 복원·정리

기기·필터를 바꾸기 전에 원래 값을 복사한다. asset과 현재 map의 기기 필터, neverAutoSwitch를 임시로 제한하고 InputSettings는 복제본으로 교체한다. 복제본에서 백그라운드/Editor 입력 포커스를 허용하며 원래 Settings 에셋을 저장하지 않는다. 필요할 때만 runInBackground를 바꾼다. OS 포커스·timeScale·Physics 설정은 쓰지 않는다.

Input System 1.18의 map.devices 공개 getter는 asset 필터까지 상속해 반환한다. 원래 map override의 null 의미를 보존하기 위해 내부 DeviceArray.Get을 읽기 전용 reflection으로 확인한다. 구조가 달라졌으면 실행을 거절한다. 내부 필드를 직접 쓰지는 않는다.

정상 종료·실패·취소·타임아웃·Play Stop·리로드·Editor 종료에서 생산 콜백/물리 observer를 먼저 해제하고, 소유 키를 해제한 뒤 필터·neverAutoSwitch·InputSettings·runInBackground·이전 current Keyboard를 복원한다. 자신이 만든 Keyboard·Settings 복제본만 제거한다. 외부에서 InputSettings 객체를 교체했다면 그 객체를 보존하고 복원 충돌을 기록한다. 다른 작업 기기와 실제 기기를 정리하지 않는다.

모든 실행 경로를 Editor SessionState에 별도로 추적해 Stop 뒤 기기 ID의 재출현 여부를 각 JSON에 기록한다. 마지막 결과가 invalid Start로 바뀌어도 실제 실행의 Stop 감사는 유지한다. 검증할 때에는 Stop 후 `CASTLINGPlaytest-*` 기기·observer·콜백 잔류 여부와 원래 편집 씬/dirty 상태를 별도로 확인한다. 자동 도구도 Editor/프로세스 강제 종료 후 복원을 보장하지 않는다.

리로드 결과의 cleanup은 **beforeAssemblyReload 시점** 복원 결과다. 이후 Unity PlayerInput이 OnEnable에서 사용자를 다시 만들고 기기를 재배정하므로 리로드 뒤 pairing·필터까지 동일하다고 보장하지 않는다. 도구 없이 같은 리로드를 해도 이 재구성이 관측됐다. 리로드 뒤 새 측정은 Describe와 새 Play 시작 조건으로 다시 확인한다. 정리는 입력 자원·콜백·관찰자·임시 설정을 다루며 런타임 월드의 위치·장치 진행을 되감지 않는다. 반복 비교는 같은 저장 상태에서 fresh Play 또는 Play 전용 씬 로드로 시작하고, Stop 뒤 생긴 dirty를 자동 저장하거나 지우지 않는다.

현재 Chapter_01은 Play 중 재컴파일/스크립트 리로드 뒤 발먼지 풀의 NullReferenceException이 발생하는 [기존 이슈 #11](https://github.com/nelu572/CASTLING/issues/11)이 있다. 코드 편집 전 Stop하고 컴파일 뒤 새 Play를 시작하는 흐름을 권장한다. 자동화 자원 정리 통과가 게임 전체의 리로드 안전을 보장하지 않는다.

실행 결과·계획·스크립트 조각은 작업 폴더 README에 용도·검증 상태·보존 종료 조건을 적는다. 검증/검토가 끝나면 중복·불필요 결과를 정리하고 재사용 방법만 이 안내에 남긴다. 코드와 `.meta`는 Unity가 import한 쌍으로 보존한다. 결과 파일을 게임 에셋이나 영구 물리 기준표로 옮기지 않는다.

## 연결

- [Unity 변경 검증](../../.codex/skills/UNITY_VERIFY.md)
- [재사용 검증 방법](VERIFICATION_CHECKS.md)
- [Unity 하네스](../../.codex/harness/UNITY.md)
- [임시 자료 기준](../../.work/README.md)
- [CASTLING 기획 원문](https://app.notion.com/p/3dcb3e6e776c81f08abde9fa6cefdb80)
