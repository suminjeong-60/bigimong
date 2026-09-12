# 비기몽 Android MVP

## 포함된 연결

- Jetpack Compose 계정 생성·선물상자·알·부화·홈 화면
- 공룡 이름과 비공개 사용자 애칭 분리
- 게스트 토큰을 Android Keystore AES-GCM 키로 암호화 저장
- Health Connect `READ_STEPS` 권한 요청
- 한국 표준시 기준 오늘 걸음 집계
- 부화 전 누적 걸음을 30,000포인트 알 게이지에 반영
- 2시간마다 알 닦기와 500포인트 반영
- 서버에서 30종·눈색·날개색 랜덤 부화
- 부화 후 누적 걸음 동기화 및 비기 잔액 갱신
- 0·10·50·100비기 원격 서버대전 매칭과 취소
- 앱 재진입 시 진행 중인 대전 또는 대기열 복구
- 10초 방향 선택, 미선택 서버 자동 플레이, HP·라운드·정산 결과 화면
- 종·성장 단계·행동별 모션 키 수신과 표시
- 30종 × 3단계 캐릭터 WebP 90개 내장
- 서버 `artId`와 성장 단계에 따라 공개·홈·대전 화면 이미지 자동 변경
- 카메라·Android 12 이상 근거리 기기 권한 분리 요청
- BLE 광고/탐색으로 동일 페어링 코드의 근거리 상대만 서버 매칭
- Unity as a Library 전체화면 AR 전투 진입과 양방향 메시지 계약

## 실행

1. 프로젝트 루트의 서버를 `BIGI_DEV=1 npm run server`로 실행한다.
2. Android Studio에서 이 `android` 폴더를 연다.
3. Gradle JDK를 17로 지정하고 Sync한다.
4. 에뮬레이터에서 앱을 실행한다. 기본 API 주소는 `http://10.0.2.2:4180`이다.
5. 실제 기기에서는 `app/build.gradle.kts`의 `API_BASE_URL`을 개발 PC의 같은 Wi-Fi IP 또는 HTTPS 테스트 서버로 변경한다.
6. `../unity/BigimongAR`을 Unity 6에서 열어 Android Gradle 프로젝트로 Export한 뒤 생성된 `unityLibrary`를 이 프로젝트에 포함한다.

이 저장소에는 실행 환경에서 생성할 수 없는 Gradle Wrapper 바이너리를 넣지 않았다. Android Studio의 내장 Gradle 또는 로컬 Gradle 9.6에서 `gradle wrapper --gradle-version 9.6.0`을 한 번 실행하면 명령행 빌드도 가능하다.

## 운영 전 필수 작업

- `usesCleartextTraffic` 제거 및 HTTPS 고정
- Health Connect 데이터 접근 사유를 Play Console에 신고
- 개인정보처리방침 URL과 앱 내 설명 화면 확정
- Play Integrity 및 이상 걸음 탐지로 위변조 방어
- 백그라운드 동기화가 필요할 경우 별도 권한과 WorkManager 정책 검토
- Cloud Anchor API 키는 앱 소스에 직접 넣지 않고 제한된 Android 키로 설정
