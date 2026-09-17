# AI 인수인계서

2026-09-17 기준. 사용자 환경에서 골드·전설검 결제, NFT 쿠폰 발행, NFT 인벤토리 연동 완료 확인. 학생 설치·실습은 [README.md](README.md) 참고.

## 구조와 유지 사항

- Unity 프로젝트: `MarketPlaceUGS-main`, 실제 씬: `Assets/Scene/1.unity`.
- Windows Editor + Reown QR + MetaMask Sepolia(11155111). PowerShell에서는 `npm.cmd` 사용.
- **Assets/.meta/씬/Packages/ProjectSettings 경로와 사용자 수정사항을 보존할 것.** 캐시·node_modules는 Git 제외, 실행 중 삭제 불필요.
- 일반 아이템·COIN은 UGS Economy, NFT 소유권은 블록체인. 일반 전설검은 NFT가 아님.
- 0.0001 ETH → 10000 COIN 또는 전설검 1개. 기존 결제 기능 유지.

## 수정 원본과 배포 파일

아래 경로는 Unity 프로젝트 기준. Dashboard 환경은 게임과 같은 `production`.

| 기능 | 원본 / Dashboard 복사 파일 |
|---|---|
| 현재 Scene 1 거래소 | `js/Mkt_*.txt` 그대로 사용 |
| 골드·전설검 서버 | `CloudCode/src/` 수정 → CloudCode에서 `node build.js` |
| 골드 배포 | `CloudCode/deploy/Gold_GetQuote.js`, `Gold_Claim.js` |
| 전설검 배포 | `CloudCode/Sword_GetQuote_COPY_ALL.txt`, `Sword_Claim_COPY_ALL.txt` |
| NFT 연결·동기화 | `CloudCode/nft/core.cjs`, `adapter.cjs` 수정 |
| NFT 배포 | NFTWorkshop에서 `npm.cmd run build:sync` → `CloudCode/nft/deploy/*_COPY.txt` |
| NFT 계약·쿠폰 웹 | `NFTWorkshop/contracts/`, `web/`, `scripts/` |
| Unity UI | `Assets/Scripts/SimpleMarket/MythicNftPanel.cs`, `ReownWalletBridge.cs`, `SceneWalletPanel.cs` |
| 인벤토리 표시 | `Assets/Scripts/MarketPlace/PortfolioMarketDemo.cs`, `InventoryRowUI.cs`, `Assets/Data/Market/GlobalItemVisuals.asset` |

**CloudCode/deploy/Mkt_*.js는 별도 SimpleMarket 예제다. 현재 js/ 거래소를 이것으로 교체하지 말 것.**
COPY 파일은 현재 배포에 필요한 생성물이다. 직접 고치지 말고 원본 수정 후 재빌드. NFT 빌드는 Dashboard 호환 ES2020, 줄바꿈, 제어문자 이스케이프 유지(`NFT SYNC COPY V3`).

## NFT 서버 설정·동작

- Economy에 Inventory Item `MYTHIC_SWORD_NFT` 등록 후 Publish.
- Cloud Save Custom ID `simple_market`, Default의 기존 `config`, `state`는 결제용. **초기화 금지.**
- 새 환경만 `nft_config` 추가: `{"contractAddress":"배포한 계약 주소","rpcUrl":"https://ethereum-sepolia-rpc.publicnode.com"}`.
- 새 환경만 `nft_state` 추가: `{"version":1,"sequence":0,"bindings":{},"wallets":{},"challenges":{},"leases":{}}`. 기존 값 덮어쓰기 금지.
- `Nft_GetChallenge`: 필수 String `wallet_address`. `Nft_BindWallet`: 필수 String `signature`. `Nft_SyncInventory`: 파라미터 없음. 각각 동일 이름으로 Save → Publish.
- 처음 NFT지갑연결에서 personal_sign으로 게임 계정과 지갑을 1:1 연결. 결제·가스비 없음. 연결돼 있으면 서명 생략.
- 로그인·왼쪽 Refresh·NFT발행확인에서 서버가 보유 tokenId를 조회해 추가/삭제. 3 confirmations 기준이므로 발행·전송 직후 지연 가능.
- 안정적인 인스턴스 ID로 중복 방지. 조회 실패를 0개로 간주하지 않음. 잠금 오류는 60초 후 재시도, PARTIAL은 다시 동기화.
- 신화검NFT의 Sell은 UI·`js/Mkt_CreateListing`·`js/Mkt_BuyListing`에서 차단. 지갑으로 전송해야 함.
- 지갑당 20 NFT, 인벤토리 조회 1000개까지인 수업용 구현. 장착·전투 검증과 운영용 권한 강화는 미구현. Add Coin/Random Item 직접 쓰기는 교육용.

## 현재 참고 값 — 학생 환경에서는 교체

- 계약: `0xd80054858B4116A15977B9DA5e27C507d1381A6e`.
- 학생 지갑: `0x8395aaBAc6864d7046CD56c6C42EbDbc77ADD1E5`, 관리자/수신 지갑: `0xf2548C84990758432d0Dc9BC5F9aC810fEb491E4`.
- UGS Player ID: `py7ENhQ4WIRk8eeYVZQm3PoWP1Wq`. 새 환경의 설정을 이 값으로 강제하지 말 것.
- 결제 오류에서 새 결제를 유도하지 말고 기존 txHash·서버 지급 기록·인벤토리를 먼저 확인. GRANTING/REVIEW_REQUIRED를 확인 없이 GRANTED로 바꾸거나 재지급하지 말 것.

## 검증·복구

- CloudCode: `npm.cmd test` → 기존 테스트 44개.
- NFTWorkshop: `npm.cmd run test:sync`, `npm.cmd test` → 동기화 10개 + 계약 테스트 1개.
- 합계 55개 통과. Unity 런타임 컴파일도 앞선 구현에서 통과. 실제 서버 배포 확인은 별도로 필요.
- Unity Editor 설치 버전: 6000.3.18f1. NFT 웹 실행: NFTWorkshop에서 `npm.cmd ci` 후 `npm.cmd start`.
- 이전 골드 V3는 `archive/obsolete-code/`에 보관, 배포 금지.
- 로컬 백업은 저장소 밖 `../ProjectBackups/`에 있으며 GitHub ZIP에 포함되지 않음. `BeforeCleanup_20260917_175033`은 변경파일 33개와 해시 기록, `BeforeDocMerge_*`는 통합 전 문서 백업.
- 안내 문서는 이 파일과 최상위 README만 유지. 서드파티 라이선스 문서는 삭제 금지.
