# 비기몽 서버·Android 대전 계약 v0.6

v0.6은 `API_v0.5.md`의 계정·걸음·부화 계약을 유지하면서 Android 원격 서버대전 흐름을 연결한다.

## 브랜드와 호환성

- 사용자에게 표시되는 앱 이름은 `비기몽`이다.
- Android 배포 ID는 `com.bigimong.app`이다.
- 기존 서버 데이터베이스 파일명과 내부 Kotlin 패키지명은 데이터 이전과 변경 위험을 줄이기 위해 유지한다.

## 원격 대전 흐름

| 순서 | 요청 | Android 처리 |
|---:|---|---|
| 1 | `POST /api/v1/matchmaking` | `REMOTE` 모드와 0·10·50·100 중 배팅액 전송 |
| 2 | `GET /api/v1/matchmaking` | 대기 중 1초마다 조회하고 매칭 시 전투 화면 진입 |
| 3 | `GET /api/v1/battles/{id}` | 진행 중 0.5초마다 라운드·HP·선택 상태 동기화 |
| 4 | `POST /api/v1/battles/{id}/choice` | 10초 안에 `LEFT`, `CENTER`, `RIGHT` 중 하나 제출 |
| 5 | 전투 종료 | 승패, 승자 지급액, 소각액 표시 후 최신 프로필 조회 |

대기 중 취소는 `DELETE /api/v1/matchmaking`을 사용한다. 앱이 다시 열리면 먼저 대기열 상태를 확인해 진행 중 전투 또는 매칭 대기를 복구한다.

## 매칭 응답

대기 중에는 다음과 같이 응답한다.

```json
{
  "status": "QUEUED",
  "queueId": "queue-id",
  "mode": "REMOTE",
  "stake": 50
}
```

상대가 정해지면 `status`는 `MATCHED`이고 `battle` 스냅샷이 포함된다.

## 전투 스냅샷 핵심 필드

```json
{
  "id": "battle-id",
  "status": "ACTIVE",
  "round": 2,
  "attacker": "B",
  "players": {
    "A": { "hp": 3, "dragon": { "name": "몽이", "species": "triceratops", "level": 8, "stage": "GROWTH", "stats": { "attack": 2, "evasion": 1, "vitality": 0 } } },
    "B": { "hp": 4, "dragon": { "name": "별이", "species": "pterosaur", "level": 12, "stage": "YOUTH", "stats": { "attack": 4, "evasion": 3, "vitality": 5 } } }
  },
  "history": [],
  "youAre": "A",
  "deadlineAt": 1789065600000,
  "choiceSubmitted": false,
  "opponentChoiceSubmitted": false
}
```

`deadlineAt`은 서버가 정한 절대 시각이다. 두 참가자 중 한 명이라도 10초 안에 선택하지 않으면 서버가 해당 방향을 동일 확률로 자동 결정한다. 연결이 끊겨도 서버 타이머가 라운드를 진행하며, 재연결한 클라이언트는 다음 조회부터 최신 상태를 받는다.

## 모션 계약

각 라운드 기록에는 서버가 확정한 판정과 모션 키가 포함된다.

```json
{
  "outcome": "HIT",
  "damage": 1,
  "attackAutomatic": false,
  "defendAutomatic": true,
  "motion": {
    "attacker": "triceratops.growth.attack",
    "defender": "pterosaur.youth.hit"
  }
}
```

모션 키 규칙은 `{species}.{stage}.{action}`이다. `stage`는 `growth`, `youth`, `adult`, `action`은 `attack`, `dodge`, `hit`, `win`, `lose` 중 하나다. 이 구조로 30종과 3단계 외형의 이미지·애니메이션을 서버 규칙 변경 없이 교체할 수 있다.

## 정산과 제한

- 원격 대전 방은 0·10·50·100비기를 제공한다.
- 0비기 연습전은 유료 대전 일일 20회 제한에 포함하지 않는다.
- 성장 단계가 달라도 모든 방에 참가할 수 있다.
- 양쪽 배팅액 합계의 20%를 소각하고 나머지를 승자에게 지급한다.
- HP는 체력 스탯에 따라 3~5이며, 강타는 2피해를 줄 수 있다.
