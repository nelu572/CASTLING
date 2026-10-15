# 범용 레벨 검증 확장 결과

외부 검토 공유 범위: 자동화 C#·meta, [실행 가이드](../../.reference/guides/PLAYTEST_AUTOMATION.md), 관련 검증 안내와 [최초 자동화 보고서](../20261015-playtest-automation/REPORT.md)를 함께 공유한다. 이번 검토 커밋의 `.work` 자료는 두 REPORT.md뿐이다. 아래 JSON·스냅샷·코멘트 편집 원문 링크는 커밋에 포함하지 않은 로컬 검증 근거이므로 GitHub에서는 열리지 않는다. 당시 사용자 씬 변경도 제외했으므로 이 커밋만으로 동일 씬 조건 재구성이나 원시 증거의 독립 대조까지 가능하다고 주장하지 않는다. 기존 이슈와 검증 코멘트의 GitHub 링크는 별도로 확인할 수 있다.

상태: **Ready with limitations**. 자동화 확장과 4회 독립 검토·수정·실측을 완료했다. Chapter_01의 기존 Play 중 스크립트 리로드 오류 [#11](https://github.com/nelu572/CASTLING/issues/11)은 별도 제약으로 재현·기록했으며 공유 게임플레이 코드를 수정하지 않았다.

## 구현·보장 범위

- 기존 `PlaytestAutomation.cs`를 확장해 실제 CASTLING/Slide 입력, 능력/입력/중력 상태, 종료 후 실제 몸체 상태와 선언형 레벨 판정을 기록한다. 정상 Slide/Respawn의 일시적 movement/input 비활성은 구조 참조 오류와 구분한다.
- `PlaytestLevelValidation.cs`에 읽기 전용 조건, 첫 trigger 이후 복구, Play 전용 Single 씬 재생성 배치와 결과 비교를 추가했다. 일반 Start와 배치는 실행 소유권을 지키며, 중단된 child와 후속 not_run을 누락하지 않는다.
- success/failure/error/not_evaluated를 구분하고, false 조건이나 관측하지 않은 복구가 시간이 지났다는 이유로 통과하지 않는다. hold/상대 기준값·단계 범위를 데이터로 지정한다. 새 게임 규칙이나 임의 C# 조건 실행은 추가하지 않았다.
- 기존 가이드만 갱신했다. 씬·프리팹·게임플레이·물리·입력 에셋/패키지·EnterPlayModeOptions는 수정하지 않았다. 커밋·푸시도 하지 않았다.

최종 코드 SHA256:

- PlaytestAutomation.cs: `DA10A9EDF82A891F12252F34D81FF4FCBA38561AE9E2C8973AFF66EB9474DFF7`
- PlaytestLevelValidation.cs: `771FE492BF80B99B3B7988E6B5F76D6197361FC1D070EA9BC898D1BE9D5D6C48`

Unity 6000.0.68f1 / Input System 1.18.0, 현재 CASTLING 프로젝트와 일치하는 stdio MCP 인스턴스에서 확인했다. 시작 상태는 D3 Edit/dirty=true, background=false/timeScale=1이었다. 구현 중 확인한 게임 규칙의 원문은 총괄이 재조회한 Notion과 기존 Chapter_01 제작 기록이다.

## 능력·조건·복구 실측

| 검사 | 근거와 관측 |
| --- | --- |
| 실제 CASTLING·Slide | [반복 1](20261015T085737547-923e018b.json), [반복 2](20261015T085739225-3d26b8ba.json): 모두 completed/levelVerdict=passed. Castling performed 1회와 수평 교환 오차 0 관측. Slide 15개 물리 tick에서 IsMoving=true, movement=false/중력0, release 뒤 IsMoving=false/movement·입력 복구. 지형/몸체 접촉으로 속도가 달라질 수 있어 일반 최대·정속 능력 인증이 아니다. |
| 실제 낙사→재시작 | [실제 입력 결과](20261015T085359808-3eefacf4.json), [계획](respawn-plan.json): 왼쪽 실제 입력으로 낙사, Respawning trigger 관측, timeScale=0 동안 Editor 평가에서 input/movement=false를 관측한 뒤 약 0.415초 안에 true 복귀. RespawnCount 시작 0 대비 +1, 종료 timeScale1. 도구가 위치·속도·활성 상태를 쓰지 않았다. |
| false 성공 조건 | [결과](20261015T085117221-04988eaf.json): 도달하지 않은 조건은 failed. |
| 금지 조건 | [결과](20261015T085118022-0d94e4b8.json): predicate가 실제 참이어서 즉시 failed. |
| 없는 컴포넌트/조건 오류 | [직접 조건 오류](20261015T085118210-32d5465a.json), [trigger 오류](20261015T085441127-58f73621.json): state/levelVerdict=error. grading에서 제외되는 trigger의 관측 오류도 전체 error에 반영. |
| 복구 trigger 없음 | [결과](20261015T085118461-8b453a10.json): 전체 not_evaluated. |
| 복구 기한 초과 | [최종 수정 뒤 결과](20261015T085441345-7af1b0b7.json): recovery failed, 실제로 관측한 trigger는 observed로 보존. |
| always+hold | [0.4초 idle/hold0.1 통과](20261015T085440349-13d4a09a.json), [raw predicate 위반 실패](20261015T085440924-e33a47f1.json). |
| 능력 없는 씬의 Basic 회귀 | [Dev_Gameplay Basic](20261015T090125331-ac3a84b9.json): 양쪽 걷기·점프 observed/completed, levelVerdict=not_configured. 반면 실제 Castling 컴포넌트를 요구한 계획은 [명시적으로 거절](cdc5102d11a146f9b424881907792f19.json). |

복구 검사는 선언한 첫 trigger/입력·이동·횟수·timeScale 조건에 한정한다. 복귀 위치, Fade alpha, 모든 중복 재시작 사례나 레벨 전체 퍼즐 완주까지 인증하지 않았다. `startDistance` 등 필요한 추가 조건은 이후 데이터로 선언할 수 있다.

## 격리·반복·비교

[반복/변형 배치](batch-20261015T085737396-959d01d9.json)는 case마다 Chapter_01을 Play 안에서 Single 로드했다. 이것은 scene instance 재생성이며 static/ScriptableObject/DDOL까지 초기화하는 완전 월드 되감기가 아니다. 실제 Stop→새 Play 경계도 별도로 여러 번 확인했고 전역 옵션은 바꾸지 않았다.

- [같은 계획 비교](comparison-repeat.json): samePlan=true/같은 기록된 물리·movement settings지만 sameInitialBodies=false였다. 로드 뒤 초기 접촉/낙하 시점 차이로 settle 높이 차 약 -0.06867을 그대로 기록했다. 교환 이후 단계의 변위·높이·샘플 차이는 0이었다. 시작 상태 동일성이나 결정론을 주장하지 않는다.
- [짧은 Slide 변형 비교](comparison-variant.json): 같은 씬, 다른 계획임을 표시. Slide 15→8 샘플, 변위 차 약 -2.397675, 추가 tail은 unmatched로 남았다. 누락/추가 단계를 조용히 건너뛰지 않는다.
- 배치의 최소 준비는 한 runtime frame과 Editor 0.1초 경과다. 접지/완전 안정 상태를 보장하지 않으므로 초기 상태와 계획 settle·조건을 함께 읽어야 한다.

## 소유권·취소·리로드·Stop

- [전체 배치 취소](batch-20261015T085649814-b8ce58de.json): 소유 child cancelled와 후속 not_run을 기록했다. 실제 Slide 중 키 해제 뒤 [후처리 스냅샷](slide-after-cancel.json)은 IsMoving=false, movement=true, 중력3.5였다. 일회성 pause fixture는 실제 Slide=true를 관측한 뒤 자신을 해제했고 이후 pause도 풀었다.
- [raw Start 소유권 거절](ownership-rejection.json): 배치가 실행권을 가진 동안 일반 Start를 거절했고 기존 실행을 교체하지 않았다.
- [child만 취소 뒤 Basic 계속](batch-20261015T090120738-9a2ca62c.json): child cancelled+completed, batch state=completed지만 verdict=not_evaluated. 중단 child를 observations_only나 passed로 숨기지 않았다.
- [실제 스크립트 리로드 중 배치](batch-20261015T090220404-43959ea7.json): batch/child cancelled, 사유 Assembly reload, 후속 not_run. [리로드 뒤 자원](after-batch-reload.json)과 최종 Stop 감사에서 소유 기기/observer 잔류가 없었다. cleanup은 beforeReload 시점이며 엔진의 이후 PlayerInput 재배정까지 동일하게 보장하지 않는다.
- [Play Stop 중 배치](batch-20261015T090543657-644869c5.json): batch/child cancelled, 후속 not_run. [child 결과](20261015T090544221-cc2c6e2e.json)는 cleanupSucceeded/ownedDeviceAbsent/postStopAudit 모두 true다.

마지막 수정은 cancelled child 집계와 `child ID/path·case 기록→추가 Write` 순서 보완이었다. 수치 판정/입력 동작은 바꾸지 않았으며 마지막 순서는 위 리로드·Stop에서 실제 종료 결과를 회수하는 것으로 확인했다. 위험한 파일 권한 fault injection은 수행하지 않았다.

## 원래 D3와 자원 보존

[시작 fingerprint](edit-before.json), [첫 Stop 뒤](edit-after-first-play.json), [반복 뒤](edit-after-repeat.json), [최종](edit-final.json)의 root 목록, dirty=true, 주요 serialized 기록 81개 SHA256가 모두 같다:
`7DF74E4E9F8A2C6F40019309AD1CD36BDC408B3AD6D012828917BA4199389C3C`.

Transform·Rigidbody/Collider·PlayerInput·컨트롤러·Entry의 주요 직렬화 상태를 비교했으며 씬의 모든 내적 상태가 동일함을 증명하는 검사는 아니다. 편집 씬 저장·폐기·ClearDirtiness·편집 원본 reload를 하지 않았다. EnterPlayModeOptionsEnabled=false/options=None도 보존했다.

[최종 자원](final-resources.json): D3 Edit/dirty=true, pause=false, timeScale1/background=false, 실제 기기 1..4만 남음, current Keyboard1, 양쪽 asset/raw map 필터 null, movement Jumped/Landed callback0, 물리 observer0, runner/batch Editor update callback0, 둘 다 idle. 직접 몸체 쓰기는 하지 않았다. 정리는 입력 자원·콜백·임시 설정을 다루며 런타임 월드를 되감는 보장은 아니다.

총괄의 최종 대조에서 시작 1090개 기존 파일 중 허용된 source/가이드 두 파일만 변경, 삭제0, 새 helper/meta만 추가됐다. branch/HEAD/index를 유지했다. 작업 파일 공백·로컬 링크와 meta GUID 검사는 통과했다. 전체 git diff --check의 기존 Chapter_01 공백 6곳은 사용자 변경이라 수정하지 않았다.

## 남는 기존 제약: Chapter_01 Play 중 스크립트 리로드

최종 Console을 0이라고 보고하지 않는다. 실제 리로드 검증에서 기존 `PlayerGroundParticles.Update` line101 NullReferenceException이 발생했다([최초 상세 로그](console-after-reload.json)). 공유 소스를 읽으면 입자 풀은 Awake에서 생성되지만 Update가 renderer를 바로 참조한다. 리로드 뒤 캐시 수명 문제라는 해석은 기존 [이슈 #11](https://github.com/nelu572/CASTLING/issues/11)과 일치한다.

도구를 실행하지 않은 별도 baseline에서도 확인했다:

1. 새 Play → Chapter_01 Play 전용 Single 로드. Start/StartFresh/StartBatch를 호출하지 않음.
2. [전 상태](native-reload-before.json): 오류0/경고0, runner/batch idle, timeScale1, 옵션 None(false).
3. EditorUtility.RequestScriptReload 뒤 [후 상태](native-reload-after.json): 조회 시 오류3932/경고0, runner/batch는 계속 idle. 오류 개수는 프레임에 따라 늘며 고정 재현 기준이 아니다.
4. [무도구 상세 stack](native-reload-console.json)이 동일 PlayerGroundParticles.Update line101을 가리킨다. Stop 뒤 최종 D3 fingerprint와 자원 상태는 위와 같다.

이는 자동화 자원 정리 실패와 구분되는 기존 게임 컴포넌트의 리로드 제약이다. 총괄이 [#11 검증 코멘트](https://github.com/nelu572/CASTLING/issues/11#issuecomment-6188631289)에 이번 증거를 추가했다. [코멘트 편집 원문](issue-11-comment.md)은 총괄 소유의 버그 검증 기록이며 사용자 결과 검토 및 후속 이슈 대조가 끝날 때까지 보존한다. 코드 편집 전 Stop/컴파일 뒤 새 Play를 권장하며 게임 전체의 리로드 안전은 보장하지 않는다.

## 보존·정리

사용자 결과 검토까지 위 계획·조건별 최소 실행/배치 JSON, 비교, fingerprint·자원 snapshot, #11 재현 자료와 총괄 LOOP/이슈 코멘트 원문을 보존한다. 이전 playtest-automation 폴더는 사용자 검토 대기이므로 건드리지 않았다. 기능별 증거를 대체한 성공 반복/중복 자료만 정리하며, 사용자 검토 완료 후 임시 자료를 정리하고 재사용 사용법은 기존 가이드에 남긴다.
