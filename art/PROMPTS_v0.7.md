# 비기몽 3D 진화 원화 생성 기록 v0.7

## 생성 방식

ChatGPT 내장 이미지 생성의 `stylized-concept` 모드를 사용했다. 사용자가 제공한 3장의 참고 이미지에서 번호별 색상, 공룡 실루엣, 뿔·볏·날개·등판 같은 식별 특징과 3단 진화 비율을 반영했다.

## 공통 최종 프롬프트

> Create one polished 3D character evolution sheet for Bigimong, showing exactly three full-body versions of the same creature from left to right: Baby, Teen, Adult. Preserve the numbered reference character's species, signature colors, silhouette, face, horns, crest, wings, plates and other identifying features. Baby is round, small and cute; Teen is taller and more confident with developing features; Adult is heroic and fully evolved but still friendly. Use a high-quality stylized 3D animated-game collectible look, large expressive eyes, smooth materials, warm off-white studio background, soft ground shadows, even spacing, and no cropping. Do not add text, numbers, arrows, logos, watermark, clothing, hats, glasses, bags or accessories. The three figures must be visually distinct growth stages of one consistent character.

## 번호별 핵심 지시

| 번호 | 핵심 지시 |
|---:|---|
| 01 | 빨간 티라노사우루스, 크림색 배, 성체 꼬리 화염 |
| 02 | 파란 트리케라톱스, 흰 뿔과 큰 프릴 |
| 03 | 주황 익룡, 긴 부리와 큰 주황 날개 |
| 04 | 빨간 육식형, 청년기 작은 날개, 성체 화염 꼬리 |
| 05 | 파란 각룡, 세 개의 뿔과 프릴 |
| 06 | 초록 숲 드래곤, 주황 날개, 성체 화염 꼬리 |
| 07 | 파란 안킬로사우루스, 갑옷 돌기와 꼬리 철퇴 |
| 08 | 초록 숲불 드래곤, 주황 날개와 화염 꼬리 |
| 09 | 빨간 육식형, 청년기 날개, 성체 화염 꼬리 |
| 10 | 붉은 뿔 드래곤, 성체 화염 볏·날개·꼬리 |
| 11 | 빨간 드래곤, 성장할수록 커지는 주황 날개 |
| 12 | 파란 드래곤, 보라 날개막 |
| 13 | 주황 익룡에서 청록 드래곤으로 성장 |
| 14 | 초록 스테고사우루스에서 주황 돛 성체로 성장 |
| 15 | 갈색 중장갑 공룡, 등판과 꼬리 철퇴 |
| 16 | 초록 장경형에서 주황 볏·초록 날개 성체로 성장 |
| 17 | 새형 오비랍토르, 분홍·파란 깃털 |
| 18 | 갈색 프로토케라톱스, 탐험가형 실루엣 |
| 19 | 청록 갈리미무스, 속도형 날렵한 체형 |
| 20 | 보라 드래곤, 파란 갑주와 보라 날개 |
| 21 | 왕관뿔 붉은 화염 드래곤 |
| 22 | 파란 각룡에서 갑주 날개 성체로 성장 |
| 23 | 주황 익룡에서 보라·붉은 드래곤으로 성장 |
| 24 | 초록 스테고형에서 파란 갑주, 보라 돛 성체로 성장 |
| 25 | 참고표 누락으로 임시 보라 테리지노사우루스 |
| 26 | 청록 소형 러너, 붉은 볏과 꼬리 지느러미 |
| 27 | 파란 장경형에서 주황·보라 볏 초식공룡으로 성장 |
| 28 | 주황 러너에서 깃털날개 미크로랍토르로 성장 |
| 29 | 청록 러너에서 초록 볏, 붉은 갑주 성체로 성장 |
| 30 | 갈색 티라노형에서 보라 날개 화염 드래곤으로 성장 |

원본 진화 시트는 `art/evolution_sheets/`에, 앱용 단계별 WebP는 `android/app/src/main/res/drawable-nodpi/`에 저장한다.
