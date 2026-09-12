# 비기몽 서버 API v0.4

## 신뢰 경계

클라이언트는 계정 정보, Health Connect에서 읽은 오늘 누적 걸음, 매칭 요청, 전투 방향만 전달한다. 서버가 누적 걸음 증가분, 일일 보상 상한, 비기 잔액, 배팅 예치, 시간 초과, 피해, 승패, 소각 및 지급을 확정한다. `ownerPetName`은 본인 조회에서만 제공하고 대전 상태에는 넣지 않는다.

현재는 단일 서버 MVP다. Health Connect 누적값은 단순 클라이언트 보고이므로 운영 전 Play Integrity, 재생 공격 방지, 이상 걸음 탐지와 HTTPS가 필요하다.

## 인증

`POST /api/v1/accounts/guest`에서 받은 토큰을 이후 요청의 헤더에 넣는다.

```http
Authorization: Bearer <token>
```

Android 앱은 이 토큰을 Android Keystore의 AES-GCM 키로 암호화해 저장한다.

## 엔드포인트

| 메서드 | 경로 | 설명 |
|---|---|---|
| GET | `/api/v1/health` | 서버 상태 |
| POST | `/api/v1/accounts/guest` | 게스트 계정 생성 |
| GET | `/api/v1/me` | 내 공룡과 비기 잔액 |
| POST | `/api/v1/steps/sync` | Health Connect 오늘 누적 걸음 동기화 |
| POST | `/api/v1/dev/steps` | 개발 모드 전용 임의 걸음 지급 |
| GET | `/api/v1/wallet/transactions` | 비기 거래 원장 |
| POST | `/api/v1/matchmaking` | 대기열 참가 또는 매칭 |
| GET | `/api/v1/matchmaking` | 내 매칭 상태 |
| DELETE | `/api/v1/matchmaking` | 대기 취소 |
| GET | `/api/v1/battles/{id}` | 참가자 대전 상태 |
| POST | `/api/v1/battles/{id}/choice` | 방향 제출 |
| GET | `/api/v1/battles/{id}/events` | 대전 SSE 이벤트 |

## 계정 생성

```json
{
  "dragonName": "루미",
  "ownerPetName": "별님",
  "species": "tyrannosaurus"
}
```

두 이름은 각각 1~12자다. `dragonName`은 공개 공룡 이름이고 `ownerPetName`은 공룡이 사용자를 부르는 비공개 애칭이다.

## Health Connect 걸음 동기화

```http
POST /api/v1/steps/sync
Authorization: Bearer <token>
Idempotency-Key: hc:2026-09-11:4321
Content-Type: application/json

{
  "day": "2026-09-11",
  "totalSteps": 4321
}
```

- `day`는 서버가 계산한 오늘의 한국 표준시 날짜와 같아야 한다.
- Android는 `StepsRecord.COUNT_TOTAL` 집계값을 보낸다.
- 서버는 같은 날 마지막으로 수락한 누적값과 비교해 증가분만 지급한다.
- 누적값이 감소하거나 같은 요청이 재전송되면 추가 지급하지 않는다.
- 하루 지급 상한은 10,000비기다.
- 모든 실제 지급은 `HEALTH_CONNECT_STEP_REWARD` 거래로 기록된다.

예: 1,000보 동기화 후 1,500보를 동기화하면 두 번째 지급은 500비기다.

## 매칭 규칙

| 모드 | 허용 배팅 | 일일 제한 |
|---|---:|---:|
| `REMOTE` | 0~100비기 | 유료 대전 20회 |
| `NEARBY` | 1,000비기 고정 | 현재 없음 |

성장기·청년기·성인기는 서로 친선전과 배팅 대전을 할 수 있다. 양쪽 배팅액은 매칭 순간 예치되고, 총액의 20%를 소각한 나머지를 승자에게 지급한다.

## 방향과 시간 초과

방향은 `LEFT`, `CENTER`, `RIGHT` 중 하나다. `deadlineAt`까지 제출하지 못하면 서버가 균등 무작위로 선택한다. 상대의 실제 선택값은 라운드 판정 전 공개하지 않는다. 재접속한 사용자는 다음 라운드부터 직접 선택할 수 있다.

## 실행

```bash
npm test
BIGI_DEV=1 npm run server
```

기본 포트는 4180이며 `PORT`, `BIGI_DB_PATH`로 변경할 수 있다.
