# CASTLING

2인 협동 물리 플랫포머·퍼즐 게임입니다. 플레이어는 **킹**과 **룩**을 조작하며, 킹의 **CASTLING**으로 두 캐릭터의 현재 위치를 교환합니다.

체스의 흑백·격자·말을 시각적 모티프로 사용하지만, 실제 체스 규칙의 구현을 목표로 하지는 않습니다.

## 개발 환경

- Unity `6000.0.68f1`
- Universal Render Pipeline (URP)

## 폴더 구성

```text
.github/          # GitHub PR·이슈 양식
.templates/       # 자료·검사·제작 기록을 추가하는 양식
├─ RESOURCE_TEMPLATE/
│  └─ template.md
├─ VERIFICATION_TEMPLATE/
│  └─ template.md
└─ RECORD_TEMPLATE/
   └─ template.md
.work/            # 작업별 임시 자료와 정리 안내
.codex/           # Codex 하네스와 작업 절차
├─ harness/       # 지속적인 작업 규칙
└─ skills/        # 커밋·PR·이슈·검증·Notion 안내 갱신 절차 파일
.reference/       # 프로젝트 사용 안내·기획·기록
├─ guides/        # 룸·배경·플랫폼 에셋 사용 안내
├─ notion/        # Notion 기획 원문 안내
└─ records/       # 보존할 제작 결정과 미확정 범위
```

## 기획 문서

최신 기획은 [Notion의 CASTLING 페이지](https://app.notion.com/p/3dcb3e6e776c80bca808c23441091ae6?source=copy_link)를 기준으로 합니다. 현재 확정 범위와 문서 위치는 [.reference/notion/NOTION_GUIDE.md](.reference/notion/NOTION_GUIDE.md)에서 확인할 수 있습니다.

## 작업 문서

작업 절차는 `.codex/skills`의 Markdown 파일로 관리하고 [AGENTS.md](AGENTS.md)에서 요청별로 연결한다. 게임 코드는 `Assets/Scripts`, 씬은 `Assets/Scenes`, 시각 에셋은 `Assets/Sprite`와 `Assets/Materials`에 둔다.

편집 원본·생성 도구는 [Tools/Art](Tools/Art/README.md)에 사용 방법과 함께 둔다. 자료나 폴더를 추가할 때는 [자료 추가 기준과 양식](.codex/harness/TEMPLATES.md)을 따른다. 스크린샷·로그·일회성 결과는 [.work](.work/README.md)의 작업별 폴더에 모으며, 안내에 따라 작업 마무리 때 정리한다.

작업 규칙은 [AGENTS.md](AGENTS.md)와 [Unity 하네스](.codex/harness/UNITY.md), 검증 절차는 [Unity 변경 검증](.codex/skills/UNITY_VERIFY.md)을 따른다. 룸·타일 작업에는 [룸 만들기](.reference/guides/ROOM_AUTHORING.md), [배경 구조물 조립](.reference/guides/BG_STRUCTURE_MODULAR.md), [플랫폼 타일 사용법](.reference/guides/PLATFORM_SHEETS.md)을 참고한다.

[Chapter_01 기록](.reference/records/CHAPTER_01.md)은 당시 사용자 결정과 미확정 범위를 모은 문서다. 현재 씬 설정이나 확정 기획을 대신하지 않는다.
