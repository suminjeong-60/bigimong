# 비기몽 Unreal 클라이언트 이식: 첫 미리보기

이 폴더는 기존 게임의 **Unreal Engine 5.8 소스 프로젝트**입니다. 현재 홈 아바타/알을 확인하는 최소 화면이며 기존 온라인 전투, 걸음 동기화, 부화 저장, AR 바닥 배치, 게임 UI는 연결 전입니다. Unreal Editor가 없는 환경에서 파일 구조와 모델 해시는 확인했지만 프로젝트의 엔진 컴파일 및 APK 설치는 확인하지 못했습니다.

## 클라우드에서 계속 개발

[이 브랜치의 GitHub Codespaces 열기](https://codespaces.new/suminjeong-60/bigimong/tree/feature/unreal-migration-v0.1?quickstart=1). 처음 생성할 때 사용 계정의 GitHub Codespaces 사용량이 적용됩니다. `.devcontainer/devcontainer.json`은 Node/ Python/ C++ 소스 검사와 개발 서버 포트 전달을 준비하며 `scripts/verify-unreal-cloud.sh`를 자동 실행합니다. 이 환경은 **소스 코딩·서버 시험용**이고 Unreal Editor 및 GPU를 제공한다고 가정하지 않습니다.

현재 옮긴 순수 C++ 부화 규칙은 `Source/Bigimong/Public/BigimongHatchCore.h`에 있으며 30,000점, 돌봄 2시간, 검증된 걸음의 중복 방지, 선택한 공룡 ID와 부화 단계 전환을 독립 테스트합니다. `BeginHatchDurably`는 최신 저장 상태를 다시 읽고, 공룡 ID 추첨 후 저장 결과가 불확실하면 재조회하는 **저장소·난수 인터페이스의 계약**을 테스트합니다. 실제 Unreal 저장소의 원자적 revision 비교·저장, 1~30 균등 추첨, Health Connect/서버 연결과 UI는 아직 구현·결합되지 않았습니다. 저장하지 못한 후보 상태를 성공으로 표시해서는 안 됩니다.

## VARCO 원본 모델 연결

저장소 루트에서, 직접 소유한 0.3.1 APK를 지정합니다.

```bash
python3 scripts/prepare-unreal-varco.py --apk /path/to/01-BigiDragon-0.3.1-Compatible.apk
```

이 도구는 APK에 포함된 남녀 `GLB`의 고정 SHA-256과 동봉된 출처 기록, GLB 형식 및 폴리곤을 확인하고 이 프로젝트의 `PrivateAssets/Varco/`에 추출합니다. 원본 모델이나 계정 정보는 공개 저장소에 올리지 않습니다. 명령은 남성 500,000, 여성 25,000삼각형과 리그/애니메이션 없음 상태를 보고합니다. 남성 모델을 모바일 배포에 사용하기 전에 별도 리토폴로지가 필요합니다.

1. 설치된 Unreal Engine 5.8로 `Bigimong.uproject`를 열고 C++ 프로젝트 생성/빌드를 완료합니다.
2. 콘텐츠 브라우저에 `/Game/Varco` 폴더를 만들고 두 원본 GLB를 각각 가져옵니다. `Import as Skeletal Mesh`는 해제합니다. 원본에는 골격이 없습니다. glTF/GLB 가져오기가 보이지 않으면 에디터에서 Interchange glTF 가져오기 지원을 활성화합니다.
3. 가져온 정적 메시를 `/Game/Varco/SM_VARCO_Male`, `/Game/Varco/SM_VARCO_Female`로 이름 붙입니다. 생성된 머티리얼과 텍스처도 그 폴더 안에서 유지하고 기준 이미지에 맞춰 재질과 조명을 조정합니다. 프로젝트의 패키징 설정은 `/Game/Varco`를 굽도록 지정합니다. 실제 Android APK 안에 메시가 들어갔는지는 엔진 빌드 후 별도 확인해야 합니다.
4. 에디터에서 Play를 실행합니다. 기본 화면은 여성 아바타입니다. 마우스 가로 이동/손가락 가로 드래그로 회전, 마우스 클릭/탭으로 알/아바타 전환, 위로 스와이프하면 여성, 아래로 스와이프하면 남성입니다. 키보드는 `Tab`, `F`, `M`을 사용합니다.
5. 실기기 시험은 Unreal의 Android SDK/NDK와 ARCore 지원 Galaxy 기기에서 별도로 실행합니다. 개발용 패키지 `com.bigimong.unrealpreview`는 기존 앱의 `com.bigimong.app`을 덮어쓰지 않습니다.

모델을 가져오기 전에는 길쭉한 구형 개발용 표시물이 나옵니다. 이 표시물은 아바타 완성 품질을 나타내지 않습니다. 모델을 가져와도 첫 미리보기에는 목걸이, 옷, 눈/머리 커스터마이징, 표정, 걸음과 온라인 대전이 아직 없습니다.

## 계속 옮길 코드

| 현행 구현 | Unreal 목표 | 첫 미리보기 |
|---|---|---|
| Unity 홈/부화 상태 v2 | 동일한 30,000점·2시간·30종 선택 및 복구 | 미연결 |
| Android Health Connect/Keystore/BLE | 네이티브 브리지로 유지 후 Unreal 클라이언트에 결합 | 미연결 |
| Node 서버와 SQLite/매칭/정산 | API, 데이터, 서버 판정 유지 | 미연결 |
| Unity AR Foundation 대전 | Unreal Handheld AR/Google ARCore 및 서버 모션 동기화 | 플러그인만 켬 |
| VARCO 원본 및 커스터마이징 | 원본 정적 화면 → 모바일 최적화·리깅 → 15×15×15 옵션 | 정적 원본 가져오기 절차 |

상세 기준은 [`docs/superpowers/specs/2026-09-26-unreal-migration-design.md`](../../../docs/superpowers/specs/2026-09-26-unreal-migration-design.md)에 있습니다. 기존 Unity 앱과 Kotlin 앱을 제거하거나 기존 사용자 저장 데이터를 새 형식으로 재해석하지 않습니다.

## 이 환경에서 가능한 검사

```bash
python3 -m unittest discover -s tests -p 'test_prepare_unreal_varco.py' -v
BIGIMONG_TEST_APK=/path/to/01-BigiDragon-0.3.1-Compatible.apk python3 -m unittest discover -s tests -p 'test_prepare_unreal_varco.py' -v
npm test
bash scripts/verify-unreal-cloud.sh
```

이 검사는 Unreal 에디터·C++ 컴파일·AR 실기기 실행의 대체 증거가 아닙니다.
