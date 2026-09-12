# 비기몽 AR 전투 구현 기록 v0.9

## 이번 버전에서 실행 가능해진 범위

- Unity 메뉴 `Bigimong > Create AR Battle Scene`으로 테스트 장면 자동 생성
- AR Session, XR Origin, ARCore Camera, 수평면 탐지, 화면 중앙 배치 표시, 대전 링, 입력 HUD 자동 구성
- `CharacterPrefabCatalog`에 최종 모델이 없으면 01~30 절차형 3D 대체 캐릭터 자동 생성
- 아동기·청년기·성인기에 따라 0.3m, 0.9m, 2.1m 높이 자동 보정
- Animator가 없으면 절차형 공격·회피·피격·KO·승리 모션 사용
- Animator가 연결되면 캐릭터별 전용 클립을 우선 재생
- Cloud Anchor 호스트 ID를 서버에 저장하고 상대 전투 스냅샷에 전달

## 임시 3D 캐릭터의 역할

임시 모델은 최종 디자인이 아니다. 카메라 거리, 성체 크기, 링 간격, HP 진행, 공격 타이밍, 기기 발열과 프레임을 최종 모델 제작 전에 검증하기 위한 개발용 대체물이다.

번호별 기본색을 다르게 하고 다음 특징 조합을 적용한다.

- 날개형: 03, 08, 10, 11, 12, 13, 20, 21, 28, 30
- 뿔형: 02, 05, 07, 10, 15, 18, 20, 21, 22, 24
- 등판형: 04, 06, 07, 15, 22, 24
- 긴 목형: 05, 27, 29
- 수중형: 11
- 깃털형: 14, 17, 19, 25, 26, 28

최종 프리팹이 `CharacterPrefabCatalog`에 등록되면 해당 번호·단계만 즉시 최종 모델로 교체되며 나머지는 계속 임시 모델을 사용한다. 따라서 90개를 한꺼번에 완성하지 않고 번호별로 순차 교체할 수 있다.

## Cloud Anchor 서버 흐름

1. 근거리 매칭에서 먼저 대기한 A 사용자가 AR 링을 배치한다.
2. `SharedAnchorProvider.Host` 구현이 Cloud Anchor ID를 생성한다.
3. Unity가 `CLOUD_ANCHOR_HOSTED` 메시지로 Android에 ID를 전달한다.
4. Android가 인증 토큰과 함께 서버에 ID를 등록한다.
5. 서버는 A 사용자만 등록할 수 있도록 검사한다.
6. B 사용자의 다음 전투 스냅샷에 `cloudAnchorId`가 포함된다.
7. Unity의 `SharedAnchorCoordinator`가 ID를 복원한 Pose에 링을 배치한다.

실제 Hosting/Resolving은 Google Cloud 프로젝트와 ARCore Extensions 설정이 필요하므로 `SharedAnchorProvider`의 구체 클래스만 아직 비어 있다. ID 교환과 권한 검증 경로는 완성되어 있다.

## 최종 3D 모델 납품 기준

- Unity 단위 1 = 실제 1m
- Y축 발바닥 기준, Z축 정면
- 각 프리팹에 Animator와 공통 Trigger 9개 제공
- 모바일 권장: 캐릭터당 30k~80k triangles, 재질 1~3개, 2K 이하 텍스처
- 눈과 날개는 색상 변경 가능한 별도 Material Slot
- 의상·안경·머리띠는 본체와 분리된 뼈대 부착 프리팹
- 성인기 두 마리, 링, 파티클을 동시에 표시해 목표 기기에서 30fps 이상 유지
