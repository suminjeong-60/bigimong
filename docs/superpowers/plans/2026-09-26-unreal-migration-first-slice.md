# Unreal 이식 첫 단계 실행 계획 (2026-09-26)

기준: [전환 설계](../specs/2026-09-26-unreal-migration-design.md). 현행 기준 브랜치: `feature/v0.19-3d-hatch-home`.

## 작업 1: 기존 원본의 안전한 추출

- 추가: `scripts/prepare-unreal-varco.py`, `tests/test_prepare_unreal_varco.py`.
- APK의 `assets/models/provenance.json`과 두 GLB의 고정 SHA-256을 확인한다. GLB 구조를 읽어 삼각형·리그·애니메이션 수를 산출한다.
- 기본 출력은 Git에서 제외되는 `unreal/Bigimong/PrivateAssets/Varco/`. 인증 정보/아무 바이너리도 Git에 포함하지 않는다.
- 정상 0.3.1 APK와 잘못된 해시, 누락 자산, 손상된 GLB, 잘못된 덮어쓰기 사례로 검증한다.

## 작업 2: Unreal 프로젝트와 홈 화면의 최소 구동 경로

- 추가: `unreal/Bigimong/Bigimong.uproject`, `Source/Bigimong` 모듈, `Config/DefaultEngine.ini`, `Config/DefaultInput.ini`, `README.md`.
- 게임 모드가 카메라·조명·계란/아바타 자리의 미리보기를 띄우고, 터치/마우스 드래그로 선택된 모델을 회전하게 한다. 입력으로 남녀 전환과 알/아바타 전환을 제공한다.
- 원본 VARCO GLB를 에디터에서 PrivateAssets로부터 수동 import하여 지정한 Static Mesh 슬롯에 꽂는 절차를 문서화한다. 스킨이 없으므로 애니메이션 완료로 취급하지 않는다.
- 엔진 설치 환경에서 프로젝트 열기, 빌드 및 Android 실기기 미리보기를 후속 검증 단계로 명시한다.

## 작업 3: 마이그레이션의 연속성 점검

- 테스트: 기존 `npm test`, Python `unittest` 및 실제 APK의 SHA/삼각형 검증, `.uproject`/INI/구조 정적 검증, Git diff의 비밀/대형 파일 포함 여부.
- 문서: 데이터/전투/AR 이전 순서, 아직 연결되지 않은 Kotlin/서버 계약과 APK 미생성 사실을 README에 기재한다.
- 사용자가 Unreal Editor를 사용할 수 있는 머신에서 실제 빌드·AR 확인 전에는 완성된 앱이라고 보고하지 않는다.
