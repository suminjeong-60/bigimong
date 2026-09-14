# 비기몽 제작 기반 v0.14

이전 AR 설계와 검증 범위: [참고 디자인·AR 구성](docs/AR_IMPLEMENTATION_v0.13.md).

비기몽의 성장·재화·전투 규칙을 실제 코드로 검증하기 위한 프로젝트입니다.

## 현재 구현

- 레벨 1~30 및 아동기·청년기·성인기 구분
- 밥 주기, 경험치, 레벨업, 랜덤 능력치 부여
- 공격·회피·체력 능력치 상한
- 성장 단계가 다른 공룡끼리의 대전 허용
- 근거리 대전 최대 1,000비기
- 원격 서버대전 최대 100비기, 하루 20회 제한
- 10초 미선택 또는 연결 끊김 시 서버 자동 선택
- 방향 공격, 자동 회피, 강타, 체력 판정
- 참가비 20% 소각 및 승자 정산
- 브라우저에서 조작할 수 있는 전투 실험판
- 공룡 이름·사용자 애칭 온보딩
- 최초 선물상자와 30,000포인트 알 부화
- 30종 균등 랜덤 부화 및 눈·날개 색상 랜덤 생성
- 부화 후 홈에서 걷기·비기 획득·밥·레벨업 체험
- 브라우저 저장을 이용한 진행 상태 유지
- 게스트 계정과 Bearer 토큰 인증
- SQLite 영구 저장 및 중복 지급 방지 거래 원장
- 원격·근거리 모드별 서버 매칭 대기열
- 배팅 비기 선예치, 승자 정산, 20% 소각
- 10초 서버 타이머와 미선택 자동 플레이
- 인증된 전투 조회 및 실시간 SSE 이벤트 스트림
- Kotlin·Jetpack Compose Android 앱 골격
- Android Keystore 기반 로그인 토큰 암호화 저장
- Health Connect 오늘 누적 걸음 집계
- 누적값 비교 방식의 걸음 중복 지급 방지 API
- Android에서 0·10·50·100비기 원격 대전 매칭 연결
- Android 대전 상태 자동 복구와 매칭 1초·전투 0.5초 폴링
- Android 10초 카운트다운, 좌·중앙·우 선택, HP·라운드·결과 화면
- 서버 자동 플레이 여부와 캐릭터·성장 단계별 모션 키 표시
- 참고 원화를 바탕으로 만든 30종 × 아동기·청년기·성인기 3D 콘셉트 이미지 90개
- 서버의 `artId`와 성장 단계에 따른 Android 캐릭터 자동 전환
- Unity 6 + AR Foundation 기반 Android 전체화면 AR 전투 모듈 골격
- 바닥 인식·AR 대전 링 배치·트래킹 유실 안내·앵커 재설정
- 아동기 0.3m·청년기 0.9m·성인기 2.1m 실제 크기 적용
- 30종·3단계별 좌/중앙/우 공격·회피·피격·KO·승리 Animator 트리거
- BLE 근거리 상대 탐색과 1,000비기 페어링 코드 매칭
- Unity 방향 입력→Android→서버 및 서버 판정→Unity 모션 동기화 계약
- 최종 3D 모델이 없어도 01~30번을 시험하는 절차형 3D 공룡 생성기
- Animator가 없을 때 종·단계에 따라 달라지는 공격·회피·피격·KO·승리 대체 모션
- Unity 메뉴에서 AR Session·XR Origin·바닥 인식·링·HUD 장면을 자동 생성하는 설치 도구
- 호스트 Cloud Anchor ID 등록과 상대 기기 수신·복원 서버 계약
- 링 비주얼과 전투 좌표 루트를 분리해 캐릭터 실제 미터 크기 보존
- 성장 단계에 따른 링 직경 자동 조절
- 서버 시각 보정 기반 10초 AR 카운트다운
- 재접속 스냅샷 HP·승패 복구와 라운드 연출 순서 보장
- AR HUD의 양쪽 HP·라운드 결과·서버 자동선택 표시
- 앵커 재설정 시 이전 비동기 Host/Resolve 결과 무효화
- GitHub Actions에서 Unity 6 ARM64 개발용 AR 테스트 APK 생성
- Unity 라이선스 값 노출 방지와 빌드 전 Secrets 누락 검사
- 서버에 저장되는 선물상자·알·부화 진행 상태
- 부화 전 걸음은 알 게이지, 부화 후 걸음은 비기로 자동 분리
- 알 닦기 2시간 제한 및 1회 500포인트
- 30,000포인트 달성 후 30종·눈색·날개색 서버 랜덤 부화
- 선물상자·알 게이지·부화 준비·캐릭터 공개 Android 화면
- 부화 전 대전 참가 차단
- v0.4 기존 사용자 데이터 자동 이전

## 실행

Node.js 18 이상이 필요합니다.

```bash
npm test
npm start
```

그다음 브라우저에서 아래 주소를 엽니다.

- 첫 플레이 흐름: `http://localhost:4173/prototype/journey.html`
- 서버전투 실험판: `http://localhost:4173/prototype/`

백엔드 API는 별도 터미널에서 실행합니다.

```bash
BIGI_DEV=1 npm run server
```

- API 상태 확인: `http://localhost:4180/api/v1/health`
- 개발 실행의 데이터베이스: `data/bigi-dragon.sqlite`
- `BIGI_DEV=1`은 걸음 센서가 연결되기 전 테스트용 걸음 지급 API를 활성화합니다. 운영에서는 사용하지 않습니다.

Android 앱은 Android Studio에서 `android` 폴더를 엽니다. 에뮬레이터는 기본적으로 `http://10.0.2.2:4180`의 로컬 서버에 연결됩니다. 자세한 내용은 `android/README.md`를 확인하세요.

## AR 테스트 APK 만들기

`.github/workflows/build-ar-debug-apk.yml`은 실제 ARCore 기기 검증을 위한 독립 실행형 Unity 개발 APK를 만듭니다. 기존 Kotlin·Compose 앱과 Unity as a Library 결합 구조는 변경하지 않으며, 이 APK는 바닥 인식·링 배치·전투 연출을 먼저 확인하는 테스트 빌드입니다.

1. GitHub 저장소의 `Settings > Secrets and variables > Actions`에 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`를 등록합니다.
2. `Actions > Build Bigimong AR debug APK > Run workflow`를 실행합니다.
3. 성공한 실행의 `Artifacts`에서 `Bigimong-AR-v0.14-debug`를 내려받습니다.
4. 압축을 풀어 `Bigimong-AR-v0.14-debug.apk`를 ARCore 지원 Android 기기에 설치합니다.

성공 artifact에는 다음 세 파일이 있어야 합니다.

- `Bigimong-AR-v0.14-debug.apk`
- `Bigimong-AR-v0.14-verification.json`
- `Bigimong-AR-v0.14-debug.apk.sha256`

JSON의 `valid`가 `true`인지 확인하고, 내려받은 APK의 SHA-256이 `.sha256` 파일 및 JSON의 `sha256` 값과 같은지 확인합니다. 실제 GitHub Actions 성공과 실기기 실행 전에는 이 저장소 상태를 “APK 빌드 소스 준비 완료”로만 판단합니다.

Unity 계정 비밀번호와 라이선스 본문은 저장소 파일, 이슈, 로그에 입력하지 않습니다. GitHub Actions Secrets에만 등록합니다.

## 다음 제작 단계

1. Google Cloud 프로젝트와 ARCore Extensions를 연결해 `SharedAnchorProvider` 완성
2. 90개 리깅 3D 프리팹과 전용 Animator Controller로 임시 모델 교체
3. AR 그림자·광원 추정·소환·데미지·KO 파티클 제작
4. 눈·날개 랜덤 색상용 머티리얼 슬롯과 코스튬 레이어 연결
5. 실기기 2대로 BLE→Cloud Anchor→5라운드→정산 통합 테스트
6. Play Integrity·이상 걸음 탐지·HTTPS·WebSocket 적용

게임 규칙은 `docs/GAME_SPEC_v0.4.md`, 캐릭터 번호는 `docs/CHARACTER_CATALOG_v0.7.md`, AR 기본 계약은 `docs/AR_BATTLE_SPEC_v0.8.md`, 최신 AR 구현 내용은 `docs/AR_IMPLEMENTATION_v0.13.md`를 확인하세요.
