# 비기몽 서버 API v0.3

## 목적과 신뢰 경계

클라이언트는 선택 의도만 서버에 전달한다. 비기 잔액, 배팅 예치, 방향 시간 초과, 능력치 확률, 피해, 승패, 소각 및 지급 결과는 서버가 확정한다. 상대방에게는 공룡 이름과 전투 정보만 공개하고 `ownerPetName`은 공개하지 않는다.

현재 구현은 로컬 MVP용 단일 Node.js 프로세스와 SQLite를 사용한다. 운영 전에는 걸음 검증, 토큰 갱신·폐기, TLS, 요청 제한, 부정행위 탐지, 다중 서버용 Redis 매칭 및 복구 작업이 추가로 필요하다.

## 인증

`POST /api/v1/accounts/guest` 응답의 토큰은 생성 시 한 번만 반환된다. 이후 요청에는 다음 헤더를 사용한다.

```http
Authorization: Bearer <token>
```

운영 모바일 앱에서는 토큰을 Android Keystore에 저장한다. 전투 SSE도 동일한 Authorization 헤더가 필요하다.

## 엔드포인트

| 메서드 | 경로 | 설명 |
|---|---|---|
| GET | `/api/v1/health` | 서버 상태 |
| POST | `/api/v1/accounts/guest` | 게스트 계정과 공룡 생성 |
| GET | `/api/v1/me` | 내 공룡과 비기 잔액 |
| POST | `/api/v1/dev/steps` | 개발 전용 검증 걸음 보상 |
| GET | `/api/v1/wallet/transactions` | 비기 거래 원장 |
| POST | `/api/v1/matchmaking` | 대기열 참가 또는 즉시 매칭 |
| GET | `/api/v1/matchmaking` | 내 대기·대전 상태 |
| DELETE | `/api/v1/matchmaking` | 대기 취소 |
| GET | `/api/v1/battles/{id}` | 인증된 참가자의 대전 상태 조회 |
| POST | `/api/v1/battles/{id}/choice` | 현재 라운드 방향 제출 |
| GET | `/api/v1/battles/{id}/events` | 라운드 결과 SSE 스트림 |

## 계정 생성

```http
POST /api/v1/accounts/guest
Content-Type: application/json

{
  "dragonName": "루미",
  "ownerPetName": "별님",
  "species": "tyrannosaurus"
}
```

- 두 이름은 각각 공백 제거 후 1~12자다.
- `dragonName`은 공개 가능한 캐릭터 이름이다.
- `ownerPetName`은 공룡이 사용자를 부르는 비공개 애칭이다.
- 개발 모드에서만 테스트용 `developmentStats`를 받을 수 있다.

## 개발용 걸음 보상

```http
POST /api/v1/dev/steps
Authorization: Bearer <token>
Idempotency-Key: health-record-batch-20260911-001
Content-Type: application/json

{ "steps": 1500 }
```

같은 사용자와 같은 `Idempotency-Key` 조합은 한 번만 지급된다. 한국 표준시 날짜 기준 하루 최대 10,000비기다. 이 경로는 `BIGI_DEV=1`일 때만 열리며 Health Connect 연동 후에는 서명·검증된 걸음 수집 경로로 교체한다.

## 매칭

```http
POST /api/v1/matchmaking
Authorization: Bearer <token>
Content-Type: application/json

{ "mode": "REMOTE", "stake": 100 }
```

| 모드 | 허용 배팅 | 카운트 |
|---|---:|---|
| `REMOTE` | 0~100비기 | 1비기 이상 대전은 하루 20회 |
| `NEARBY` | 1,000비기 고정 | 현재 일일 제한 없음 |

대기 중 같은 모드·같은 배팅액의 사용자가 있으면 매칭한다. 성장기·청년기·성인기 조합에는 제한이 없다. 매칭 순간 양쪽 배팅액을 거래 원장에 기록하며 예치한다.

## 방향 제출과 자동 플레이

```http
POST /api/v1/battles/6b5.../choice
Authorization: Bearer <token>
Content-Type: application/json

{ "direction": "LEFT" }
```

방향은 `LEFT`, `CENTER`, `RIGHT` 중 하나다. 응답의 `deadlineAt`까지 제출해야 하며, 제출하지 않은 쪽은 서버가 균등 무작위 방향을 선택한다. 양쪽 선택값은 라운드가 해결되기 전 공개하지 않고 제출 여부만 표시한다. 연결이 복구되면 다음 라운드부터 다시 직접 선택할 수 있다.

라운드 결과에는 다음 모션 키가 포함된다.

```json
{
  "motion": {
    "attacker": "tyrannosaurus.growth.attack",
    "defender": "triceratops.adult.hit"
  }
}
```

이 키로 30종 × 3단계의 고유 공격·회피 애니메이션을 모바일 클라이언트가 선택한다.

## 정산

양쪽이 100비기씩 예치한 경우 총 200비기 중 40비기를 소각하고 승자에게 160비기를 지급한다. 예치와 지급은 각각 고유한 멱등 키를 사용하므로 서버 재시도 시 중복 차감·지급되지 않는다.

## 로컬 실행

```bash
npm test
BIGI_DEV=1 npm run server
```

기본 주소는 `http://localhost:4180`이며 `PORT`와 `BIGI_DB_PATH` 환경 변수로 변경할 수 있다.
