# 챕터 UI 글꼴

- 목적: Chapter_01과 개발용 LevelCollection에 저장된 일시정지·재시도·완료 UI의 한글을 렌더한다. 시작 전 조작 안내는 사용자 피드백으로 제거했다. 비활성 완료 패널의 보존은 레벨의 완료 규칙 채택을 뜻하지 않는다.
- 원본: [Google Fonts의 Nanum Gothic Regular](https://github.com/google/fonts/tree/main/ofl/nanumgothic). `NanumGothic-Regular.ttf`와 `OFL.txt`를 함께 보존한다. 원본 SHA256은 `76f45ef4a6bcff344c837c95a7dcc26e017e38b5846d5ae0cdcb5b86be2e2d31`이며, 2026-10-18에 가져왔다. 재배포 시 저작권 고지와 OFL을 포함한다.
- 결과물: `ChapterUI_SDF.asset`은 위 원본에서 만든 Static TMP 글꼴이며 Material·Atlas를 하위 에셋으로 가진다. 기본 TMP 셰이더는 프로젝트에 가져온 TMP Essential Resources를 사용한다.
- 편집: 원본 TTF를 TMP Font Asset Creator의 Source Font로 사용한다. 씬의 비활성 패널까지 모든 TMP 문구와 숫자를 Custom Characters로 넣고 Sampling Point Size 48, Padding 5, SDFAA, 1024×1024로 생성한다. 글리프 추가 후 Static으로 저장하고 UI의 Font Asset 참조를 연결한다. 에셋을 교체하면 Material과 Atlas 참조도 함께 확인한다. 운영체제 글꼴 경로에 의존하지 않는다.
- 검증: Chapter_01 Play의 2:1 Game View에서 조작 안내·완료 문구를 확인했다. 문구·글꼴·화면비를 바꾸면 누락 글리프, 넘침, 메뉴 버튼 선택을 다시 확인한다. 모든 한글을 미리 포함한 글꼴은 아니다.
- 보존·정리: 챕터 UI에서 참조하는 동안 원본·라이선스·SDF를 함께 보존한다. 글꼴을 교체하면 씬과 TMP 참조를 갱신한 뒤 더 이상 쓰지 않는 에셋을 Unity Editor에서 정리한다.
