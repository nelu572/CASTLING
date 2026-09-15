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

## 안전한 변경과 확인

- Unity가 만든 `.unity`, `.prefab`, `.asset`의 YAML과 `.meta` 파일을 임의로 편집하지 않는다.
- 새 에셋·이동한 에셋은 대응 `.meta` 파일을 함께 관리한다.
- 씬·프리팹 변경 뒤에는 Unity Editor에서 누락된 참조와 컴포넌트 경고를 확인한다.
