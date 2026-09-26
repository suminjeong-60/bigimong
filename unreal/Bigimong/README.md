# 비기몽 Unreal 클라이언트 이식: 첫 미리보기

이 폴더는 기존 게임의 **Unreal Engine 5.8 소스 프로젝트**입니다. 현재 홈 아바타/알을 확인하는 최소 화면이며 기존 온라인 전투, 걸음 동기화, 부화 저장, AR 바닥 배치, 게임 UI는 연결 전입니다. 갤럭시북의 Unreal 5.8.3에서 VARCO 부품과 얼굴 변형, 옵션 이미지 가져오기를 확인했습니다. C++ 컴파일과 Android APK 패키징은 이 컴퓨터의 Visual Studio 빌드 도구와 Unreal 설치본의 Android 선택 구성요소가 없어 완료되지 않았습니다.

## 클라우드에서 계속 개발

[이 브랜치의 GitHub Codespaces 열기](https://codespaces.new/suminjeong-60/bigimong/tree/feature/unreal-migration-v0.1?quickstart=1). 처음 생성할 때 사용 계정의 GitHub Codespaces 사용량이 적용됩니다. `.devcontainer/devcontainer.json`은 Node/ Python/ C++ 소스 검사와 개발 서버 포트 전달을 준비하며 `scripts/verify-unreal-cloud.sh`를 자동 실행합니다. 이 환경은 **소스 코딩·서버 시험용**이고 Unreal Editor 및 GPU를 제공한다고 가정하지 않습니다.

현재 옮긴 순수 C++ 부화 규칙은 `Source/Bigimong/Public/BigimongHatchCore.h`에 있으며 30,000점, 돌봄 2시간, 검증된 걸음의 중복 방지, 선택한 공룡 ID와 부화 단계 전환을 독립 테스트합니다. `BeginHatchDurably`는 최신 저장 상태를 다시 읽고, 공룡 ID 추첨 후 저장 결과가 불확실하면 재조회하는 **저장소·난수 인터페이스의 계약**을 테스트합니다. 실제 Unreal 저장소의 원자적 revision 비교·저장, 1~30 균등 추첨, Health Connect/서버 연결과 UI는 아직 구현·결합되지 않았습니다. 저장하지 못한 후보 상태를 성공으로 표시해서는 안 됩니다.

## 남성 캐릭터: 첫 커스터마이징 단계

첨부한 눈·얼굴형·헤어 레퍼런스의 **번호 1~15를 그대로 선택 번호**로 사용합니다. 기본 남성은 눈 4번(큰 눈), 얼굴형 1번(가름한 형), 헤어 1번(시스루 댄디컷)입니다. 눈/헤어 파일의 `00~14`는 이미지 번호보다 1 작습니다. 피부색은 참고 이미지가 별도로 없어 15단계 팔레트를 사용합니다. 눈 색 8종과 머리색 10종도 선택할 수 있습니다. 남성 선택값은 Unreal 전용 저장 슬롯에 기록하며 여성 커스터마이징은 다음 단계입니다.

```bash
python3 scripts/prepare-unreal-male.py --apk /path/to/01-BigiDragon-0.3.1-Compatible.apk
```

이 명령은 제공된 APK에서 남성 기본 GLB 1개, 눈 GLB 15개, 헤어 GLB 15개를 고정 SHA-256과 메시/모프 구조로 확인하여 **Git에 올라가지 않는** `PrivateAssets/Varco/MaleParts/`에 풉니다. 기본 GLB의 `face_00`~`face_14` 모프가 얼굴형 15종입니다. 사용자 이미지 원본은 `PrivateAssets/ReferenceSheets/`에만 보관합니다. 참고 PNG는 3D 메시가 아닙니다.

Codespace에 검증된 원본 31개를 이미 복사했다면 APK를 다시 올리지 않고 다음 명령을 사용합니다.

```bash
python3 scripts/stage-unreal-male.py
```

이 명령은 **원본을 보존하면서** `PrivateAssets/Varco/UnrealImport/`에 몸 1개, 얼굴 1개, 눈 15개, 헤어 15개, 총 **32개 GLB**를 생성합니다. 모델마다 고정 SHA-256을 먼저 검사하고, 원본 눈·머리의 분리된 조각을 번호별 단일 메시로 결합하며 위치·비균등 크기 조정을 정점/법선에 반영합니다. 기존 출력과 내용이 다르면 덮어쓰지 않습니다. 생성 파일도 공개 Git에 포함되지 않습니다.

Unreal Editor에서 `PrivateAssets/Varco/UnrealImport/*.glb`를 `/Game/Varco/Male/`에 가져와야 런타임에 선택 결과가 보입니다. 아래 이름의 **실제 `.uasset` 32개**가 만들어졌는지 에디터 콘텐츠 브라우저에서 확인합니다.

Editor가 설치된 머신에서는 프로젝트의 Python Editor Script / Editor Scripting Utilities 플러그인을 사용해 **정적 메시 31개와 얼굴 스켈레탈 메시 1개**를 한 번에 가져올 수 있습니다. 엔진 실행 파일과 비공개 GLB가 **같은 머신**에 있을 때 저장소 루트에서 실행합니다.

```bash
/path/to/UnrealEngine/Engine/Binaries/Linux/UnrealEditor-Cmd \
  "$PWD/unreal/Bigimong/Bigimong.uproject" \
  -ExecutePythonScript="$PWD/scripts/import-unreal-male.py"
```

스크립트는 32개 파일과 얼굴 모프를 먼저 확인하고 기존 자산을 덮어쓰지 않습니다. 생성된 자산의 경로·타입을 검사하며 예상과 다르면 오류를 내고 멈춥니다. 얼굴 `SK_MaleFace`는 Interchange에서 스켈레탈 메시로 변환하고 기본 얼굴에 더해 `face_01`~`face_14` 모프 14개를 확인합니다. 갤럭시북의 Unreal 5.8.3에서 실제 가져오기와 재검증을 통과했습니다.

| 소스 | 가져오기 결과의 이름 | 가져오기 조건 |
|---|---|---|
| `UnrealImport/SM_MaleBody.glb` | `SM_MaleBody` | 정적 메시로 가져오기 |
| `UnrealImport/SK_MaleFace.glb` | `SK_MaleFace` | Interchange에서 **Convert Statics with Morph Targets to Skeletals**와 **Import Morph Targets**를 켜고 기본 얼굴과 `face_01`~`face_14` 확인 |
| `UnrealImport/SM_MaleEye_00.glb`~`14.glb` | `SM_MaleEye_00`~`14` | 각 GLB를 정적 메시 한 개로 가져오기 |
| `UnrealImport/SM_MaleHair_00.glb`~`14.glb` | `SM_MaleHair_00`~`14` | 각 GLB를 정적 메시 한 개로 가져오기 |

몸·얼굴·눈·헤어의 원본 좌표를 동일하게 유지해야 조립됩니다. [Epic의 Unreal 5.8 Interchange 가져오기 옵션](https://dev.epicgames.com/documentation/unreal-engine/interchange-import-reference-in-unreal-engine)에서 위 얼굴 변환/모프 설정을 확인할 수 있습니다. **GLB 준비는 `.uasset` 가져오기가 아닙니다.** 가져온 몸·눈·머리의 `BaseColorFactor`를 부위별로 조정하고, `scripts/configure-unreal-male-materials.py`가 얼굴 피부에 같은 매개변수를 가진 Unreal 머티리얼을 연결합니다. 부품이 준비되면 화면의 `항목 바꾸기`·`이전`·`다음`으로 각각 고르고 저장합니다. 눈·얼굴형·헤어 참고 이미지에서 Unreal로 추출한 개별 15장씩과 피부색 견본 15장은 `scripts/render-unreal-reference-options.py`로 만들며, 선택 화면에는 `/Game/Customization/Thumbnails`의 해당 이미지를 표시합니다. 원본 시트와 생성 이미지는 공개 Git에 포함하지 않습니다. 남성 기본 GLB만 약 38만 삼각형이어서 모바일 성능 검증과 최적화가 필요합니다.

### Unreal Android APK 빌드

Unreal Engine 5.8, **Epic Games Launcher에서 추가한 Unreal Android 선택 구성요소**, Android SDK/NDK/JDK, Visual Studio C++ 빌드 도구, `/Game/Varco/Male` 아래 **32개**(몸 1·얼굴 1·눈 15·헤어 15)의 가져온 `.uasset`이 준비된 빌드 머신에서 실행합니다. 갤럭시북에서 `BuildCookRun`을 실행했으나 UnrealBuildTool이 `Missing files required to build Android targets. Enable Android as an optional download component in the Epic Games Launcher.`라고 보고하여 APK가 생성되지 않았습니다.

Android 도구가 아직 설치되지 않은 엔진 머신에서는 먼저 엔진의 Turnkey로 설치합니다.

```bash
/path/to/UnrealEngine/Engine/Build/BatchFiles/RunUAT.sh Turnkey -Command=InstallSDK -platform=Android -SdkType=Full -BestAvailable -Unattended -nocompile -nocompileuat
```

```bash
BIGIMONG_UNREAL_ROOT=/path/to/UnrealEngine bash scripts/build-unreal-android.sh
```

갤럭시북 Windows에서는 저장소 루트에서 `scripts/build-unreal-android.ps1 -OutputDir <출력 폴더>`를 실행합니다. 이 스크립트는 필요한 32개 메시 자산을 먼저 확인하고 Android 패키지를 만든 뒤 APK의 매니페스트·ARM64 라이브러리·ZIP 무결성을 검사합니다.

명령은 `BuildCookRun`으로 Unreal 클라이언트를 Android용으로 빌드·쿠킹·패키징한 뒤 APK 구조/CRC와 ARM64 라이브러리를 검사하여 `build/Android/Bigimong-Unreal-MalePreview-debug.apk` 및 SHA-256 파일을 만듭니다. 엔진이나 자산이 없으면 **APK를 만들지 않고 오류를 표시**합니다. 이는 기존 Android/Unity APK와 다른 `com.bigimong.unrealpreview` 개발용 패키지입니다. 현재 Codespaces 기본 컨테이너에서는 Unreal Engine·Android SDK/NDK와 가져온 비공개 `.uasset`을 제공하지 않으므로 이 명령의 실제 엔진 빌드가 수행되지 않았습니다. 엔진 빌드 후에도 실기기 설치·화면과 Play Protect 판정은 별도 확인이 필요합니다.

## VARCO 원본 모델 연결

저장소 루트에서, 직접 소유한 0.3.1 APK를 지정합니다.

```bash
python3 scripts/prepare-unreal-varco.py --apk /path/to/01-BigiDragon-0.3.1-Compatible.apk
```

이 도구는 APK에 포함된 남녀 `GLB`의 고정 SHA-256과 동봉된 출처 기록, GLB 형식 및 폴리곤을 확인하고 이 프로젝트의 `PrivateAssets/Varco/`에 추출합니다. 원본 모델이나 계정 정보는 공개 저장소에 올리지 않습니다. 명령은 남성 500,000, 여성 25,000삼각형과 리그/애니메이션 없음 상태를 보고합니다. 남성 모델을 모바일 배포에 사용하기 전에 별도 리토폴로지가 필요합니다.

1. 설치된 Unreal Engine 5.8로 `Bigimong.uproject`를 열고 C++ 프로젝트 생성/빌드를 완료합니다.
2. `scripts/import-unreal-varco.py`를 Unreal Editor Python으로 실행해 두 원본 GLB를 `/Game/Varco/SM_VARCO_Male`, `/Game/Varco/SM_VARCO_Female`로 가져옵니다. 원본에는 골격이 없으므로 정적 메시입니다. 갤럭시북의 Unreal 5.8.3에서 두 자산의 생성과 타입을 확인했습니다.
3. 생성된 머티리얼과 텍스처는 `/Game/Varco` 안에 유지합니다. 프로젝트의 패키징 설정은 `/Game/Varco`를 굽도록 지정합니다. 실제 Android APK 안에 메시가 들어갔는지는 엔진 빌드 후 별도 확인해야 합니다.
4. 에디터에서 Play를 실행합니다. 기본 화면은 남성 아바타입니다. 마우스 가로 이동/손가락 가로 드래그로 회전, 마우스 클릭/탭으로 알/아바타 전환, 위로 스와이프하면 여성, 아래로 스와이프하면 남성입니다. 키보드는 `Tab`, `F`, `M`을 사용합니다.
5. 실기기 시험은 Unreal의 Android SDK/NDK와 ARCore 지원 Galaxy 기기에서 별도로 실행합니다. 개발용 패키지 `com.bigimong.unrealpreview`는 기존 앱의 `com.bigimong.app`을 덮어쓰지 않습니다.

모델을 가져오기 전에는 길쭉한 구형 개발용 표시물이 나옵니다. 이 표시물은 아바타 완성 품질을 나타내지 않습니다. 남성 부품을 가져와도 첫 미리보기에는 목걸이, 의상 판매, 완성된 표정 애니메이션, 걸음과 온라인 대전이 아직 없습니다.

## 계속 옮길 코드

| 현행 구현 | Unreal 목표 | 첫 미리보기 |
|---|---|---|
| Unity 홈/부화 상태 v2 | 동일한 30,000점·2시간·30종 선택 및 복구 | 미연결 |
| Android Health Connect/Keystore/BLE | 네이티브 브리지로 유지 후 Unreal 클라이언트에 결합 | 미연결 |
| Node 서버와 SQLite/매칭/정산 | API, 데이터, 서버 판정 유지 | 미연결 |
| Unity AR Foundation 대전 | Unreal Handheld AR/Google ARCore 및 서버 모션 동기화 | 플러그인만 켬 |
| VARCO 원본 및 커스터마이징 | 남성 부품 가져오기 → 외형 편집 → 모바일 최적화·리깅 → 여성 확장 | 남성 선택 코드와 수동 자산 가져오기 절차 |

상세 기준은 [`docs/superpowers/specs/2026-09-26-unreal-migration-design.md`](../../../docs/superpowers/specs/2026-09-26-unreal-migration-design.md)에 있습니다. 기존 Unity 앱과 Kotlin 앱을 제거하거나 기존 사용자 저장 데이터를 새 형식으로 재해석하지 않습니다.

## 이 환경에서 가능한 검사

```bash
python3 -m unittest discover -s tests -p 'test_prepare_unreal_varco.py' -v
BIGIMONG_TEST_APK=/path/to/01-BigiDragon-0.3.1-Compatible.apk python3 -m unittest discover -s tests -p 'test_prepare_unreal_varco.py' -v
BIGIMONG_TEST_APK=/path/to/01-BigiDragon-0.3.1-Compatible.apk python3 -m unittest discover -s tests -p 'test_prepare_unreal_male.py' -v
python3 -m unittest discover -s tests -p 'test_stage_unreal_male.py' -v
python3 -m unittest discover -s tests -p 'test_import_unreal_male.py' -v
python3 -m unittest discover -s tests -p 'test_unreal_android_build.py' -v
npm test
bash scripts/verify-unreal-cloud.sh
```

이 검사는 Unreal 에디터·C++ 컴파일·AR 실기기 실행의 대체 증거가 아닙니다.
