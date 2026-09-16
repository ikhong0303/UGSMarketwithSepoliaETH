# 전설검 구매 설정 — 지금 직접 할 순서

작성: 2026-09-16. 이번 단계는 **일반 아이템 전설검 구매**입니다. 기존 10,000골드 구매는 유지합니다. 신화검NFT·쿠폰·지갑 소유권 동기화는 다음 단계이며 아직 구현하지 않았습니다.

## 1. 준비된 것과 직접 할 것

| 구분 | 내용 |
|---|---|
| Codex가 준비한 코드 | 전설검 견적, 송금, 서버 검증, Economy 인벤토리 1개 지급, 별도 결제 확인 |
| 결제 가격 | 0.0001 Sepolia ETH + 가스비. 기존 골드 상품과 같은 가격으로 준비 |
| 지급 아이템 ID | `LEGENDARY_SWORD` |
| 사용자가 할 일 | Economy 아이템 등록·Publish, Cloud Code 두 개 등록·Publish, Scene 1 버튼 두 개 배치·연결, 실제 결제 테스트 |
| 검증 범위 | 서버 모의 테스트 및 로컬 C# 컴파일. 실제 UGS 배포·휴대폰 결제는 아직 검증하지 않음 |

현재 프로젝트 위치는 `C:/Users/PC/Desktop/blockChianMarket/UGSMarketwithSepoliaETH/MarketPlaceUGS-main`입니다. 예전 경로의 파일 대신 이 폴더를 사용하세요.

## 2. Unity Play를 종료하고 Economy에 전설검 등록

1. Unity 위쪽 Play 버튼을 눌러 실행을 종료합니다. 코드 컴파일이 끝날 때까지 기다립니다.
2. Unity Dashboard에서 지금 사용 중인 `multiFpsSample` 프로젝트를 선택합니다.
3. 왼쪽 **Economy → Configuration**으로 이동합니다.
4. 환경이 **production**인지 확인합니다. 게임이 다른 환경을 사용하도록 바꿨다면 모든 설정을 그 환경에 맞춰야 합니다.
5. **Add Resource**를 누르고 유형을 **Inventory Item**으로 선택합니다.
6. 이름에는 `전설검`, ID에는 **`LEGENDARY_SWORD`**를 입력합니다. 이름으로부터 다른 ID가 자동 생성되면 직접 고칩니다.
7. Custom Data는 이번에는 입력할 필요 없습니다. 저장합니다.
8. 오른쪽 위 **Publish**를 누르고 확인 창까지 완료합니다.
9. 목록에 ID `LEGENDARY_SWORD`, 유형 `Inventory Item`이 보이는지 확인합니다.

이미 같은 ID가 있다면 새로 만들지 말고 유형과 Publish 여부를 확인합니다. 기존 `COIN`, 검, 포션은 그대로 둡니다.

## 3. Cloud Code에 신규 함수 두 개 등록

이번에는 아래 **두 개만** 신규 등록합니다. 기존 `Gold_*` 및 `Mkt_*`는 교체하지 않아도 됩니다. 특히 `CloudCode/deploy` 안의 모든 파일을 한꺼번에 올리지 마세요. 기존 거래소의 `js/Mkt_*`와 별도 구현이 함께 들어 있습니다.

### 3-1. Sword_GetQuote

1. Dashboard 왼쪽 **Cloud Code → JS 스크립트**로 이동합니다.
2. 스크립트 생성 기능을 누릅니다. 생성 버튼의 번역은 화면에 따라 다를 수 있습니다.
3. 이름을 정확히 **`Sword_GetQuote`**로 입력하고 JavaScript 스크립트를 만듭니다.
4. PC에서 프로젝트의 `CloudCode/Sword_GetQuote_COPY_ALL.txt`를 메모장이나 코드 편집기로 엽니다.
5. 파일 안에서 **Ctrl+A → Ctrl+C**로 전체를 복사합니다.
6. Dashboard의 코드 입력 영역을 한 번 클릭한 뒤 **Ctrl+A → Ctrl+V**로 기본 코드를 전부 교체합니다. 기존 코드 아래에 덧붙이지 않습니다.
7. 첫 줄에 `SWORD_SHOP_FULL_20260916_V1`이 있는지 확인합니다.
8. 이 함수는 Parameters가 **없습니다**.
9. **Save Script → Publish Version** 순서로 누릅니다. Live 버전이 생겼는지 확인합니다.

### 3-2. Sword_Claim

1. 다시 JS 스크립트 목록으로 돌아가 새 스크립트를 만듭니다.
2. 이름은 정확히 **`Sword_Claim`**입니다.
3. `CloudCode/Sword_Claim_COPY_ALL.txt`를 열어 전체 복사합니다.
4. Dashboard 코드 영역 전체를 교체합니다.
5. Parameters 설정에서 다음 항목 하나를 등록합니다. 편집 위치가 별도 **Details** 탭이면 그 탭에서 등록합니다.

| 항목 | 값 |
|---|---|
| Name | `tx_hash` |
| Type | `String` |
| Required | 켜기 |

6. **Save Script → Publish Version**을 완료합니다.
7. 실행 화면의 Parameters에 `tx_hash*`가 보이는지 확인합니다. 여기서 임의의 해시를 넣어 Run할 필요는 없습니다. 게임이 로그인한 플레이어 정보와 해시를 전달합니다.

`No changes to be published`는 저장된 내용과 배포본이 같다는 뜻입니다. Live 코드 첫 줄의 버전 표식과 함수 이름을 확인하세요. Working Copy만 저장하고 Publish하지 않으면 게임에서 새 코드를 사용하지 못합니다.

## 4. Cloud Save 설정은 기존 값 사용

`Cloud Save → 게임 데이터 → simple_market → Default`의 기존 `config`, `state`를 그대로 사용합니다.

- 수신 지갑: 기존 판매자 지갑
- 금액: 기존 `priceWei = "100000000000000"`
- 승인 수: 기존 `confirmations = 3`
- `state`에 전설검 기록은 서버가 추가합니다. 초기 JSON으로 덮어쓰지 않습니다.

가격 변경은 이번 설정 범위가 아닙니다. 현재 클라이언트와 서버 모두 0.0001 ETH를 검증하므로 config만 다른 가격으로 바꾸면 오류가 납니다.

## 5. Scene 1에 버튼 두 개 배치

1. Unity에서 기존 **Scene 1**을 열고 Play가 꺼져 있는지 확인합니다.
2. Hierarchy에서 **Canvas → WalletPanel**을 펼칩니다.
3. 기존 `Buy10000GoldButton`을 선택하고 **Ctrl+D**로 복제합니다.
4. 복제한 오브젝트의 이름을 **`BuyLegendarySwordButton`**으로 바꿉니다. 자식 Text가 아닌 버튼 오브젝트 이름입니다.
5. 그 아래 자식 **Text (TMP)**를 선택하고 Inspector의 Text Input을 `전설검 구매 (0.0001 ETH)`로 바꿉니다.
6. 새 버튼의 Rect Transform 위치를 옮겨 기존 골드 구매 버튼 아래에 놓습니다. WalletPanel이 좁으면 높이를 늘립니다.
7. 기존 `PaymentCheckButton`을 선택하고 **Ctrl+D**로 복제합니다.
8. 복제한 버튼 오브젝트 이름을 **`SwordPaymentCheckButton`**으로 바꿉니다.
9. 그 자식 Text (TMP)의 글자를 `전설검 결제 확인`으로 바꿉니다. 새 전설검 구매 버튼 아래에 놓습니다.
10. 기존 결제 확인 버튼의 글자는 구분하기 쉽게 `골드 결제 확인`으로 바꿔도 됩니다. 오브젝트 이름 `PaymentCheckButton`은 유지합니다.
11. 두 새 버튼의 Button 컴포넌트 **On Click() 목록은 비워 둡니다**. 복제 시 기존 수동 연결이 따라왔다면 새 버튼에서 제거합니다. 코드가 자동으로 연결합니다.
12. Hierarchy에서 **WalletPanel 자체**를 선택합니다.
13. 상단 **Simple Market → Scene 1 - Connect Selected WalletPanel**을 누릅니다.
14. Inspector의 **Scene Wallet Panel**에서 아래 연결을 확인합니다.

| Inspector 필드 | 들어갈 오브젝트 |
|---|---|
| Buy Legendary Sword Button | BuyLegendarySwordButton |
| Sword Payment Check Button | SwordPaymentCheckButton |
| Buy 10000 Gold Button | 기존 Buy10000GoldButton |
| Payment Check Button | 기존 PaymentCheckButton |
| Market Demo | 기존 PortfolioMarketDemo가 붙은 오브젝트 |

15. 새 필드가 비어 있다면 Hierarchy의 해당 버튼을 Inspector 필드에 드래그합니다.
16. **Ctrl+S**로 Scene을 저장합니다.

Scene 파일은 자동으로 바꾸지 않았습니다. 사용자가 만들어 둔 배치에 위 버튼 두 개를 추가하면 됩니다. 지갑 주소·ETH 잔액·상태 문구는 기존 표시를 같이 사용합니다.

## 6. 전설검 이미지

`Assets/Data/Market/GlobalItemVisuals.asset`에 `LEGENDARY_SWORD` 표시 정보를 추가했습니다. 초기 이미지는 기존 검 이미지를 재사용합니다.

다른 그림을 쓰려면 Project에서 이 파일을 선택한 뒤 ID가 `LEGENDARY_SWORD`인 항목의 Sprite를 바꿉니다. 같은 ID 항목을 또 추가하지 않습니다. 표시 이름은 `전설검`입니다. 해당 항목의 기본 가격 1000은 **게임 거래소 COIN 판매 가격**이며 Sepolia 결제 가격이 아닙니다.

기존 Random Item 버튼은 기본 검·빨간 포션·파란 포션만 뽑도록 제한했습니다. 아이콘 목록에 전설검이 생겼다는 이유로 무료 랜덤 지급되지 않습니다.

## 7. 실제 테스트 순서

1. Play → 게임 로그인 → 지갑 연결을 합니다.
2. 기존 COIN 잔액과 전설검 개수를 기록합니다.
3. **전설검 구매**를 한 번 누릅니다.
4. 휴대폰 MetaMask에서 Sepolia 네트워크, 기존 판매자 수신 주소, **0.0001 ETH + 가스비**를 확인하고 승인합니다.
5. 게임이 블록 확인과 UGS 지급을 확인할 때까지 기다립니다. RPC 상황에 따라 늦어질 수 있습니다.
6. 자동 확인 시간이 끝나면 **전설검 결제 확인**을 누릅니다. 골드 결제 확인과 구분하세요.
7. 성공하면 인벤토리에 **전설검 1개**가 추가되고 COIN 잔액은 유지돼야 합니다.
8. 전설검 결제 확인을 다시 눌러도 같은 결제로 전설검이 추가되지 않아야 합니다. 로컬 기록이 정리된 경우 확인할 결제가 없다고 나오는 것이 정상입니다.
9. Play를 종료하고 같은 게임 계정으로 로그인합니다. 인벤토리에 전설검이 남아 있어야 합니다.
10. 기존 **10,000골드 구매**도 따로 테스트합니다. 골드 결제는 골드만 지급해야 합니다.

전설검은 NFT가 아니므로 MetaMask의 NFT 목록에 나타나지 않습니다. 서버가 결제를 확인하고 UGS 인벤토리에 넣는 게임 아이템입니다.

기존 거래소의 전설검 판매·다른 계정 구매는 별도 확인합니다. 로컬 `js/Mkt_*`는 일반 아이템을 처리하지만 실제 Dashboard에 올라간 버전까지 이번 로컬 검사로 확인한 것은 아닙니다.

## 8. 오류가 나면

| 표시 | 먼저 확인할 것 |
|---|---|
| Script not found / 함수 없음 | 두 스크립트 이름, 동일 프로젝트·환경, Publish 여부 |
| SETUP_REQUIRED | LEGENDARY_SWORD가 Inventory Item으로 Publish됐는지 |
| PENDING / 입금 확인 대기 | 전설검 결제 확인으로 재조회. 다시 송금하지 않음 |
| WRONG_PLAYER | 로그인 계정과 결제 계정, 골드/전설검 확인 버튼 혼동 여부 |
| REVIEW_REQUIRED | 지급 도중 결과가 불확실한 상태. 거래 해시와 Console 전체 오류를 보관 |
| 성공인데 인벤토리 없음 | Refresh 후 Player Management에서 해당 플레이어의 Economy Inventory 확인 |

REVIEW_REQUIRED에서 `state`를 삭제하거나 임의로 GRANTED로 바꾸면 안 됩니다. 지급 내역과 아이템 인스턴스를 대조해야 합니다. 전설검 결제 기록은 `payments` 안의 `sword_0x…` 키에 저장되며 인스턴스 ID는 `sword_` + 거래 해시의 0x를 제외한 부분입니다. 이미 지급한 검을 판매한 뒤 다시 결제 확인한다고 새 검을 지급하지 않습니다.

MetaMask에 **실제 송금도 승인 대기 요청도 없을 때만**, Play 중 WalletPanel의 Scene Wallet Panel 컴포넌트 메뉴에서 **Recovery - Clear unsubmitted SWORD payment**로 미전송 로컬 기록을 해제할 수 있습니다. 송금 완료 건의 해결 방법은 아닙니다.

선택 기능인 TransactionHashInput을 이미 사용 중이면 해당 상품의 해시를 넣고 맞는 확인 버튼을 누릅니다. 수동 입력한 해시는 자동 저장하지 않으므로 별도로 보관합니다.

## 9. 이후 단계

전설검 실결제가 확인된 뒤 `전설검과신화검nft계획.md`의 신화검NFT 단계를 진행합니다. 계약 배포, 쿠폰 발행, 서명 기반 지갑 연결 검증, 로그인 시 NFT 소유권 조회와 인벤토리 동기화가 이어집니다. 현재의 Add Coin 등 개발용 버튼과 클라이언트 직접 지급 기능은 공개 서비스 배포 전 별도 권한 정리가 필요합니다.
