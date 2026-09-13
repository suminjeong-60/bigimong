# Bigimong AR 구현 기록 v0.12

## 상태

APK 빌드 소스 준비 완료. Unity C# 컴파일과 실제 APK 생성은 GitHub Actions 실행 전까지 미검증이다.

## v0.12 변경

- Unity·Android·Node 버전을 `0.12.0`/`12`로 정렬
- `scripts/verify-apk.mjs`에서 패키지명, 버전, minSdk 28, ARM64 전용 여부 검증
- APK SHA-256 및 JSON 검증 보고서 생성
- 검증 통과 후에만 설치 artifact 업로드
- 빌드 실패 시 비민감 진단 로그 artifact 업로드

## 유지된 설계

- Unity `6000.0.58f2`, AR Foundation/ARCore `6.1.1`
- package ID `com.bigimong.app`, IL2CPP, ARM64, portrait
- 기존 Android·서버·Unity AR 전투 계약과 서버 권위 판정

## 실제 APK 완료 조건

1. `suminjeong-60/bigimong`에 소스 push
2. `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` Actions Secrets 등록
3. `Build Bigimong AR debug APK` workflow 성공
4. JSON `valid: true` 및 SHA-256 일치 확인
5. ARCore 지원 Android 기기 설치·실행 확인

실제 APK와 실기기 증거가 없으면 제품 또는 APK 완성을 주장하지 않는다.
