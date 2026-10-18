# CASTLING

2인 협동 물리 플랫포머·퍼즐 게임입니다. 플레이어는 **킹**과 **룩**을 조작하며, 킹의 **CASTLING**으로 두 캐릭터의 현재 위치를 교환합니다.

체스의 흑백·격자·말을 시각적 모티프로 사용하지만, 실제 체스 규칙의 구현을 목표로 하지는 않습니다.

## 개발 환경

- Unity `6000.0.68f1`
- Universal Render Pipeline (URP)

## 폴더 구성

```text
.github/          # GitHub PR·이슈 양식
.templates/       # 레벨 설계·자료·검사·제작 기록을 추가하는 양식
├─ LEVEL_DESIGN_TEMPLATE/
│  └─ template.md
├─ RESOURCE_TEMPLATE/
│  └─ template.md
├─ VERIFICATION_TEMPLATE/
│  └─ template.md
├─ RECORD_TEMPLATE/
│  └─ template.md
└─ LEVEL_LIBRARY_TEMPLATE/
   └─ template.md
.work/            # 작업별 임시 자료와 정리 안내
.codex/           # Codex 하네스와 작업 절차
├─ harness/       # 지속적인 작업 규칙
└─ skills/        # 레벨 디자인·커밋·PR·이슈·검증·Notion 안내 갱신 절차 파일
.reference/       # 프로젝트 사용 안내·기획·기록
├─ guides/        # 레벨 설계 근거·룸·배경·플랫폼 에셋 사용 안내
├─ levels/        # 독립 레벨 보관소·레벨별 해법과 재사용 안내
├─ notion/        # Notion 기획 원문 안내
└─ records/       # 보존할 제작 결정과 미확정 범위
```

## 기획 문서

최신 기획은 [Notion의 CASTLING 페이지](https://app.notion.com/p/3dcb3e6e776c80bca808c23441091ae6?source=copy_link)를 기준으로 합니다. 현재 확정 범위와 문서 위치는 [.reference/notion/NOTION_GUIDE.md](.reference/notion/NOTION_GUIDE.md)에서 확인할 수 있습니다.

## 작업 문서

작업 절차는 `.codex/skills`의 Markdown 파일로 관리하고 [AGENTS.md](AGENTS.md)에서 요청별로 연결한다. 게임 코드는 `Assets/Scripts`, 씬은 `Assets/Scenes`, 시각 에셋은 `Assets/Sprite`와 `Assets/Materials`에 둔다.

편집 원본·생성 도구는 [Tools/Art](Tools/Art/README.md)에 사용 방법과 함께 둔다. 자료나 폴더를 추가할 때는 [자료 추가 기준과 양식](.codex/harness/TEMPLATES.md)을 따른다. 스크린샷·로그·일회성 결과는 [.work](.work/README.md)의 작업별 폴더에 모으며, 안내에 따라 작업 마무리 때 정리한다.

작업 규칙은 [AGENTS.md](AGENTS.md)와 [Unity 하네스](.codex/harness/UNITY.md), 검증 절차는 [Unity 변경 검증](.codex/skills/UNITY_VERIFY.md)을 따른다. 룸·타일 작업에는 [룸 만들기](.reference/guides/ROOM_AUTHORING.md), [배경 구조물 조립](.reference/guides/BG_STRUCTURE_MODULAR.md), [플랫폼 타일 사용법](.reference/guides/PLATFORM_SHEETS.md)을 참고한다.

독립 레벨의 아이디어·해법은 [레벨 보관소](.reference/levels/README.md)에서 찾는다. 목록에서 레벨별 카드·실제 씬·평가로 이동하며 [카드 양식](.templates/LEVEL_LIBRARY_TEMPLATE/template.md)으로 새 후보를 추가한다. A·B를 포함한 보관 레벨의 챕터·순서는 별도 결정한다. 사용자 승인·평가는 [독립 레벨 결정 기록](.reference/records/LEVEL_COLLECTION.md), Chapter_01의 결정은 [해당 챕터 기록](.reference/records/CHAPTER_01.md)에서 관리한다.

레벨 작업은 [레벨 디자인 절차](.codex/skills/LEVEL_DESIGN.md)에 따라 **요청·게임 분석 → 목표 경험/설계 전략 선택 → 공간 설계·승인 → 구현 → 기술·게임플레이·공간 평가 → 개선** 순서로 진행하며, 요청한 단계까지만 수행한다. [프로젝트 근거](.reference/guides/LEVEL_DESIGN_CONTEXT.md), [작품별 레퍼런스 분석 자료](.reference/guides/LEVEL_DESIGN_REFERENCES.md), [설계 양식](.templates/LEVEL_DESIGN_TEMPLATE/template.md)을 구분해 사용한다. 특정 레퍼런스·지형형·기믹을 공통 기본값으로 삼지 않으며 부족한 조건은 프로젝트에서 파악한다. 요청 예시는 절차 문서에 있다. 자동 배치·도달성 검사 프로그램은 포함하지 않는다.

이번 검토 브랜치의 [하네스 검토 범위·검증 보고서](.work/20261015-level-design-harness-review/REPORT.md)는 문서 구조, 세 가상 요청의 모의 점검과 한계를 설명한다. 검토용 보고서만 선별해 포함했으며 테스트용 Unity 씬·에셋은 포함하지 않는다.
