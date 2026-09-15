# PR 생성

사용자가 PR 생성을 명시적으로 요청했을 때만 사용한다. GitHub 이슈 연결, 릴리스 자동화, 라벨 생성은 이 절차의 전제가 아니다.

## 준비 확인

1. 현재 브랜치, 작업 트리, 원격 브랜치, 기존 PR을 확인한다.
2. base 브랜치는 원격 저장소의 기본 브랜치를 우선 사용한다. 추측으로 `main` 또는 `develop`을 고정하지 않는다.
3. 커밋되지 않은 변경사항이 있으면 PR을 만들지 않고 커밋 여부를 사용자에게 확인한다.
4. 변경된 코드와 [UNITY_VERIFY.md](UNITY_VERIFY.md)의 검증 결과를 정리한다.
5. [GITHUB.md](../harness/GITHUB.md)에서 변경 유형 하나와 관련 영역 라벨을 고른다. GitHub에 실제 존재하는 라벨만 적용한다.

## 제목과 본문

- 제목: `[Feature]`, `[Fix]`, `[Improve]`, `[Refactor]`, `[Design]`, `[Docs]`, `[Test]`, `[Chore]` 중 하나로 시작하는 한글 제목
- 본문은 [.github/PULL_REQUEST_TEMPLATE/task.md](../../.github/PULL_REQUEST_TEMPLATE/task.md) 양식을 사용해 한글로 작성하고, `.meta` 파일만의 변경은 언급하지 않는다.

## Assignees

- PR에는 담당자를 반드시 지정한다.
- 사용자가 담당자를 지정하면 해당 GitHub 사용자명으로 지정한다.
- 지정이 없으면 `gh api user -q .login`으로 확인한 현재 GitHub 사용자를 담당자로 지정한다.
- 사용자명을 확인하거나 지정할 수 없으면 PR 생성 전에 멈추고 사용자에게 요청한다. 담당자 없는 PR을 만들지 않는다.

## 생성 전 확인과 생성

사용자에게 `base ← head`, PR 제목·본문 요약, 담당자, 적용 라벨, 검증 결과를 보이고 승인을 받는다. 승인 후에만 `gh pr create`로 생성하고 결과 URL, 담당자, 라벨을 보고한다.
