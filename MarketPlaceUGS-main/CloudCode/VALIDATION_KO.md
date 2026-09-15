# 검증 결과 — 2026-09-14

## 2026-09-15 Scene 1 연결 추가

- `SceneWalletPanel` 및 SDK 미설치 상태의 `ReownWalletBridge`: 현재 프로젝트의 실제 Unity 컴파일 응답 파일·의존 DLL과 Unity 6000.3.18f1 C# 컴파일러로 컴파일 통과. 검증 출력은 `Logs/SceneWalletCheck.dll`이며 기존 게임 어셈블리를 덮어쓰지 않았습니다.
- 선택한 WalletPanel을 연결하는 `SceneWalletSetup` Editor 코드도 같은 방식으로 컴파일 통과했습니다.
- QR 활성 분기는 Reown AppKit Unity **1.7.1**의 실제 배포 패키지 소스를 읽고 API를 맞췄습니다. 사용자 프로젝트에 Reown SDK를 설치하거나 `SIMPLE_MARKET_REOWN`을 켜지는 않았습니다. **QR 활성 분기의 실제 SDK 컴파일·휴대폰 연결·서명·결제는 아직 검증하지 않았습니다.**
- Scene 1의 저장하지 않은 UI와 충돌하지 않도록 `.unity` 파일은 직접 편집하지 않았습니다. Unity에서 현재 WalletPanel을 선택한 뒤 새 연결 메뉴를 실행해야 합니다.
- 안내서는 `SCENE_1_WALLET_KO.md`입니다. 기존 거래소의 무료 지급 코드와 Mkt 서버는 이번 연결에서 교체하지 않았습니다.

## 통과

- Node.js 로컬 자동 테스트 **27개 통과** (`node --test --test-isolation=none tests/*.test.js`).
- Dashboard 배포용 JavaScript 8개 생성 및 `node --check` 문법 검사 통과.
- 신규 C# 3개를 Roslyn으로 파싱하여 문법 오류 없음 확인. **Unity 타입 검사·컴파일을 대체하지 않습니다.**
- 공개 RPC `https://ethereum-sepolia-rpc.publicnode.com`에 읽기 전용 `eth_chainId` 요청: `0xaa36a7` 응답 확인.
- 로컬에 설치된 Economy 3.5.3 소스에서 인벤토리 페이지 API를 확인했습니다.
- Cloud Code 2.10.2 소스에서 스크립트 오류 상세가 예외의 `ToString()`에 포함됨을 확인하고 오류 처리를 맞췄습니다.

자동 테스트 범위: 정상 지급, 동시·재시도 중복 지급 방지, 다른 플레이어·금액·수신자·네트워크 거부, 실패한 거래, 미채굴·승인 부족·블록 불일치, 지급 응답 유실, 원장 초기화 누락·한도, 가챠, 구매·취소 경쟁, 판매대금 중복 수령 방지, MetaMask 승인 거절·계정 변경·로컬 결제 기록 복구.

## 아직 확인하지 못한 것

- Unity 실제 컴파일 및 WebGL 빌드: 설치된 Unity 6000.3.18f1이 **No valid Unity Editor license found**로 종료했습니다. 로그는 `Logs/compile.log`입니다.
- 실제 UGS 프로젝트의 Authentication / Economy / Cloud Save / Cloud Code / Access Control 배포 및 연동.
- 브라우저에서 실제 화면 레이아웃·입력·MetaMask 팝업 동작.
- 실제 테스트 ETH 송금 후 UGS 잔액 증가. 수신 지갑과 본인 UGS 설정을 넣은 뒤 안내서 체크리스트로 검증해야 합니다.

테스트의 서버 SDK와 MetaMask provider는 모의 구현입니다. 공식 SDK·Dashboard·실제 브라우저와의 통합 검증이 완료되었다고 해석하면 안 됩니다.

## 기존 프로젝트 수정

원래 `ItemVisualData.cs`의 불필요한 런타임 `NUnit.Framework` 참조를 제거했습니다. CP949로 저장되어 있던 기존 C# 6개 파일을 UTF-8로 변환하여 한글 소스가 깨지지 않도록 했습니다. 기존 장면과 JS 텍스트 원본은 유지했습니다.
