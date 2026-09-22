# AI 인수인계서

2026-09-22: Economy 신규 등록 중단 대응으로 **Economy-free v2** 구현. 학생 설정은 [README.md](README.md) 7~10절 기준.

## 현재 구성

- Unity 프로젝트: `MarketPlaceUGS-main`, 수업 씬: `Assets/Scene/1.unity`, Editor 6000.3.18f1.
- Assets/.meta/씬의 기존 연결을 보존했다. UI의 서버 호출을 교체했으며 씬 자체는 변경하지 않았다.
- Authentication + Cloud Code + Cloud Save Private 상태. 실제 게임 스크립트는 Economy API를 호출하지 않는다. Economy 패키지는 기존 프로젝트 호환을 위해 manifest에 남아 있으나 활성화가 필요하지 않다.
- 저장 위치: `classroom_market` / **Private** / `state`, version 2. 새 환경의 최초 초기값은 `CloudCode/classroom-state.json`.
- 잔액·인벤토리·매물·정산·요청 ID/결제 기록을 하나의 값에 writeLock으로 저장한다. 최대 200계정, JSON 4 MB 미만의 수업용 구조이며 운영 규모로 확장된 구조가 아니다.
- 최초 계정 잔액 1000. Add Coin(+100)/Random Item은 서버 demoEnabled 설정과 계정당 합계 100회 제한. 가챠는 100 COIN.
- 기본 아이템 SWORD/REDPOTION/BLUEPOTION 및 LEGENDARY_SWORD 거래 가능. MYTHIC_SWORD_NFT 거래소 판매/구매 차단, 지갑 전송만 허용.
- 클라이언트는 요청 ID를 PlayerPrefs에 저장하고 성공한 응답을 받은 뒤 삭제. 알 수 없는 오류 뒤에는 동일 인수/요청으로 재시도한다.
- PortfolioMarketDemo와 SimpleMarketApp 모두 새 서버 계약 사용. PriceInput 연결 완료.
- MarketLiveUpdates: Push 수신 시 새로 조회, 5초 폴링으로 누락/재접속 보완. Push 모듈 미배포 시에도 거래 저장 및 자동 갱신 가능.

## 원본 및 배포

| 기능 | 원본 / 생성물 |
|---|---|
| 원자적 거래 저장 | CloudCode/src/cloud-store.js |
| 재화·아이템·결제 지급 어댑터 | CloudCode/src/cloud-adapter.js |
| 판매·구매·취소·정산·가챠·수업 지급 | CloudCode/src/cloud-market.js |
| 기존 검증된 Sepolia 결제 검증 | CloudCode/src/core.js; v2는 grantPayment 경로로 한 번에 지급 |
| JS 배포 | node CloudCode/build.js → CloudCode/deploy/*.js 및 js/Mkt_*.txt |
| NFT 서버 | CloudCode/nft/core.cjs, adapter.cjs → NFTWorkshop에서 npm.cmd run build:sync |
| Push 모듈 | CloudCode/MarketNotifications/Notifications.cs 및 csproj |
| Push 패키지 | pwsh -File CloudCode/MarketNotifications/package.ps1 → MarketNotifications.ccm |

**js/Mkt_*.txt와 CloudCode/deploy/Mkt_*.js는 이제 동일한 생성물이다.** 이전 버전의 '서로 다른 서버이므로 교체하지 말라'는 안내는 폐기했다. 새 클라이언트와 서버는 함께 배포한다.

기본 거래소 JS 8개(Mkt_*), 결제 JS 4개(Gold_*/Sword_*), NFT JS 3개. Mkt_* 쓰기 함수는 request_id 필수. deploy/parameters.json이 기준이다. NotifyChanged는 MarketNotifications C# 모듈에 있으며 JS 함수가 아니다. private 상태 revision을 서버에서 읽어 변경 신호만 방송한다. 알림 제한은 인스턴스별이며 알림이 정산을 수행하지 않는다.

## 기존 데이터 / 결제 / NFT 보존

- **기존 UGS 환경에는 아직 배포하지 않았다.** 실제 적용 프로젝트/환경이 필요하다. 새 수업용 프로젝트/환경 권장.
- 기존 Economy 잔액·인벤토리·Cloud Save market 매물은 자동 이관하지 않는다. 새 v2 상태는 별도의 저장 영역이다.
- 기존 `simple_market/state`와 `market`은 삭제·초기화하지 않는다. 이전 결제 영수증은 MIGRATION_REQUIRED로 읽어 재지급을 막는다. 구버전과 v2를 같은 환경에서 동시 운영하지 않는다.
- 0.0001 Sepolia ETH → 10000 COIN 또는 LEGENDARY_SWORD 1개. 수신자·금액·상품 메모·계정·체인·블록 확인 검증을 유지했다.
- 새 결제의 자산과 GRANTED 영수증은 같은 private 상태에 원자적으로 저장한다. 거래 해시 재전송은 중복 지급하지 않는다.
- 구버전 core.js/adapter.js는 회귀 테스트를 위해 유지. 특정 영수증의 과거 복구 분기를 다른 학생에게 적용하지 않는다.
- 공개 결제 설정은 simple_market/Default/config. NFT 설정/서명 연결은 같은 곳의 nft_config/nft_state를 유지한다. NFT 표시 인벤토리 저장만 v2로 옮겼다.
- NFT 계약, 쿠폰 웹, MetaMask/Reown 연결 구조는 유지. 첫 서명으로 UGS 계정과 지갑 1:1 연결. RPC 실패를 NFT 미보유로 취급하지 않는다.
- 기존 NFT 제한: 지갑당 20개, 동기화 lease 60초, 3 confirmations. 자동 거래소 갱신은 NFT RPC를 반복 호출하지 않으며 수동 Refresh/발행 확인 때 동기화한다.

## 검증 기록

- `node --test --test-isolation=none CloudCode/tests/*.test.js CloudCode/nft/tests.cjs`: **69개 통과**. 동시 구매/취소, 잔액 초과 사용, 중복 정산, 응답 유실 전후, 결제 재전송, NFT 전송/서명 검증 포함.
- Unity 설치본의 Roslyn과 기존 Bee 응답 파일/참조로 변경된 런타임 소스와 새 클라이언트 스크립트 컴파일 성공. 결과는 Temp/MarketValidation.dll. 실제 Editor Play 실습이나 학생 PC 테스트를 대신하지 않는다.
- Push 모듈 Linux 대상 publish 성공, MarketNotifications.ccm 생성. NuGet Core 0.0.7 / Apis 0.0.27.
- UGS 실제 배포, 두 클라이언트의 온라인 거래/Push, 신규 학생 프로젝트에서의 전체 수업 실습은 아직 미검증.
- Git commit/push 하지 않았다. 사용자 파일 변경은 작업 트리에 남아 있다.
