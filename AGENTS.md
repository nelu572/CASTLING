# CASTLING 작업 지침

- 기존 파일과 작업 트리의 변경은 사용자의 작업으로 간주하고 존중한다. 템플릿·하네스와 다르다는 이유만으로 사용자 변경을 되돌리거나 삭제하지 않는다.
- 사용자 변경과 충돌할 수 있는 수정·삭제·자동 정리 전에는 현재 내용을 확인하고, 의도가 불명확하면 사용자에게 먼저 확인한다.
- 작업 중 이전에 확인한 값·씬 상태·컴포넌트 설정이 달라졌거나, 구현자가 예상한 값과 현재 값이 다르면 사용자 디버깅·튜닝 변경으로 간주한다. 요청에 변경이 명시되지 않은 한 임의로 덮어쓰거나 원복하지 말고, 변경이 필요하면 현재 값과 변경 이유를 제시해 사용자에게 먼저 확인한다.
- 구현·기획·콘텐츠 작업 전에는 [.reference/notion/NOTION_GUIDE.md](.reference/notion/NOTION_GUIDE.md)에서 관련 기획 원문을 확인한다. Notion 기획은 이 프로젝트의 기준이다.
- 현재 기획에서 확정되지 않은 CASTLING 발동 조건, 물리 수치, 조작 방식, 퍼즐 규칙은 임의로 정하지 않는다. 구현에 필요한 선택지가 남아 있으면 사용자에게 확인한다.
- 요청과 기획에 없는 기능, 콘텐츠, 리팩터링은 추가하지 않는다.
- `.codex/harness`에는 지속적인 작업 규칙, `.reference/guides`에는 룸·에셋 사용 안내, `.reference/notion`에는 기획 원문 안내, `.reference/records`에는 보존할 제작 결정을 둔다. 반복해서 필요한 발견은 해당 지침에 반영하고 일회성 산출물을 문서 폴더에 쌓지 않는다.
- `.codex/skills`에는 커밋·PR·이슈·검증·Notion 안내 갱신 절차를 파일 단위로 둔다. 아래 요청별 링크로 읽으며, 일회성 검사 코드나 작업 결과는 넣지 않는다.
- 프로젝트 문서용 양식은 `.templates/<종류>_TEMPLATE/template.md`에 두고, 사용법·관리 기준은 `.codex`에서 관리한다. 보존할 편집 원본·재사용 검사·제작 기록을 추가하거나 임시 작업 폴더를 만들 때는 [자료 추가 기준과 양식](.codex/harness/TEMPLATES.md)을 따른다. 새 자료의 용도·사용 방법·검증 상태·보존 및 정리 기준을 작성하고 관련 지침에서 연결한다.
- `.reference/records/CHAPTER_01.md`에는 보존할 사용자 결정과 미확정 범위만 남긴다. 현재 씬 상태·수치는 Unity Editor와 코드에서 확인하며, 기록의 값으로 사용자 튜닝을 되돌리지 않는다.
- Chapter_01의 조작·퍼즐·배경·카메라 작업 전에는 [제작 결정 기록](.reference/records/CHAPTER_01.md)을 관련 Notion 기획과 함께 읽는다.
- 스크린샷·일회성 검사 코드·JSON·로그·시안은 [.work 안내](.work/README.md)에 따라 작업별 폴더에 모은다. 작업 시작·재개, 검사·구현 단락 종료, 검토 완료, 커밋·PR·완료 보고 전에 해당 작업 자료의 필요성과 보존 종료 조건을 확인하고, 필요 없어진 자료를 별도 정리 요청 없이 정리한다. 검토 대기·미해결·보존 자료를 남기면 작업별 안내를 갱신하고 경로·이유·정리 조건을 보고한다. `.work/README.md`와 루트는 유지한다. 화면 확인 방법은 [.codex/harness/UNITY.md](.codex/harness/UNITY.md)를 따른다.
- Unity 씬·프리팹·에셋 작업은 [.codex/harness/UNITY.md](.codex/harness/UNITY.md)를 먼저 읽는다.
- 코드·씬·에셋 변경 후 검증을 요청받거나 커밋 전에 검증할 때는 [.codex/skills/UNITY_VERIFY.md](.codex/skills/UNITY_VERIFY.md)를 따른다.
- 사용자가 커밋을 명시적으로 요청하면 stage·commit 전에 [.codex/skills/AUTO_COMMIT.md](.codex/skills/AUTO_COMMIT.md)를 따른다.
- GitHub PR, 라벨, 이슈 관련 작업은 [.codex/harness/GITHUB.md](.codex/harness/GITHUB.md)를 먼저 읽는다. 사용자가 PR 또는 이슈 생성을 명시적으로 요청하면 각각 [.codex/skills/AUTO_PR.md](.codex/skills/AUTO_PR.md), [.codex/skills/AUTO_ISSUE.md](.codex/skills/AUTO_ISSUE.md)를 추가로 따른다.
- 사용자의 지속적인 사전 요청에 따라 작업 중 새로 발견해 재현한 버그는 수정 전에 같은 목적의 열린 이슈를 확인하고, 중복이 없으면 [이슈 생성 절차](.codex/skills/AUTO_ISSUE.md)에 따라 먼저 등록한다. 해당 버그 이슈 생성은 매번 재승인을 요구하지 않으며, 수정·검증 결과는 이슈 코멘트에 기록한다.
- 사용자가 Notion 안내 갱신을 명시적으로 요청하면 [.codex/skills/REFRESH_NOTION_GUIDE.md](.codex/skills/REFRESH_NOTION_GUIDE.md)를 따른다.
