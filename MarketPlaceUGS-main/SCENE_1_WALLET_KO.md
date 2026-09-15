# Scene 1 — 만들어 둔 WalletPanel에 지갑 연결하기

이 문서는 기존 `0`을 복사한 **`Assets/Scene/1.unity`**를 사용합니다. 별도 SimpleMarket 장면을 생성하지 않습니다. 기존 거래소의 매물 저장 방식과 Cloud Code `Mkt_*` 5개는 이번 연결에서 교체하지 않습니다.

## 지금 할 일: 만들어 둔 UI와 코드를 연결

1. **Play를 끄고 Ctrl+S로 Scene 1을 저장**합니다. 화면의 `1*` 표시는 저장하지 않은 변경이 있다는 뜻입니다.
2. Unity로 돌아가 새 C# 코드의 컴파일이 끝날 때까지 기다립니다. 메뉴가 안 보이면 `Assets → Refresh`를 실행합니다.
3. Hierarchy에서 **Canvas → WalletPanel**을 선택합니다.
4. 상단 메뉴 **Simple Market → Scene 1 - Connect Selected WalletPanel**을 누릅니다.
5. Inspector에 `Scene Wallet Panel`, `Web Gl Wallet`, `Reown Wallet Bridge` 컴포넌트가 생깁니다. 이름을 기준으로 버튼·텍스트를 자동 연결합니다.
6. 아래 표와 일치하는지 확인합니다. 자동 연결에서 찾지 못한 칸만 드래그해서 채웁니다.

| Scene Wallet Panel 필드 | 넣을 오브젝트 / 컴포넌트 |
|---|---|
| Wallet Button | `WalletButton`의 Button |
| Payment Check Button | `PaymentCheckButton`의 Button |
| Buy 10000 Gold Button | `Buy10000GoldButton`의 Button |
| Wallet Address Text | `WalletAddressText`의 TMP 텍스트 |
| Sepolia Eth Text | `Sepolia ETH Text`의 TMP 텍스트 |
| Payment Status Text | `PaymentStatusText`의 TMP 텍스트 |
| Market Demo | 기존 `PortfolioMarketDemo`가 붙은 오브젝트 — 보통 `GameManager` |
| Environment Name | 기존 UGS에서 쓰는 환경 이름. 기본 초기화라면 `production` |

**버튼의 On Click() 이벤트는 따로 등록하지 않습니다.** 컴포넌트가 실행 시 연결합니다. 이미 직접 같은 기능을 연결했다면 제거해서 중복 호출되지 않게 합니다. `SimpleMarketApp`을 WalletPanel에 추가하지 않습니다.

7. Ctrl+S로 저장합니다. 지금 단계에서는 Play → 기존 게임 로그인 후 버튼이 활성화되는지까지 확인합니다. SDK를 아직 설치하지 않았다면 지갑 연결 버튼은 **QR 준비 안내**를 표시합니다. 가짜 지갑이나 가짜 지급은 하지 않습니다.

선택 항목 `Transaction Hash Input`, `Forget Payment Button`은 비워도 최초 정상 결제가 가능합니다. 아래 복구 UI는 함께 추가하는 것을 권장합니다.

## Editor에서 QR 테스트 준비

이 부분은 지갑 SDK 설치와 본인의 Reown 계정 설정이 필요합니다. 코드는 **Reown AppKit Unity 1.7.1의 공개 패키지 소스**에 맞춰 작성했습니다. 아직 실제 모바일 지갑 연결은 검증하지 않았습니다.

1. Unity의 **File → Build Profiles**에서 활성 플랫폼을 **Windows**로 둡니다. Editor에서 QR을 테스트할 때 WebGL을 활성 플랫폼으로 두지 않습니다.
2. [Reown Dashboard](https://dashboard.reown.com/)에서 프로젝트를 만들고 **Project ID**를 복사합니다. **Unity UGS의 Project ID와 다른 값**입니다.
3. Unity **Edit → Project Settings → Package Manager → Scoped Registries**에 추가합니다.

   - Name: `OpenUPM`
   - URL: `https://package.openupm.com`
   - Scope: `com.reown`
   - Scope: `com.nethereum`

4. **Window → Package Manager → + → Install package by name**에서 다음을 설치합니다.

   - Name: `com.reown.appkit.unity`
   - Version: `1.7.1`

   의존 패키지도 내려받습니다. Package Manager에 오류가 없다면 별도로 동일 패키지를 중복 설치하지 않습니다. 의존성 문제는 [공식 설치 안내](https://docs.reown.com/appkit/unity/core/installation)를 기준으로 해결합니다. `vectorgraphics` 오류가 나면 해당 패키지 설치 여부를 확인합니다.

5. Project 창의 **Packages → Reown AppKit Unity → Prefabs**에서 **Reown AppKit** Prefab을 `1` Scene의 Hierarchy에 하나 배치합니다. WalletPanel 안이 아니라 Hierarchy 최상위에 두면 됩니다. QR UI는 이 Prefab이 제공합니다.
6. **Edit → Project Settings → Player → Other Settings → Scripting Define Symbols**에 `SIMPLE_MARKET_REOWN`을 추가하고 Apply합니다. 기존 심볼은 지우지 않습니다. SDK 설치가 끝난 뒤 추가해야 컴파일 오류가 나지 않습니다.
7. `WalletPanel`의 **Reown Wallet Bridge**에서 다음을 입력합니다.

   - Project Id: 위에서 복사한 **Reown Project ID**
   - Game Url: 본인 프로젝트 소개 URL
   - Icon Url: 공개 아이콘 이미지 URL

   URL은 지갑에 보여줄 앱 정보이며 개인 키가 아닙니다. 코드의 example.com 값은 자리표시자이므로 본인의 정보로 바꿉니다. SDK를 초기화하는 별도 샘플 스크립트는 붙이지 않습니다. Bridge가 초기화합니다.

8. 공식 SDK는 Gamma 색 공간을 전제로 안내합니다. 현재 프로젝트가 Linear라면 QR UI 표시 호환성을 확인해야 합니다. 변경하려면 **Scene만이 아니라 프로젝트 전체 렌더링에 영향을 주므로**, 현재 설정을 기록한 뒤 Player의 Color Space를 Gamma로 설정하고 기존 화면을 확인하세요. 이 작업에서 자동으로 바꾸지는 않았습니다.
9. Ctrl+S → Play → 기존 게임 로그인 → **지갑 연결**을 누릅니다.
10. 연결 창의 **MetaMask**를 선택하고 휴대폰 MetaMask 앱의 스캔 기능으로 QR을 읽습니다. 앱에서 연결 및 Sepolia 전환을 승인합니다.

연결되면 기존 `WalletAddressText`와 `Sepolia ETH Text`에 주소·잔액이 표시됩니다. 게임 계정은 UGS가 그대로 관리합니다. 이 연결은 지갑 소유권을 UGS 계정에 영구 등록하는 기능이나 Google 로그인 기능이 아닙니다.

### WebGL로 실행할 때

같은 SceneWalletPanel이 브라우저용 WebGlWallet로 전환됩니다. Reown Prefab은 WebGL 빌드 대상 Scene에서 제외하거나 비활성화하고 브라우저 MetaMask 확장 프로그램을 사용합니다. WebGL 쪽 코드에는 Reown 의존성이 없습니다.

**기존 `Simple Market → 2. Build WebGL` 메뉴는 별도 SimpleMarket 장면을 대상으로 하므로 누르지 않습니다.** `File → Build Profiles`에서 **Scene 1만 빌드 목록에 넣고** WebGL로 빌드합니다. 로컬 서버를 쓰려면 출력 경로를 `Builds/WebGL`로 지정하고 `node Tools/serve-webgl.js`로 실행합니다.

## 다음: UGS에 골드 결제 설정

지갑 연결까지 성공하면 결제 서버를 설정합니다. 기존 거래소와 **같은 UGS 프로젝트·환경**에서 작업합니다.

1. Economy의 `COIN`이 Publish되어 있는지 확인합니다. 신규 계정 1,000골드는 `Initial balance = 1000`으로 설정하며 기존 계정에는 소급 적용하지 않습니다.
2. Cloud Save → **Game Data → Default**에 Custom ID `simple_market`을 만들고 키 두 개를 추가합니다.

   - `state`: `CloudCode/state.initial.json`의 JSON 객체. **최초 한 번만 생성**합니다. 기존 값이 있다면 덮어쓰지 않습니다.
   - `config`: `CloudCode/config.example.json`의 JSON 객체. `receiverAddress`를 **수신용 Sepolia 공개 지갑 주소**로 바꿉니다. 수신 계정은 구매용 MetaMask에 등록되지 않은 별도 지갑이어야 합니다. 같은 앱의 다른 계정도 MetaMask의 internal accounts 제한에 걸립니다. 별도 브라우저 프로필에서 독립적으로 생성한 상점 지갑의 공개 주소를 사용할 수 있습니다. 그 지갑을 구매용 MetaMask에 가져오지 마세요.

3. Cloud Code에서 아래 **두 스크립트만** 추가하거나 갱신하고 Publish합니다.

| 이름 | 붙여넣을 파일 | 파라미터 |
|---|---|---|
| `Gold_GetQuote` | `CloudCode/deploy/Gold_GetQuote.js` 전체 | 없음 |
| `Gold_Claim` | `CloudCode/deploy/Gold_Claim.js` 전체 | `tx_hash`: String, Required |

기존 `Mkt_*`를 덮어쓰지 않습니다. 이 두 파일에 공유 코드가 포함되어 있어도 실행되는 진입점은 각각 Gold_GetQuote / Gold_Claim뿐입니다. 기존 매물 데이터는 자동 이전하지 않습니다.

4. 구매용 모바일 지갑에 Sepolia ETH를 넣습니다. 가격은 **0.0001 ETH + 가스비**입니다.
5. Play → 게임 로그인 → QR 지갑 연결 → **10,000골드 구매** → 휴대폰에서 승인합니다.
6. 전송 후 자동으로 확인하며, 세 블록 이상 확인되면 서버가 해당 게임 계정의 `COIN`에 10,000을 더합니다. 기존 `PortfolioMarketDemo.RefreshCoinsAsync()`로 기존 골드 표시만 갱신합니다.
7. UGS Dashboard의 같은 플레이어 잔액도 확인합니다. `결제 확인`을 반복해도 동일 거래는 추가 지급하지 않습니다.

**기존 화면의 `Add Coin`·무료 랜덤 지급은 아직 기존 테스트 코드입니다.** 지갑 결제 버튼을 연결했다고 클라이언트 직접 지급이나 기존 거래소의 모든 동시성 문제가 해결되는 것은 아닙니다. 최종 테스트 완료 전에 무료 지급 버튼 제거·가챠의 서버 처리·Economy 플레이어 쓰기 차단을 함께 적용해야 합니다. 기존 직접 지급 코드를 그대로 둔 채 `project-policy.json`을 적용하면 해당 버튼은 거부됩니다. 이 문서의 단계에서는 결제 추가와 기존 거래소 교체를 섞지 않습니다.

## 복구용 UI 두 개 — 권장

WalletPanel 아래에 다음 두 오브젝트를 추가한 후 자동 연결 메뉴를 다시 실행하거나 Inspector에 직접 연결합니다.

- `TransactionHashInput`: **UI → TMP Input Field**. 휴대폰 MetaMask 활동에서 거래 해시를 복사해 넣으면 결제 확인에 사용합니다.
- `ForgetPaymentButton`: **UI → Button**, 표시 문구 `기록 해제`. 전송하지 않았음을 MetaMask에서 확인한 후 로컬 대기 기록을 해제합니다. **환불·블록체인 취소가 아닙니다.**

QR 백엔드는 승인 거절·연결 끊김 등으로 전송 결과가 불명확하면 대기 기록을 보수적으로 유지합니다. 전송이 없음을 확인한 뒤 기록 해제로 다시 시도할 수 있습니다. 실제로 전송됐다면 기록을 해제하는 대신 해시를 입력하고 결제 확인을 누릅니다. `REVIEW_REQUIRED`는 서버의 수동 확인이 필요한 상태이며 `CloudCode/OPERATIONS_KO.md`를 따릅니다.

## 작성한 코드

- `SceneWalletPanel.cs`: 기존 UI 버튼 연결, 결제 요청, 로그인 계정 확인, 서버 검증, 기존 골드 표시 갱신
- `ReownWalletBridge.cs`: 선택 설치한 Reown SDK를 통한 Editor QR 연결·Sepolia 전송·거래 해시 저장
- `SceneWalletSetup.cs`: 선택한 WalletPanel을 이름으로 자동 연결. 현재 장면을 다른 장면으로 바꾸지 않음
- `PortfolioMarketDemo.cs`: 기존 잔액 조회 함수 `RefreshCoinsAsync`의 접근 수준만 public으로 변경

SDK가 설치되기 전에도 기존 프로젝트는 컴파일되도록 QR 코드를 조건부로 구성했습니다. `SIMPLE_MARKET_REOWN`은 SDK 설치 후에만 켭니다. 실제 지갑 연결·승인은 본인 Reown Project ID와 휴대폰으로 확인해야 합니다.
