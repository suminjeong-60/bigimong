# 비기몽 AR 구현 기록 v0.11

## 목적

v0.11은 v0.10의 AR 전투 동작을 바꾸지 않고, ARCore 지원 Android 기기에 설치할 수 있는 ARM64 개발용 테스트 APK를 GitHub Actions에서 생성하는 경로를 추가한다.

## 추가된 빌드 경로

- Unity 진입점: `Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk`
- Unity 버전: `6000.0.58f2`
- 애플리케이션 ID: `com.bigimong.app`
- 최소 Android API: 28
- 스크립팅 백엔드: IL2CPP
- ABI: ARM64
- 출력: `build/Android/Bigimong-AR-v0.11-debug.apk`
- GitHub artifact: `Bigimong-AR-v0.11-debug`

빌드 진입점은 기존 `BigimongArSceneBuilder.CreateScene()`을 호출하므로 CI에서도 AR Session, XR Origin, AR Camera, 수평면 탐지, 링, 캐릭터, HUD가 포함된 동일한 장면을 생성한다.

## 기존 설계 유지

이번 artifact는 바닥 인식과 AR 대전 연출을 먼저 확인하는 독립 실행형 Unity 테스트 APK다. 기존 Kotlin·Compose 앱의 계정, 걸음, 알, BLE, 서버 통신 코드와 `Unity as a Library` 연결 경계는 제거하거나 대체하지 않았다. 제품용 통합 APK는 실기기 AR 검증 뒤 Unity Gradle export의 `unityLibrary`를 기존 Android 앱에 포함하는 방식으로 이어간다.

ARCore 미지원 단말과 카메라 권한 거절 시 Compose 2D 대전으로 복귀하는 기존 정책도 그대로 유지한다.

## 비밀정보와 현재 제한

워크플로는 빌드 전에 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`가 GitHub Actions Secrets에 모두 존재하는지 확인한다. 실제 값은 소스나 로그에 기록하지 않는다.

Cloud Anchor ID 교환과 서버 검증 경로는 유지되지만, 실제 Hosting/Resolving은 Google Cloud 프로젝트와 ARCore Extensions 설정 전까지 로컬 바닥 앵커로 작동한다.
