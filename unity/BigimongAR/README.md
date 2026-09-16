# Bigimong AR Unity module

Unity `6000.0.58f2`와 AR Foundation `6.1.1`을 사용하는 Android 전용 AR 전투 및 v0.19 3D 부화·홈 모듈이다.

현재 상태는 **APK 빌드 소스 준비 완료**다. Unity C# 컴파일·에디터 검사·실제 APK 생성·실기기 수용 검사는 미검증이다. Node 소스 계약 검사 통과는 설치 가능한 APK나 모델·시각 품질의 증거가 아니다.

## 에디터 설정

1. Unity Hub에서 고정 버전 `6000.0.58f2`와 Android Build Support(IL2CPP·SDK·NDK·JDK)를 설치한다.
2. 이 폴더를 Unity 프로젝트로 연다.
3. XR Plug-in Management에서 Android ARCore를 활성화한다.
4. Unity 메뉴에서 `Bigimong > Create AR Battle Scene`을 실행한다.
5. 자동 생성된 `Assets/BigimongAR/Scenes/ArBattle.unity`를 열어 카메라·바닥 인식·링·HUD를 확인한다.
6. 아직 최종 모델이 없다면 01~30 절차형 3D 공룡과 대체 모션으로 전체 전투 흐름을 시험한다.
7. 최종 모델이 준비되면 `CharacterPrefabCatalog` 자산을 만들고 artId 01~30의 Baby/Teen/Adult 프리팹 90개를 연결한다.
8. Android Build Settings에서 Export Project를 선택해 내보낸 `unityLibrary` 모듈을 기존 Android 프로젝트에 포함한다.

## 필수 Animator Trigger

`Attack_Left`, `Attack_Center`, `Attack_Right`, `Dodge_Left`, `Dodge_Center`, `Dodge_Right`, `Hit`, `KO`, `Victory`

모든 프리팹의 Trigger 이름은 같고, 연결되는 Animation Clip은 캐릭터 번호와 성장 단계별로 다르게 제작한다.

## Shared AR

`SharedAnchorProvider`는 Cloud Anchor 구현을 주입하는 경계다. ARCore Extensions와 Google Cloud 프로젝트가 준비되기 전에는 각 기기의 로컬 바닥 배치로 작동한다. BLE 좌표값을 그대로 공유해 같은 공간이라고 가정하면 안 된다.

호스트가 만든 Cloud Anchor ID는 Android를 거쳐 서버의 `/api/v1/battles/{battleId}/anchor`에 저장된다. 상대 기기는 전투 스냅샷의 `cloudAnchorId`를 받아 `SharedAnchorCoordinator`에서 복원한다.

링 비주얼은 전투 좌표 루트의 자식으로 생성된다. 링을 납작하게 만드는 스케일이 캐릭터에 상속되지 않으므로 0.3m·0.9m·2.1m 높이가 실제 공간에서 유지된다. 앵커를 다시 잡으면 이전 Host/Resolve 콜백은 무효화되고 호스트가 새 Cloud Anchor ID를 등록할 수 있다.

## 서버 동기화

서버 응답의 `serverNow`와 `deadlineAt` 차이를 Unity의 단조 증가 시간에 연결해 단말 시계 오차의 영향을 받지 않는 10초 카운트다운을 표시한다. 라운드 모션을 먼저 재생한 뒤 최신 스냅샷으로 HP·승패를 보정하므로 재접속이나 폴링 누락 후에도 서버 상태가 최종 기준이다.

## GitHub Actions AR 테스트 APK

원격 push·GitHub Actions 실행·APK 게시·Secrets 변경은 별도 명시적 승인이 필요하다. 이 소스 준비 작업에서는 실행하지 않는다. 기존 `feature/v0.12-offline-beta` push 트리거는 그대로이며 v0.19 자동 트리거는 추가하지 않았다.

승인 후 저장소의 `Build Bigimong AR debug APK` 워크플로를 수동 실행할 수 있다. `Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk`는 장면 생성 전 동작 검사를 실행하고 장면 생성 후 장면 검사를 실행한 다음 `com.bigimong.app`, 버전 `0.19.0`/`19`, minSdk 28의 ARM64 IL2CPP 개발 APK를 빌드한다. `BuildReport.summary.result`가 성공이 아니면 실패 처리한다. 실제 빌드와 APK 검증이 모두 성공해야 `Bigimong-AR-v0.19-debug` artifact를 내려받을 수 있다. 실패 로그 artifact는 `Bigimong-AR-v0.19-build-logs`다.

v0.19 후보는 제공된 알·홈 원화를 기준으로 한 단일 3D Focus Stage, 저장 가능한 30,000포인트 부화, 동일 art ID 복구와 공룡·아바타 전환을 포함한다. 승인된 프리팹이 없으면 실제 3D 절차형 대체 모델을 사용하며, 기존 VARCO `avatar_male`은 새 생성 없이 재사용한다. AR은 오프라인 연습 대전이며 Kotlin·Compose 앱의 `Unity as a Library` 연결 구조는 유지한다.

Unity Personal 계정은 저장소 Actions Secrets의 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`가 필요하다. 값을 소스나 워크플로 본문에 직접 쓰지 않는다.

## 로컬 빌드와 실제 APK 검증

저장소 루트에서 아래 명령을 실행한다. `Unity`는 반드시 `6000.0.58f2` 에디터를 가리켜야 하며 유효한 로컬 라이선스와 Android 도구가 필요하다. 에디터나 라이선스가 없으면 중단하고 해당 blocker를 기록한다. 다른 버전이나 모의 APK로 대체하지 않는다.

```bash
mkdir -p build/Android
Unity -batchmode -quit \
  -projectPath unity/BigimongAR \
  -executeMethod Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk \
  -logFile build/Android/v0.19-unity.log
```

`RenderTexture`, `Camera.Render`, `ReadPixels` 동작 검사가 필수이므로 `-nographics`를 사용하면 안 된다. 빌드 진입점은 Null graphics device 또는 RenderTexture 미지원 환경에서 검사/장면 생성 전에 즉시 실패하고 GPU 정보를 로그에 기록한다. 검사를 생략하여 빌드 성공으로 처리하지 않는다. CI 소스 검사는 Ubuntu, Unity 빌드는 macOS + `enableGpu: true`를 사용한다([GameCI v4 공식 설정](https://game.ci/docs/github/builder/#enablegpu)). 실패 진단은 macOS `~/Library/Logs/Unity/Editor.log`도 수집한다. 워크플로 수정은 원격 실행이나 빌드 성공 증거가 아니다.

종료 코드 0과 실제 `build/Android/Bigimong-AR-v0.19-debug.apk` 생성이 확인된 뒤에만 다음을 실행한다. Android SDK의 `apkanalyzer`와 `apksigner`가 필요하다.

```bash
node scripts/verify-apk.mjs \
  --apk build/Android/Bigimong-AR-v0.19-debug.apk \
  --report build/Android/Bigimong-AR-v0.19-verification.json
sha256sum build/Android/Bigimong-AR-v0.19-debug.apk \
  > build/Android/Bigimong-AR-v0.19-debug.apk.sha256
sha256sum -c build/Android/Bigimong-AR-v0.19-debug.apk.sha256
```

macOS에서는 동일 SHA-256 계산/확인을 `shasum -a 256` / `shasum -a 256 -c`로 실행한다. CI는 macOS용 해시 명령과 명시적인 Java/Android APK 검사 도구 설치를 사용한다.

검증 JSON의 `valid: true`, `com.bigimong.app`, `0.19.0`/`19`, minSdk 28, ARM64 Unity·ARCore 라이브러리, 유효한 서명과 금지 ABI 부재를 확인한다. APK·JSON·SHA-256 파일의 해시가 일치해야 한다. 단위 테스트의 가짜 바이트 fixture는 검증기 로직만 시험하며 실제 APK 검증으로 인정하지 않는다.

## 실기기 수용 게이트 — 아직 미검증

실제 개발 APK로 세로 Android 기기의 알·부화 준비·균열·폭발·공개, 공룡과 아바타 각각의 정면·측면·후면을 캡처한다. 돌봄 500포인트·2시간 쿨다운, 29,999에서 비활성/30,000에서 활성, 자동 부화 없음, `STARTED`·`SHELL_BURST`·`REVEALED`마다 앱 종료 후 동일 ID 복구, 첫 부화 건너뛰기 불가, 아동기·이름·등급 공개를 확인한다. 대상 전환·정면 초기화·오프라인·모델 누락 대체와 16:9·19.5:9·20:9 안전 영역도 확인한다.

minSdk 28 호환 4 GB 기기를 포함한 저·중·고 사양에서 터치 시각 반응 100 ms 미만, 예열된 대상 전환 2초 미만, 60 fps 목표/30 fps 하한, 정상 홈 350 MiB/부화 최대 450 MiB 메모리 상한을 프로파일링한다. 실패는 진단 스냅샷과 함께 기록하고 소스 검사만으로 시각·모델·성능 수용을 표시하지 않는다.
