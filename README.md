# CASTLING

2인 협동 물리 플랫포머·퍼즐 게임입니다. 플레이어는 작은 **킹**과 무거운 **룩**을 조작하며, 공통 기술인 **CASTLING**으로 두 캐릭터의 현재 위치를 교환합니다.

체스의 흑백·격자·말을 시각적 모티프로 사용하지만, 실제 체스 규칙의 구현을 목표로 하지는 않습니다.

## 개발 환경

- Unity `6000.0.68f1`
- Universal Render Pipeline (URP)

## 폴더 구성

```text
.docs/           # Notion 기획 색인과 Unity 작업 하네스
Assets/
├─ Art/           # 시각 에셋
├─ Audio/         # 사운드 에셋
├─ Prefabs/       # 재사용 오브젝트
├─ Scenes/        # 게임 씬과 개발용 실험 씬
├─ Scripts/       # 기능·도메인별 코드
└─ Settings/      # Unity·렌더링 설정
```

## 기획 문서

최신 기획은 [Notion의 CASTLING 페이지](https://app.notion.com/p/3dcb3e6e776c80bca808c23441091ae6?source=copy_link)를 기준으로 합니다. 현재 확정 범위와 문서 위치는 [.docs/notion/NOTION_GUIDE.md](.docs/notion/NOTION_GUIDE.md)에서 확인할 수 있습니다.
