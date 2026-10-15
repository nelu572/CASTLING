# 플레이테스트 자동화 구현·검증 결과

외부 검토 공유 범위: 이 문서는 최초 자동화 구현 당시의 검증 기록이며, 아래 SHA256·Console 상태는 그 시점의 결과다. 현재 코드의 확장·최종 제약은 함께 공유하는 [범용 레벨 검증 보고서](../20261015-level-validation/REPORT.md)와 [실행 가이드](../../.reference/guides/PLAYTEST_AUTOMATION.md)를 따른다. 이번 검토 커밋의 `.work` 자료는 두 REPORT.md뿐이다. 아래 JSON·스냅샷 링크는 커밋에 포함하지 않은 로컬 검증 근거이므로 GitHub에서는 열리지 않는다. 당시 사용자 씬 변경도 제외했으므로 이 커밋만으로 동일 씬 조건 재구성이나 원시 증거의 독립 대조까지 가능하다고 주장하지 않는다.

## 완료 범위

Editor 전용 `Assets/Editor/PlaytestAutomation.cs`와 실행 안내 `.reference/guides/PLAYTEST_AUTOMATION.md`를 추가했다. 기존 `VERIFICATION_CHECKS.md`에 짧은 연결을 추가했다. 공유 gameplay 코드, 씬·프리팹·물리·입력 에셋은 수정하지 않았다. 커밋·패키지 설치는 하지 않았다.

최종 C# SHA256: `3905066AB952050A123B541C7A99FFE044EC06BE14A8FF4503E65D1F5E4B209F`.

Unity 6000.0.68f1 / Input System 1.18.0, 현재 CASTLING 프로젝트와 일치하는 stdio MCP 인스턴스에서 검증했다. 검사 전 Console 오류·경고 0, 최종 Console 오류·경고 0이다. 최종 Editor 상태는 Edit Mode, 원래 `Dev_PressureRelay_D3`, `dirty=true`, timeScale=1, background=false, pause=false다.

시작 때 D3의 dirty=false였고 첫 Play Stop 뒤 true가 됐다. 이를 저장하거나 지우지 않았다. 이후 Dev_Gameplay는 **Play Mode 전용 Single 로드**로 격리했고 Stop 뒤 원래 D3와 dirty=true가 유지됐다. 원본 파일 보존과 편집 dirty 상태의 완전 동일 복원은 별개다. 파일 해시 보존 대조는 총괄이 수행한다.

## 최종 코드의 반복·실측

두 실행 모두 저장된 `held-sequence-plan.json`과 Dev_Gameplay의 현재 저장 시작점에서 시작했다. 테스트에서 위치·속도를 쓰지 않았다.

- [반복 1](20261015T081850609-46f03a9d.json)
- [반복 2](20261015T081941765-7f701aa2.json)
- [동일 코드·계획 비교](repeat-comparison.json)

두 실행 모두 completed이며 9단계의 191개 유효 물리 샘플, 단계별 변위·높이·샘플 수가 정확히 일치했다. 전체 샘플은 207 / 192개로 달랐다. 나머지 16 / 1개는 다음 InputSystem 업데이트 대기 `step=-1` tick이다. Dynamic 입력 업데이트와 Editor 지연의 영향이며 결정론·정확한 실시간 재생을 보장하지 않는다.

| 관측 | 결과 (월드 단위) |
| --- | --- |
| 킹 첫 0.2초 걷기 | +1.307999, 관측 최대 수평 속도 9.000002 |
| 킹 같은 방향 held 다음 0.2초 | +1.800003, 신규 performed=0이어도 실제 가상기기 Move Action 값으로 observed |
| 킹 방향 전환 0.4초 | -1.463989 |
| 킹 제동 | -0.360001 |
| 킹 정지 점프 | sampled height 약 4.28645, Jump performed / Jumped / Landed 각 1, 안정 접지 2 tick |
| 룩 0.3초 걷기 | -2.208000, 관측 최대 수평 속도 약 9 |
| 룩 제동 | -0.360001, subject=rook으로 raw Rook과 일치 |
| 룩 정지 점프 | sampled height 약 4.28645, Jump performed / Jumped / Landed 각 1, 안정 접지 2 tick |

고정 간격은 약 0.02초다. 최고점은 샘플 하한이고 비접지 시간은 비접지 샘플 수 × 간격이다. 입력기기 ID, 실제 Action control/phase, 양쪽 위치·속도·접지와 접촉점/법선은 JSON에 있다. 현재 지형·접촉에서의 관측이며 캐릭터의 일반 최대 능력이 아니다.

초기 D3 기본 +1 방향 검사는 [초기 결과](20261015T080014078-586930ad.json)에 보존했다. 킹 걷기/점프는 observed였지만 룩이 플랫폼 끝을 넘어 걷기 접지 조건을 잃고 다음 점프의 시작도 공중이어서 failed였다. -1 재시도도 [대상 상태 변화](20261015T080208011-c13388b2.json)로 실패했다. 씬/물리를 고치거나 이 실패를 합격으로 바꾸지 않았다. 이 두 파일은 최종 raw map 보완 전 버전이다.

## raw 기기 필터·상태 복원

최종 코드의 [복원 실행](20261015T082042927-b617d1d3.json)은 completed/cleanupSucceeded=true였다.

- [실행 전](raw-filter-before.json), [실행 후](raw-filter-after.json): Settings JSON·객체, 모든 기기, 필터·pairing·neverAutoSwitch·movement 콜백 수가 동일했다. King never=false, background=true, timeScale=0.75와 별도 fixture Keyboard도 보존했다.
- 킹은 asset=A / raw map=null, 룩은 asset=A / raw map=A였다. 외부 snapshot은 도구 helper를 쓰지 않고 패키지의 DeviceArray 값·haveValue·count를 직접 읽었다.
- [상속 동작 확인](raw-filter-inheritance-probe.json): 완료 뒤 두 asset을 별도 기기 B로만 변경했을 때 킹 effective map은 B를 따르고 룩 explicit map은 A를 유지했다. 이후 검사에서 바꾼 asset 필터만 A로 돌렸다.
- Input System 공개 map.devices getter는 asset의 상속 결과도 반환한다. 이를 그대로 저장했던 초안 결함은 raw nullable read-only reflection으로 보완했고, 내부 구조 불일치 시 시작을 거절한다. reflection으로 내부 필드를 쓰지 않는다.

최종 코드의 [Play Stop 중단](20261015T082144793-ed2936cb.json)은 cancelled, 현재 partial / 후속 not_run, raw 필터 복원 검증과 cleanupSucceeded=true, 소유 기기 부재, Stop 뒤 감사 모두 통과했다.

## 경계 검증

다음 lifecycle 사례는 raw map 보완 직전 코드에서 실행했고, 마지막 수정은 raw override 읽기·복원 검증에 한정했다. 영향받는 복원/Play Stop은 위 최종 코드로 재검증했다.

| 사례 | 근거와 결과 |
| --- | --- |
| 명시 취소·동시 Start 거절 | [결과](20261015T080842512-6d5e5eb3.json), cancelled, 기존 실행 유지·거절 이벤트, partial/not_run, 소유 기기·marker 정리. 취소 전후 별도 [스냅샷](cancel-fixture-before.json), [스냅샷](cancel-fixture-after.json) 비교 |
| Pause 중 타임아웃 | [결과](20261015T080935594-d3ad03f1.json), timed_out, Editor 벽시계로 종료, partial/not_run |
| 소유 기기 외부 제거 | [결과](20261015T080950605-29a04426.json), failed/Owned keyboard was removed, 이후 QueueStateEvent 예외 반복·콜백 잔류 없음 |
| fixedDeltaTime 변경 | [결과](20261015T081048326-1131d8a2.json), failed, [스냅샷](dt-changed-after.json)에 외부 0.03 보존. 검사 종료 후 fixture에서 바꾼 값만 원래 간격으로 복원 |
| 부분 setup 실패 | [결과](20261015T081139704-7f9cb457.json), 잘못된 Keyboard control로 setup failed, 모든 단계 not_run, 생성 자원 정리 |
| 실제 domain/script reload | [결과](20261015T081206753-13ac4e71.json), interrupted/beforeAssemblyReload cleanup=true, [리로드 뒤](reload-after.json) 소유 기기·observer·콜백 잔류 없음 |
| 다중 controller·다른 active scene | [결과](target-selection.json), 선택 없음/복수 controller 거절, 명시 현재 ID 허용, 다른 active scene의 ID 거절, fixture와 Selection 복원 |
| 정상 run 뒤 invalid Start 뒤 Stop | invalid rejection이 최근 result를 바꿔도 실제 실행별 pending audit가 별도 유지돼 모든 실제 run의 postStopAuditPerformed/Absent=true |
| Edit Describe | [최종 결과](final-edit-description.json), 현재 씬·참조 몸체·바인딩 반환 성공. Edit grounded/contacts는 미관측 자리표시자 |

리로드의 cleanup=true는 **beforeAssemblyReload 시점** 복원을 뜻한다. 리로드 뒤에는 Unity PlayerInput의 사용자/기기 재초기화가 필터·pairing을 다시 바꿨다. 도구 Start 없이 같은 종류의 runtime fixture에서 RequestScriptReload한 [전](native-reload-before.json) / [후](native-reload-after.json)에도 같은 재구성이 발생했다. 어느 PlayerInput이 기기를 먼저 얻는지는 달랐다. 공유 코드나 리로드 뒤 사용자 필터를 강제 수정하지 않았으며, 엔진 리로드 뒤 모든 필터·pairing 동일성을 보장하지 않는다.

## 최종 정리·제한

[최종 외부 스냅샷](final-after-stop.json): 실제 원래 기기 1 Keyboard / 2 Mouse / 3 Touchscreen / 4 Pen만 남음, current Keyboard=1, Playtest observer=0, Edit 상태 movement Jumped/Landed callback=0, active run 없음. 다른 작업을 흉내 낸 fixture 기기는 테스트 도구의 cleanup에서 보존됐고, 검사 종료 때 fixture 소유자가 별도로 제거했다. 일부 일회성 snapshot 코드에서 Edit map=null을 잘못 읽은 예외는 snapshot 코드만 고쳐 최종 스냅샷을 다시 저장했다. 도구 Console 오류는 남지 않았다.

raw 상속 fixture가 일부러 설정한 Rook explicit map=A는 도구가 검사 시작값 A로 정확히 복원했다. Play Stop 뒤에도 InputActionAsset의 nonserialized 캐시에 남아 있어, fixture 소유자가 최종 두 실행의 pre-fixture raw map=null 근거로 **그 Rook default map override 하나만** null로 후처리했다. 이는 automation cleanup과 구분되는 검사 fixture 복구다. 다른 필터를 일괄 지우거나 입력 에셋/씬을 저장하지 않았다. 최종 외부 스냅샷은 이 후처리 뒤 다시 저장했으며 양쪽 raw map=null이다.

정리는 입력 자원·콜백·물리 관찰자·임시 Settings를 다룬다. 런타임 월드의 위치/진행을 되감는 기능은 없다. 동일 조건 비교는 fresh Play 또는 Play 전용 씬 로드로 한다. 저장 씬의 dirty를 자동 저장·폐기하지 않는다. 사람의 퍼즐 플레이, 체감 난이도·발견성·재미, 독립 빌드는 이번 자동화 검증 범위가 아니다.

본 작업 소스·가이드·meta의 공백/로컬 링크 검사는 통과했다. 전체 git diff --check의 Chapter_01.unity 6곳 trailing whitespace는 시작부터 있던 사용자 변경이며 수정하지 않았다. 새 meta는 Unity가 생성했다.

상태: 사용자 결과 검토 대기. 각 lifecycle 최소 결과, raw 필터/리로드 비교, 최종 반복 2개와 계획, 최종 스냅샷, LOOP/REPORT를 검토 근거로 보존한다. 검토가 완료되면 임시 JSON·메모를 정리하고 재사용 API 설명만 정식 가이드에 남긴다.
