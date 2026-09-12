# 비기몽 서버 API v0.5

## 첫 플레이 상태

서버가 사용자의 첫 플레이 상태를 다음 순서로 저장한다.

| 상태 | 의미 | 걸음 처리 |
|---|---|---|
| `GIFT` | 최초 선물상자 대기 | 동기화하지 않음 |
| `EGG` | 공룡알 육성 | 1걸음당 알 1포인트 |
| `READY_TO_HATCH` | 30,000포인트 완료 | 부화 전까지 추가 보상 없음 |
| `HATCHED` | 공룡 탄생 완료 | 1걸음당 1비기, 하루 최대 10,000 |

기존 v0.4 데이터베이스의 공룡은 마이그레이션 시 `HATCHED` 상태로 보존한다.

## 계정 응답

`POST /api/v1/accounts/guest` 또는 `GET /api/v1/me` 응답에는 `journey`가 포함된다. 부화 전 `dragon`은 `null`이며 상대 매칭도 허용되지 않는다.

```json
{
  "id": "user-id",
  "wallet": 0,
  "journey": {
    "phase": "EGG",
    "giftOpened": true,
    "egg": {
      "id": "egg-id",
      "progress": 12500,
      "required": 30000,
      "lastCleanedAt": null,
      "nextCleanAt": null
    }
  },
  "dragon": null
}
```

## 선물상자와 알

| 메서드 | 경로 | 기능 |
|---|---|---|
| POST | `/api/v1/journey/gift/open` | 최초 상자를 열고 공룡알 지급 |
| POST | `/api/v1/journey/egg/clean` | 알에 500포인트 추가 |
| POST | `/api/v1/journey/egg/hatch` | 준비 완료 알을 랜덤 부화 |

상자는 한 번만 열 수 있다. 알 닦기는 서버 시간 기준 2시간마다 가능하다. 닦기로 30,000포인트가 되면 상태가 `READY_TO_HATCH`로 바뀐다.

## 걸음 동기화

```http
POST /api/v1/steps/sync
Authorization: Bearer <token>
Idempotency-Key: hc:2026-09-11:18000
Content-Type: application/json

{ "day": "2026-09-11", "totalSteps": 18000 }
```

서버는 오늘 마지막 누적 걸음과 새 누적값의 차이만 사용한다. `EGG`에서는 `eggProgressAdded`, `HATCHED`에서는 `rewarded`에 반영된 값이 반환된다. 부화에 필요한 수치를 넘긴 걸음은 비기로 소급 지급하지 않는다.

## 랜덤 부화

`READY_TO_HATCH` 상태에서만 부화할 수 있다. 서버가 30종 공룡 모티브, 6개 눈색, 6개 날개색을 각각 독립적으로 동일 확률 추첨한다. 등급은 없다. 결과에는 종 이름, 고유 기술명, 표시용 이모지와 색상 정보가 포함된다.

## 보안 원칙

- 캐릭터가 사용자를 부르는 `ownerPetName`은 본인 응답에만 포함한다.
- 부화 전에는 대전 대기열 참가를 거부한다.
- 진행 상태와 알 닦기 시간은 클라이언트가 아닌 서버가 확정한다.
- 걸음 누적값 위변조 방지는 운영 전 Play Integrity와 이상 패턴 탐지를 추가해야 한다.

그 밖의 인증·거래 원장·대전 API는 `API_v0.4.md`와 동일하다.
