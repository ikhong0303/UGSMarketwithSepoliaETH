# Simple RPG Market — 프로젝트 열기부터 골드 결제까지

> **기존 Scene 0을 복사한 Scene 1에 직접 WalletPanel을 만들었다면 [SCENE_1_WALLET_KO.md](SCENE_1_WALLET_KO.md)를 따라 하세요.** 아래 본문은 별도 자동 생성 장면 기준입니다. Scene 1에서는 자동 장면 생성·빌드 메뉴 및 기존 Mkt 스크립트 일괄 교체 단계를 사용하지 않습니다.

이번 구현 범위는 **회원가입 → 1,000골드 → 가챠·아이템 거래 → MetaMask 연결 → 세폴리아 ETH 결제 → UGS 10,000골드 지급**입니다.

일반 아이템과 골드는 UGS 데이터입니다. 실제 NFT 발행·구매는 이 단계에 포함하지 않았습니다. NFT는 이후 스마트 컨트랙트를 붙이는 별도 단계입니다.

## 0. 준비물과 역할

| 준비물 | 용도 |
|---|---|
| Unity Hub + Unity 6.3 Editor | 프로젝트 열기·WebGL 빌드 |
| WebGL Build Support 모듈 | 브라우저용 빌드 |
| 본인 Unity 계정 / UGS 프로젝트 | 회원·골드·아이템·서버 코드 |
| PC 크롬 + MetaMask 확장 프로그램 | 구매자의 결제 승인 |
| 수신용 지갑 주소 1개 | 테스트 ETH를 받는 계정 |
| 구매자 지갑의 Sepolia ETH | 상품 가격 + 가스비 |
| Node.js 22 이상 | 제공한 로컬 실행 서버·자동 테스트 |

**게임 코드와 브라우저 연결 코드는 이미 작성되어 있습니다. 아래에서는 계정 설정, 파일 붙여넣기, 실행만 합니다.** MetaMask Developer Client ID, Google 로그인 설정, Nethereum, 담당자1의 패키지는 이번 방식에 필요하지 않습니다.

원본 프로젝트 버전은 `6000.3.2f1`입니다. 이 PC에는 `6000.3.18f1`과 WebGL 지원 모듈이 있습니다. 같은 6.3 계열로 열되, 처음 업그레이드할 때 프로젝트 폴더를 복사해 두세요. 코드를 검증하려고 실행한 Editor는 라이선스 미활성화로 종료되어, 실제 Unity 컴파일·WebGL 빌드 성공 여부는 아직 확인하지 못했습니다.

## 1. Unity 프로젝트 불러오기

1. Unity Hub에 로그인합니다.
2. Hub 설정의 Licenses에서 사용할 라이선스를 활성화합니다. Personal 사용 자격이 있다면 해당 라이선스를 선택합니다. `No valid Unity Editor license found`가 나오면 이 단계부터 해결합니다.
3. Hub의 Installs에서 사용할 Editor의 **WebGL Build Support** 설치 여부를 확인합니다.
4. Projects → Add / Add project from disk에서 **`MarketPlaceUGS-main` 폴더**를 선택합니다. `Assets`, `Packages`, `ProjectSettings`가 들어 있는 바로 그 폴더입니다.
5. 프로젝트를 열고 Package Manager의 패키지 가져오기가 끝날 때까지 기다립니다.
6. Unity 상단 메뉴 **Simple Market → 1. Create or Open Test Scene**을 실행합니다.
7. `Assets/Scene/SimpleMarket.unity`가 생성되고 빌드 대상으로 지정됩니다. `SimpleMarketApp`의 Environment Name은 우선 `production`으로 둡니다.

버튼·입력창·아이템 목록은 실행 시 자동 생성됩니다. 각 버튼을 Inspector에서 연결할 필요가 없습니다. 기존 `Assets/Scene/0.unity`는 원본 참고용이며, 이번 테스트는 **SimpleMarket 장면**에서 합니다.

## 2. 본인 UGS 프로젝트 연결

1. [Unity Dashboard](https://cloud.unity.com/)에서 본인 조직에 **새 테스트 프로젝트**를 만듭니다. 기존 담당자의 서비스 데이터와 섞이지 않게 새 프로젝트로 시작하는 것이 쉽습니다.
2. Unity Editor의 **Edit → Project Settings → Services**에서 같은 조직·프로젝트를 연결합니다. 기존 Project ID가 남아 있다면 본인 프로젝트로 바꿉니다.
3. Dashboard의 환경을 `production`으로 선택합니다. 여기서 production은 UGS 환경 이름이며, Ethereum 메인넷이라는 뜻이 아닙니다. 블록체인은 항상 Sepolia만 사용합니다.
4. **Authentication / Player Authentication → Identity Providers → Username & Password**를 추가하고 저장합니다.

프로젝트, 환경, Economy, Cloud Save, Cloud Code는 모두 같은 곳을 사용해야 합니다. 다른 UGS 환경을 사용하려면 장면의 Environment Name도 같은 이름으로 변경한 후 다시 빌드합니다.

## 3. Economy에 골드와 아이템 등록

Economy → Configuration에서 아래 리소스를 만듭니다. 메뉴 표시는 Dashboard 언어에 따라 조금 다를 수 있습니다.

| 종류 | Resource ID — 정확히 입력 | 이름 | 설정 |
|---|---|---|---|
| Currency | `COIN` | 골드 | Initial balance = **1000**, Maximum balance = 0 또는 제한 없음 |
| Inventory Item | `SWORD` | 검 | 기본 설정 |
| Inventory Item | `REDPOTION` | 빨간 포션 | 기본 설정 |
| Inventory Item | `BLUEPOTION` | 파란 포션 | 기본 설정 |

**Save 후 Publish까지 실행합니다.** 이 버전은 Virtual Purchase / Real Money Purchase를 추가할 필요가 없습니다. 구매·가챠·세폴리아 검증은 제공한 Cloud Code에서 처리합니다.

신규 플레이어가 잔액을 처음 조회하면 Economy가 초기 잔액 1,000을 생성합니다. 아이템은 자동 지급하지 않으므로 0개입니다. 로그인할 때마다 1,000을 더하는 방식이 아닙니다. 초기화가 끝난 기존 테스트 계정에는 초기 잔액 수정이 소급 적용되지 않으므로 새 계정으로 확인하세요.

아이템의 표시 이름과 판매가는 기존 `Assets/Data/Market/GlobalItemVisuals.asset`을 사용합니다. 기본 판매가는 검 120 / 빨간 포션 100 / 파란 포션 130골드입니다. 가챠는 서버 고정 100골드, 세 아이템이 각각 1/3 확률입니다.

## 4. Cloud Save 초기 데이터 2개 넣기

Cloud Save의 **Game Data**에서 Custom ID를 **`simple_market`**로 만들고, **Default 접근 등급**에 아래 키를 추가합니다. Player Data가 아닙니다.

### 키 1: `state`

값 유형은 **JSON 객체**입니다. `CloudCode/state.initial.json`의 내용을 그대로 넣습니다.

```json
{"version":1,"payments":{},"listings":{},"operations":{}}
```

문자열 안에 JSON을 넣지 말고 객체로 저장합니다. 이 최초 값이 있어야 Cloud Save의 writeLock을 이용한 동시 요청 제어가 작동합니다. **한 번만 초기화하고, 거래 시작 후에는 덮어쓰거나 지우지 마세요.** 결제 재사용 방지 기록도 여기에 저장됩니다.

### 키 2: `config`

`CloudCode/config.example.json`을 복사하고 `receiverAddress`만 **수신용 공개 지갑 주소**로 바꿉니다.

```json
{
  "receiverAddress": "여기를_0x로_시작하는_수신용_지갑주소로_교체",
  "rpcUrl": "https://ethereum-sepolia-rpc.publicnode.com",
  "priceWei": "100000000000000",
  "goldAmount": 10000,
  "confirmations": 3
}
```

- 수신용 계정과 구매자 계정은 달라야 합니다. MetaMask에 계정을 2개 만들고 한 계정은 수신용, 다른 계정은 구매용으로 사용할 수 있습니다.
- 수신용은 일반 지갑 주소를 사용합니다. 스마트 컨트랙트·위임된 스마트 계정 주소는 이번 버전에서 받지 않습니다.
- 가격은 **0.0001 Sepolia ETH = 10,000골드**로 고정했습니다. 숫자를 임의로 바꾸면 검증이 거부됩니다. 가격 변경은 상품 버전·클라이언트·서버를 함께 바꾸어야 합니다.
- RPC는 서버가 블록체인 거래를 조회하는 주소입니다. 예시는 공개 RPC이므로 사용량 제한·장애가 생길 수 있습니다. 이 Default Game Data는 비밀 저장소가 아니므로 API 비밀키가 포함된 URL이나 지갑 개인 키를 넣지 않습니다.
- `confirmations: 3`은 거래가 포함된 블록부터 세 블록을 확인한다는 뜻입니다. 테스트용 확정 기준이며 Ethereum의 완전한 최종성을 뜻하지 않습니다.

## 5. Cloud Code 스크립트 8개 등록

**`CloudCode/deploy` 안의 `.js` 파일**을 사용합니다. 각각 다른 파일을 필요로 하지 않는 완성된 스크립트입니다. 원래 `js/*.txt`는 참고용으로 보존했으며, 새 서버 코드는 아래 버전을 등록합니다.

Dashboard → Cloud Code → Scripts에서 JavaScript 스크립트를 만들고 다음 순서로 반복합니다.

1. 이름을 파일 이름에서 `.js`를 뺀 것과 정확히 같게 입력합니다.
2. 해당 파일 전체 내용을 붙여넣습니다.
3. 저장 후 파라미터를 확인합니다. 아래 표 및 `deploy/parameters.json`과 일치해야 합니다. `module.exports.params`도 코드에 포함되어 있습니다.
4. **Publish**합니다.

| 스크립트 이름 | 파라미터 |
|---|---|
| `Mkt_GetActiveListings` | `limit`: Numeric 선택, `sort`: String 선택 |
| `Mkt_CreateListing` | `players_inventory_item_id`: String 필수, `price`: Numeric 필수, `currency_id`: String 선택 |
| `Mkt_BuyListing` | `listing_id`: String 필수 |
| `Mkt_CancelListing` | `listing_id`: String 필수 |
| `Mkt_ClaimEarnings` | `currency_id`: String 선택 |
| `Mkt_Gacha` | `request_id`: String 필수 |
| `Gold_GetQuote` | 없음 |
| `Gold_Claim` | `tx_hash`: String 필수 |

코드 안에 Project ID, Player ID, 토큰을 직접 입력하지 않습니다. UGS가 인증된 호출의 `context`로 제공합니다. Dashboard에서 수동 테스트할 때도 실제 테스트 플레이어를 지정해야 합니다.

새 스크립트는 기존 거래소의 이름·입출력 형태를 재사용하지만, 저장소는 `market` 대신 `simple_market`입니다. 기존 매물과 정산 내역을 자동 이전하지 않습니다. 그래서 새 테스트 UGS 프로젝트를 권장합니다. 기존 `Mkt_*`와 새 버전을 섞어 배포하지 마세요.

### 5-1. 클라이언트가 골드를 직접 늘리지 못하게 설정

UI에서 무료 지급 버튼을 없애도 UGS의 직접 쓰기 권한이 열려 있으면 임의 지급을 막을 수 없습니다. **지갑 결제 테스트를 완료하기 전에** 제공한 정책을 적용합니다.

1. [UGS CLI 시작 안내](https://docs.unity.com/en-us/services/ugs-cli-introduction)를 따라 CLI 설치·인증 및 본인 프로젝트 선택을 진행합니다.
2. 공식 가이드에 따라 서비스 계정에 Project Resource Policy Editor, Project Resource Policy Reader, Unity Environments Viewer 역할을 부여합니다. 서비스 계정 키는 로컬 CLI에만 사용하고 Unity 코드나 WebGL 빌드에 넣지 않습니다.
3. `MarketPlaceUGS-main` 폴더에서 실행합니다.

```powershell
ugs access upsert-project-policy CloudCode/project-policy.json
```

본인 프로젝트가 선택되어 있는지 먼저 확인합니다. 정책은 플레이어의 Economy 쓰기를 차단하고, Cloud Code의 서비스 권한으로만 변경하게 합니다. 기존 정책이 있는 프로젝트라면 기존 정책을 확인하고 병합하세요. 자세한 설정은 [Unity 공식 접근 제어 예제](https://docs.unity.com/en-us/cloud-code/scripts/how-to-guides/access-control)를 참고합니다.

## 6. Editor에서 UGS 기능 먼저 확인

SimpleMarket 장면을 열고 Play를 누릅니다.

1. 아이디·비밀번호를 입력하고 **회원가입**합니다. 아이디는 3~20자, 영문·숫자 및 `. - @ _` 사용 가능. 비밀번호는 8~30자이며 대문자·소문자·숫자·기호를 포함합니다.
2. **골드 1,000 / 보유 아이템 0개**가 표시되는지 확인합니다.
3. 가챠를 한 번 누릅니다 → **골드 900 / 아이템 1개**.
4. 아이템의 판매 등록을 누릅니다 → 보유 목록에서 빠지고 거래소에 표시됩니다.
5. 로그아웃하고 다른 게임 계정을 가입합니다 → 그 계정으로 상품을 구매합니다.
6. 판매자 계정으로 돌아와 **판매대금 받기**를 누릅니다.
7. 한 번 더 눌렀을 때 0골드가 지급되는지 확인합니다.

**Editor의 지갑 버튼은 안내 메시지만 표시합니다.** MetaMask 결제는 다음 단계의 브라우저에서 합니다.

## 7. WebGL 빌드·실행·지갑 연결

1. Unity 메뉴 **Simple Market → 2. Build WebGL**을 누릅니다. `Builds/WebGL`에 결과가 생성됩니다. 첫 빌드는 오래 걸릴 수 있습니다.
2. `MarketPlaceUGS-main`에서 터미널을 열고 실행합니다.

```powershell
node Tools/serve-webgl.js
```

3. PC 크롬에서 [로컬 게임 실행](http://localhost:8080)을 엽니다. `index.html` 파일을 직접 더블클릭하지 않습니다.
4. 크롬에 MetaMask를 설치하고 구매용 계정을 준비합니다.
5. MetaMask에서 테스트 네트워크 표시를 켜고 **Sepolia**를 선택합니다.
6. 구매용 지갑에 테스트 ETH를 받습니다. 예: [Google Cloud Sepolia Faucet](https://cloud.google.com/application/web3/faucet/ethereum/sepolia). Faucet 이용 조건은 제공처에 따라 달라질 수 있습니다.
7. 게임에 로그인하고 **지갑 연결**을 누릅니다. MetaMask에서 연결을 승인합니다. 게임에 지갑 주소와 Sepolia ETH 잔액이 표시됩니다.

수신용 계정으로 구매하면 거부됩니다. 게임 로그아웃은 MetaMask 계정을 삭제하거나 확장 프로그램의 사이트 권한을 해제하는 동작이 아닙니다. 게임 계정마다 결제 확인 기록을 분리하며, 영구적인 지갑 소유권 등록·서명 로그인은 이번 범위에 없습니다.

## 8. 지갑으로 골드 구매 → UGS 갱신 확인

1. 현재 게임 골드를 기록합니다. 예를 들어 1,000골드입니다.
2. **10,000골드 구매 · 0.0001 ETH + 가스비**를 누릅니다.
3. MetaMask에서 **Sepolia 네트워크·수신 지갑·0.0001 ETH**를 확인하고 승인합니다. 가스비는 별도입니다.
4. 전송한 거래 해시가 자동 저장됩니다. 브라우저가 확인 요청을 반복하지만 ETH 전송을 다시 하지는 않습니다.
5. 서버가 거래를 검증하고 3블록 이상 확인되면 UGS `COIN`에 10,000을 더합니다.
6. 게임 잔액이 예시 기준 **11,000**으로 바뀝니다.
7. Unity Dashboard → Player Management에서 해당 플레이어의 Economy `COIN` 잔액도 확인합니다.
8. 로그아웃·재로그인해도 11,000이 유지되어야 합니다.

지갑으로 ETH를 보내는 것만으로 게임이 자동으로 계정을 알아내지는 못합니다. 게임 구매 버튼은 거래 데이터에 프로젝트·환경·플레이어 식별자를 넣고, 서버가 로그인한 계정과 비교합니다. 이 식별자는 블록체인에서 공개됩니다. 계정 비밀번호나 인증 토큰은 넣지 않습니다. **MetaMask에서 수신 지갑으로 직접 송금한 거래는 골드 지급 대상이 아닙니다.**

### 기다리는 중 창을 닫았거나 오류가 난 경우

- 같은 브라우저·주소·게임 계정으로 다시 접속하고 **결제 확인**을 누릅니다. 이 버튼은 추가 ETH를 보내지 않습니다.
- 다른 브라우저로 옮겼다면 MetaMask 활동에서 거래 해시를 복사하여 아래 입력창에 붙여넣고 결제 확인을 누릅니다.
- MetaMask에서 거래를 Speed up한 경우 새 거래 해시를 입력할 수 있습니다.
- 승인 거절은 미결제 기록을 해제합니다. 네트워크 오류로 전송 결과를 알 수 없으면 기록을 유지합니다. MetaMask 활동을 먼저 확인하세요.
- 거래가 체인에서 실패했다고 서버가 확인하면 지급하지 않고 미결제 기록을 해제합니다. 가스비가 사용되었을 수 있습니다.
- 잔액 부족으로 전송되지 않았거나 MetaMask에서 거래를 취소·대체하여 기존 해시로 확인할 수 없다면, **MetaMask 활동을 먼저 확인한 뒤** `기록 해제`를 누를 수 있습니다. 이 버튼은 브라우저의 확인 기록만 지우며 환불·블록체인 취소를 하지 않습니다. 아직 진행 중인 거래가 있다면 먼저 기다리세요. 이미 전송한 거래 해시는 보관하면 나중에 다시 확인할 수 있습니다.
- `REVIEW_REQUIRED`는 **Economy 지급과 기록 저장 사이의 응답이 불확실**하다는 뜻입니다. 자동 재지급하지 않습니다. 관리자용 처리 절차는 `CloudCode/OPERATIONS_KO.md`를 따릅니다.

## 완료 확인표

- [ ] Unity가 오류 없이 컴파일되고 SimpleMarket 장면이 실행됨
- [ ] 신규 계정 아이템 0 / 골드 1,000
- [ ] 가챠 100골드 차감 / 아이템 1개
- [ ] 판매 등록 / 다른 계정 구매 / 판매대금 수령
- [ ] 플레이어의 Economy 직접 쓰기가 거부됨
- [ ] WebGL에서 MetaMask 연결 / Sepolia 잔액 표시
- [ ] 결제 승인 후 UGS 골드 +10,000
- [ ] 같은 거래 해시를 다시 확인해도 추가 지급 없음
- [ ] 다른 계정으로 같은 거래 해시를 제출하면 거부됨
- [ ] 재로그인 후 잔액 유지

## 코드와 검증 위치

| 위치 | 내용 |
|---|---|
| `Assets/Scripts/SimpleMarket/SimpleMarketApp.cs` | 가입·로그인·거래소 UI·결제 진행·UGS 잔액 갱신 |
| `Assets/Scripts/SimpleMarket/WebGlWallet.cs` | Unity ↔ 브라우저 비동기 연결 |
| `Assets/Plugins/WebGL/SimpleMarketWallet.jslib` | MetaMask 연결·Sepolia 전송·로컬 거래 해시 복구 |
| `Assets/Editor/SimpleMarketSetup.cs` | 장면 자동 생성·WebGL 빌드 메뉴 |
| `CloudCode/src` | 서버 원본 |
| `CloudCode/deploy` | Dashboard에 붙여넣을 독립 스크립트 8개 |
| `CloudCode/tests` | 결제 검증·동시성·브라우저 지갑 테스트 |

서버를 수정했을 때만 다음을 실행하고 `deploy`를 다시 Publish합니다.

```powershell
cd CloudCode
node build.js
node --test --test-isolation=none tests/*.test.js
```

자동 테스트는 UGS와 MetaMask를 모의한 로컬 테스트입니다. 실제 Dashboard 연결·계정 생성·ETH 송금·WebGL 실행은 위 체크리스트로 별도 검증해야 합니다.

## 공식 문서

- [Unity Economy Dashboard](https://docs.unity.com/ko-kr/economy/write-configuration/unity-dashboard)
- [Economy 초기 잔액](https://docs.unity.com/en-us/Economy/item-types)
- [Username & Password](https://docs.unity.com/en-us/authentication/platform-signin/username-password)
- [Cloud Save Game Data](https://docs.unity.com/en-us/cloud-save/concepts/game-data)
- [Cloud Code 사용 가능 라이브러리](https://docs.unity.com/en-us/cloud-code/scripts/reference/available-libraries)
- [MetaMask 브라우저 Provider API](https://docs.metamask.io/metamask-connect/evm/reference/provider-api/)
- [Unity WebGL과 JavaScript 연결](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-interactingwithbrowserscripting.html)

문서 확인일: 2026-09-14. 보내주신 Embedded Wallets Unity SDK 문서는 소셜 로그인 지갑 방식이며, 이번 브라우저 확장 지갑 구현에서는 설치하지 않습니다.
