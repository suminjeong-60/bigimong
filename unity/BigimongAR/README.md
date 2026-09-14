# Bigimong AR Unity module

Unity 6.0과 AR Foundation 6.1.1을 사용하는 Android 전용 AR 전투 모듈 골격이다.

## 에디터 설정

1. Unity Hub에서 Unity 6.0과 Android Build Support를 설치한다.
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

저장소의 `Build Bigimong AR debug APK` 워크플로를 수동 실행하면 `Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk`가 AR 장면을 생성하고 `com.bigimong.app` ARM64 개발 APK를 빌드한다. 결과물은 `Bigimong-AR-v0.16-debug` artifact에서 내려받는다.

이 APK는 사용자가 제공한 5장 화면 위에 저장 가능한 오프라인 알 부화·육성·간식·선물·퀘스트·도감과 AR 연습 대전을 얹은 독립 실행형 Unity 테스트 빌드다. 제품 구조인 Kotlin·Compose 앱의 `Unity as a Library` 연결 코드는 그대로 유지되며, 테스트가 끝난 뒤 같은 Unity 장면을 `unityLibrary`로 내보내 결합한다.

Unity Personal 계정은 저장소 Actions Secrets의 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`가 필요하다. 값을 소스나 워크플로 본문에 직접 쓰지 않는다.
