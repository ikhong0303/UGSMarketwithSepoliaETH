## WebGL + MetaMask 골드 결제 통합

**기존 Scene 1에 WalletPanel을 추가하는 경우: [Scene 1 연결 안내](SCENE_1_WALLET_KO.md)** — 기존 거래소 유지, Editor QR 연결 옵션 포함.

**[프로젝트 열기부터 완료까지: START_HERE_KO.md](START_HERE_KO.md)** 순서대로 진행하세요.

- 테스트 장면: Unity 메뉴 `Simple Market > 1. Create or Open Test Scene`
- WebGL 빌드: `Simple Market > 2. Build WebGL`
- 서버에 등록할 코드: `CloudCode/deploy/*.js` (8개)
- 고정 테스트 상품: `0.0001 Sepolia ETH → 10,000 COIN`
- UGS 초기 골드: Economy `COIN` Initial Balance = `1000`
- NFT 발행·구매는 이번 골드 결제 단계에 포함하지 않습니다.

원본 `Assets/Scene/0.unity`와 `js/*.txt`는 참고용입니다. 신규 테스트는 새 SimpleMarket 장면과 새 Cloud Code를 함께 사용합니다.

검증: 서버 및 브라우저 지갑 모의 테스트를 제공했습니다. 실제 Unity 컴파일은 로컬 Editor 라이선스 미활성화로 수행하지 못했으며, UGS 배포·실제 Sepolia 송금은 본인 계정 설정 후 확인해야 합니다.

---

## 🎨 Item Visual System Update

기존의 하드코딩된 아이템 ID 방식을 제거하고, `ScriptableObject`를 통해 아이템의 ID, 이름, 아이콘을 관리하도록 변경했습니다.

### 📁 주요 파일
* **Script:** `Assets/Scripts/MarketPlace/ItemVisualData.cs`
* **Data:** `Assets/Data/Market/GlobalItemVisuals.asset`

### 🛠 아이템 추가 방법 (How to add new items)
1. 프로젝트 창에서 `GlobalItemVisuals` 데이터 파일을 선택합니다.
2. Inspector 창에서 `Items` 리스트의 `+` 버튼을 누릅니다.
3. 아래 정보를 입력합니다:
   * **ID:** UGS Economy에 등록된 Resource ID (예: `PAINT_RED`)
   * **Item Name:** 게임 내 UI에 표시될 이름 (예: `빨간 물감`)
   * **Price:** 게임 내 UI에 표시될 가격 (예: `Coin: 140`)
   * **Icon:** 사용할 Sprite 이미지를 드래그 앤 드롭
4. 게임을 실행하면 자동으로 인벤토리와 상점에 반영됩니다.

---
