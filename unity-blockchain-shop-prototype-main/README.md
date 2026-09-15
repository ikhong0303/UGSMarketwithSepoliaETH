Unity에서 Google 계정으로 Embedded Wallet에 로그인하고, Ethereum Sepolia 테스트넷에서 상품 구매와 판매자 지갑 입금을 확인하는 교육용 프로토타입입니다.

## 주요 기능

- Google 계정으로 Embedded Wallet 로그인
- 지갑 공개 주소 복사 및 Sepolia ETH 잔액 표시
- 상품별 예상 가스비(수수료)와 예상 결제 총액 표시
- 별도의 MetaMask 판매자 지갑으로 Sepolia ETH 전송
- 거래 성공 여부와 실제 가스비 표시
- 구매한 상품의 보유 개수 저장
- 잔액 부족 및 거래 실패 처리

> 이 프로젝트는 교육용 프로토타입입니다. 실제 서비스나 실제 자산 거래에 사용하지 마세요.

## 1. 준비 사항

- Unity 6.5 계열
  - 개발 버전: Unity 6000.5.5f1
  - 이식 테스트 버전: Unity 6000.5.7f1
- 인터넷 연결
- Google 계정
- 제공된 `.unitypackage` 파일

구매자는 MetaMask 앱이나 MetaMask Developer 계정을 만들 필요가 없습니다. Google 로그인 시 테스트용 Embedded Wallet이 생성됩니다.

## 2. 필수 패키지 설치

### Nethereum

Unity 메뉴에서 `Edit > Project Settings > Package Manager`를 열고 다음 Scoped Registry를 추가합니다.
<details>
<summary>설명</summary>

이 설정은 Unity Package Manager에서 Nethereum Unity 패키지를 검색하고 설치할 수 있도록 OpenUPM 저장소를 등록하는 과정입니다.

Unity의 기본 Registry에는 com.nethereum.unity가 없어서, 별도의 패키지 공급처인 OpenUPM을 알려줘야 합니다.

</details>

| 항목 | 값 |
|---|---|
| Name | `package.openupm.com` |
| URL | `https://package.openupm.com` |
| Scope | `com.nethereum.unity` |

<img width="935" height="318" alt="image" src="https://github.com/user-attachments/assets/da4c8d48-4071-4ad3-9d4d-45c7e9519c6c" />


그다음 `Window > Package Manager`를 열고 왼쪽 위 `+` 버튼에서  
`Install package by name` 또는 `Install package by technical name`을 선택합니다.

다음 값을 입력하고 `Install`을 누릅니다.

| 항목 | 값 |
|---|---|
| Name | `com.nethereum.unity` |
| Version | `4.19.2` |
<img width="319" height="145" alt="image" src="https://github.com/user-attachments/assets/230e9982-f076-4abe-a907-b5d7291dc584" />

### 추가 패키지 확인

Package Manager에서 다음 패키지를 확인합니다.

- Unity UI (`com.unity.ugui`) 2.5.0
- Input System (`com.unity.inputsystem`) 1.19.0 이상
- Newtonsoft Json (`com.unity.nuget.newtonsoft-json`) 3.2.2

이미 호환되는 버전이 설치되어 있다면 버전을 낮추지 않아도 됩니다.

## 3. TextMeshPro 설정

Unity 메뉴에서 `Window > TextMeshPro > Import TMP Essential Resources`를 실행합니다.

이미 설치된 프로젝트에서는 생략할 수 있습니다. `TMP Examples & Extras`는 설치하지 않아도 됩니다.

## 4. 패키지 가져오기

1. `Assets > Import Package > Custom Package`를 선택합니다.
2. 제공된 `.unitypackage` 파일을 선택합니다.
3. 모든 항목을 선택하고 `Import`를 누릅니다.
4. Console에 빨간색 컴파일 오류가 없는지 확인합니다.

## 5. 상점 Prefab 배치

Prefab 위치:

```text
Assets/BlockchainShop/Prefabs/BlockchainShop.prefab
```

1. Scene에 Canvas가 없다면 생성합니다.
2. `BlockchainShop.prefab`을 Canvas 아래로 드래그합니다.
3. Scene에 EventSystem이 하나만 존재하는지 확인합니다.

Canvas의 `Canvas Scaler`를 다음과 같이 설정합니다.

| 항목 | 값 |
|---|---|
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | 1920 × 1080 |
| Screen Match Mode | Match Width Or Height |
| Match | 0.5 |
| Reference Pixels Per Unit | 100 |

`Edit > Project Settings > Player > Active Input Handling`은 다음 중 하나로 설정합니다.

- Input System Package (New)
- Both

Unity가 재시작을 요구하면 프로젝트를 재시작합니다.

## 6. 기능 테스트

1. Play 버튼을 누릅니다.
2. 화면 오른쪽 위 상점 버튼 또는 키보드 `P` 키로 상점을 엽니다.
3. Google 계정으로 로그인합니다.
4. 지갑 주소와 Sepolia ETH 잔액이 표시되는지 확인합니다.
5. 상품을 선택합니다.
6. 상품 가격, 예상 가스비와 예상 총액을 확인합니다.
7. `구매하기` 버튼을 누릅니다.
8. 거래 완료 후 다음 항목을 확인합니다.
   - 구매 성공 메시지
   - 실제 가스비
   - 상품 보유 개수 증가
   - 지갑 잔액 갱신

처음에는 가격이 `0.0001 Sepolia ETH`인 일반 상품으로 테스트하는 것을 권장합니다.

가격이 `0.05 Sepolia ETH`인 고가 상품은 잔액 부족 처리를 확인하기 위한 테스트 상품입니다.

## 7. Sepolia ETH 받기

Sepolia ETH는 테스트넷에서만 사용하는 화폐이며 실제 금전적 가치가 없습니다.

1. 상점에서 Google 로그인을 완료합니다.
2. 지갑 주소 옆의 `주소 복사` 버튼을 누릅니다.
3. `테스트 ETH 충전` 버튼을 눌러 Faucet 페이지를 엽니다.
4. 복사한 지갑 주소를 Faucet에 붙여넣습니다.
5. Sepolia ETH를 요청합니다.
6. 입금 완료 후 상점을 다시 열거나 다시 로그인하여 잔액을 갱신합니다.

Faucet 주소:

https://cloud.google.com/application/web3/faucet/ethereum/sepolia

Faucet에는 `0x`로 시작하는 공개 지갑 주소만 입력하세요.

다음 정보는 절대 입력하거나 공유하지 마세요.

- 개인 키
- 복구 문구
- Client Secret
- 로그인 토큰

## 8. 거래 결과 확인

거래 내역은 Sepolia Etherscan에서 확인할 수 있습니다.

```text
https://sepolia.etherscan.io/address/지갑주소
```

현재 패키지의 구매 금액은 미리 설정된 교육용 판매자 지갑으로 전송됩니다.

- 일반 상품: 0.0001 Sepolia ETH
- 고가 상품: 0.05 Sepolia ETH
- 네트워크: Ethereum Sepolia
- Chain ID: 11155111

상품 가격 외에 가스비가 추가로 차감됩니다.

## 9. 알아둘 점

- 상품 보유 개수는 블록체인 NFT나 토큰 보유량이 아닙니다.
- 보유 개수는 지갑 주소별로 `PlayerPrefs`에 로컬 저장됩니다.
- 같은 PC와 프로젝트에서는 Play Mode를 다시 실행해도 유지됩니다.
- 프로젝트를 삭제하거나 PlayerPrefs를 초기화하면 보유 개수도 사라질 수 있습니다.
- 다른 PC나 프로젝트에는 보유 개수가 자동으로 공유되지 않습니다.
- 실제 서비스에서는 서버 또는 스마트 컨트랙트 기반 저장이 필요합니다.

## 10. 선택 사항: 설정 변경
<details><summary><strong>판매지갑주소 변경 </strong></summary>
  
단순 기능 테스트만 하는 학생은 이 부분을 변경하지 않아도 됩니다.

### 판매자 지갑 변경

Prefab에서 다음 오브젝트를 선택합니다.

```text
BlockchainShop > Controller > Web3AuthSample
```

Inspector의 `Shop Receiver Address`에 새로운 판매자 EVM 공개 주소를 입력합니다.

주소 형식:

```text
0x1234567890abcdef1234567890abcdef12345678
```

공개 주소만 입력해야 하며 개인 키나 복구 문구를 입력하면 안 됩니다.

### Web3Auth 프로젝트 변경

자신의 서비스로 구성하려는 경우에만 다음 항목을 변경합니다.

- MetaMask Developer Client ID
- Dashboard 허용 도메인 및 Redirect URL
- Unity Android Application Identifier
- Deep Link 설정
- 런타임 Redirect URL

Client ID, Application Identifier, Redirect URL과 Deep Link 설정은 서로 일치해야 합니다.

### 상품 설정 변경

상품명, 가격, 설명과 판매자 주소를 변경할 수 있습니다. 가격을 변경할 때는 실제 전송 금액과 잔액 부족 판정 기준도 함께 확인해야 합니다.
</details>

## 문제 해결

### 글자가 보이지 않거나 TMP 오류가 발생합니다

`Window > TextMeshPro > Import TMP Essential Resources`를 실행합니다.

### UI가 화면보다 너무 크게 표시됩니다

Canvas의 `Canvas Scaler`가 `Scale With Screen Size`인지 확인하고 Reference Resolution을 `1920 × 1080`으로 설정합니다.

### P 키가 작동하지 않습니다

`Active Input Handling`을 `Input System Package (New)` 또는 `Both`로 설정합니다.

### 로그인 후 Unity로 돌아오지 않습니다

MetaMask Developer Dashboard의 Redirect URL, Unity Application Identifier와 Deep Link 설정이 서로 일치하는지 확인합니다.

### 구매가 실패합니다

다음 항목을 확인합니다.

- 네트워크가 Sepolia인지
- 상품 가격과 가스비를 지불할 잔액이 있는지
- 인터넷과 RPC 연결이 정상인지
- 판매자 주소가 올바른 `0x` 공개 주소인지
- Unity Console에 빨간색 오류가 있는지

## 테스트 환경

- Unity 6000.5.7f1
- Windows 11
- Ethereum Sepolia
- Nethereum Unity 4.19.2
- Unity UI 2.5.0
- Input System 1.20.0
- Newtonsoft Json 3.2.2
