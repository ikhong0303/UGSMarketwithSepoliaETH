using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SimpleMarket
{
    // Uses the original project's Authentication, Economy IDs and Mkt_* endpoint contracts.
    // UI is created here so the setup requires no manual Inspector button wiring.
    public sealed class SimpleMarketApp : MonoBehaviour
    {
        public TMP_FontAsset font;
        public ItemVisualData itemVisuals;
        public string environmentName = "production";
        private WebGlWallet wallet;
        private TMP_InputField username, password, receiptInput;
        private TextMeshProUGUI message, balanceText, walletText, playerText, inventoryTitle;
        private Transform inventoryRows, marketRows;
        private GameObject loginPanel, gamePanel;
        private readonly List<Button> buttons = new();
        private Button buyGoldButton;
        private bool busy, initialized, walletOnSepolia;
        private string address = "", pendingHash = "";
        private bool hasPending;
        private string storageKey;
        private static readonly Color Ink = new(0.9f, 0.94f, 1f);
        private static readonly Color PanelColor = new(0.10f, 0.14f, 0.21f);
        private bool SignedIn => initialized && AuthenticationService.Instance.IsSignedIn;

        [Serializable] private class Quote { public string chainId, to, value, data, priceEth; public int goldAmount, confirmations; }
        [Serializable] private class Claim { public string status, txHash; public int goldAmount; }
        [Serializable] private class Gacha { public bool ok; public string inventoryItemId; }

        private async void Start()
        {
            BuildUi();
            wallet = gameObject.AddComponent<WebGlWallet>();
            wallet.Changed += WalletChanged;
            await Run(async () =>
            {
                var options = new InitializationOptions().SetEnvironmentName(environmentName);
                await UnityServices.InitializeAsync(options);
                initialized = true;
                AuthenticationService.Instance.Expired += SessionExpired;
                SetMessage("준비 완료. 회원가입 또는 로그인하세요. 신규 계정: 아이템 0개 / 골드 1,000");
            });
        }
        private void OnDestroy()
        {
            if (wallet != null) wallet.Changed -= WalletChanged;
            if (initialized) AuthenticationService.Instance.Expired -= SessionExpired;
        }
        private void SessionExpired()
        {
            address = ""; walletOnSepolia = false;
            gamePanel.SetActive(false); loginPanel.SetActive(true);
            SetMessage("로그인이 만료되었습니다. 다시 로그인한 뒤 결제 확인을 눌러주세요.");
        }
        private void WalletChanged()
        {
            // Immediately invalidate the displayed account. Never carry a quote across account changes.
            address = ""; walletOnSepolia = false;
            walletText.text = "지갑 계정 또는 네트워크가 바뀌었습니다. 지갑 연결을 다시 눌러주세요.";
            UpdateButtons();
        }
        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true; UpdateButtons();
            try { await action(); }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
                string text = e.Message;
                if (text.Contains("REVIEW_REQUIRED")) text = "처리 결과를 관리자가 확인해야 합니다. 거래 해시를 보관하고 추가 결제하지 마세요.";
                else if (text.Contains("INSUFFICIENT_GOLD")) text = "골드가 부족합니다.";
                else if (text.Contains("SETUP_REQUIRED")) text = "UGS 설정이 아직 완료되지 않았습니다. 안내서의 Economy / Cloud Save / Cloud Code 단계를 확인하세요.";
                else if (text.Contains("LISTING_NOT_ACTIVE")) text = "이미 판매되었거나 처리 중인 상품입니다. 새로고침하세요.";
                SetMessage(text);
            }
            finally { busy = false; UpdateButtons(); }
        }
        private void UpdateButtons()
        {
            foreach (var button in buttons) if (button != null) button.interactable = !busy;
            if (buyGoldButton != null) buyGoldButton.interactable = !busy && SignedIn && walletOnSepolia && !string.IsNullOrEmpty(address) && !hasPending;
        }
        private void RequireLogin()
        {
            if (!SignedIn) throw new Exception("로그인이 필요합니다.");
        }
        private async Task Authenticate(bool register)
        {
            if (!initialized) throw new Exception("UGS 초기화에 실패했습니다. 프로젝트 연결을 확인한 뒤 Play를 다시 실행하세요.");
            string user = username.text.Trim(), pass = password.text;
            if (user.Length == 0 || pass.Length == 0) throw new Exception("아이디와 비밀번호를 입력하세요.");
            if (register) await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(user, pass);
            else await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(user, pass);
            password.text = "";
            storageKey = Application.cloudProjectId + ":" + environmentName + ":" + AuthenticationService.Instance.PlayerId;
            loginPanel.SetActive(false); gamePanel.SetActive(true);
            playerText.text = "계정: " + user;
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
            await Refresh(); // First balance read initializes the Dashboard's 1000 starting gold.
#if UNITY_WEBGL && !UNITY_EDITOR
            await LoadPending();
#endif
            SetMessage(register ? "회원가입 완료. 기본 골드를 확인하세요." : "로그인 완료.");
        }
        private Task Logout()
        {
            AuthenticationService.Instance.SignOut();
            address = ""; walletOnSepolia = false; pendingHash = ""; hasPending = false;
            loginPanel.SetActive(true); gamePanel.SetActive(false);
            walletText.text = "지갑 미연결 · 결제할 때만 연결하면 됩니다.";
            receiptInput.text = "";
            SetMessage("로그아웃했습니다. 다른 계정으로 거래소를 테스트할 수 있습니다.");
            return Task.CompletedTask;
        }
        private async Task<T> Endpoint<T>(string name, Dictionary<string, object> args = null)
        {
            RequireLogin();
            try { return await CloudCodeService.Instance.CallEndpointAsync<T>(name, args ?? new Dictionary<string, object>()); }
            catch (CloudCodeException e)
            {
                // SDK 2.10.2 puts the script's error in ToString(), not Message.
                string details = e.ToString();
                string[] codes = { "REVIEW_REQUIRED", "TRANSACTION_FAILED", "INSUFFICIENT_GOLD", "SETUP_REQUIRED", "LISTING_NOT_ACTIVE", "WRONG_PLAYER", "INVALID_AMOUNT", "INVALID_RECEIVER", "INVALID_SENDER", "WRONG_CHAIN", "INVALID_TX_HASH", "INVALID_CONFIG", "RPC_UNAVAILABLE", "RECEIVER_MUST_BE_EOA", "LEDGER_FULL", "BUSY", "ALREADY_LISTED" };
                foreach (string code in codes) if (details.Contains(code)) throw new Exception(code);
                throw new Exception($"서버 요청 실패 ({name}, {e.Reason}). Cloud Code Publish와 파라미터를 확인하세요.");
            }
        }
        private async Task Refresh()
        {
            RequireLogin();
            await RefreshBalance();
            var inventory = await EconomyService.Instance.PlayerInventory.GetInventoryAsync();
            var items = new List<Unity.Services.Economy.Model.PlayersInventoryItem>(inventory.PlayersInventoryItems);
            while (inventory.HasNext) { inventory = await inventory.GetNextAsync(); items.AddRange(inventory.PlayersInventoryItems); }
            ClearRows(inventoryRows);
            inventoryTitle.text = $"보유 아이템 · {items.Count}개";
            foreach (var item in items)
            {
                var mapping = itemVisuals != null ? itemVisuals.GetMapping(item.InventoryItemId) : null;
                int price = mapping != null && mapping.price > 0 ? mapping.price : 100;
                string instanceId = item.PlayersInventoryItemId;
                var row = Row(inventoryRows, ItemName(item.InventoryItemId) + $"  ·  {price:N0}골드");
                AddButton(row, "판매 등록", () => Run(async () =>
                {
                    await Endpoint<PortfolioMarketDemo.CreateListingResult>("Mkt_CreateListing", new() { { "players_inventory_item_id", instanceId }, { "price", price }, { "currency_id", "COIN" } });
                    await Refresh(); SetMessage("거래소에 등록했습니다. 팔리면 판매대금 받기를 눌러주세요.");
                }), 110);
            }
            if (items.Count == 0) Label(inventoryRows, "아직 아이템이 없습니다. 가챠로 첫 아이템을 획득하세요.", 18, 65);
            var result = await Endpoint<PortfolioMarketDemo.MarketListResult>("Mkt_GetActiveListings", new() { { "limit", 50 }, { "sort", "NEWEST" } });
            ClearRows(marketRows);
            foreach (var item in result.listings ?? Array.Empty<PortfolioMarketDemo.ListingDto>())
            {
                bool mine = item.sellerPlayerId == AuthenticationService.Instance.PlayerId;
                string id = item.listingId;
                var row = Row(marketRows, ItemName(item.inventoryItemId) + $"  ·  {item.price:N0}골드" + (mine ? " (내 상품)" : ""));
                AddButton(row, mine ? "판매 취소" : "구매", () => Run(async () =>
                {
                    if (mine) await Endpoint<PortfolioMarketDemo.CancelResult>("Mkt_CancelListing", new() { { "listing_id", id } });
                    else await Endpoint<PortfolioMarketDemo.BuyResult>("Mkt_BuyListing", new() { { "listing_id", id } });
                    await Refresh(); SetMessage(mine ? "판매를 취소했습니다." : "아이템 구매 완료.");
                }), 110);
            }
            if (result.listings == null || result.listings.Length == 0) Label(marketRows, "등록된 상품이 없습니다. 보유 아이템을 판매해 보세요.", 18, 65);
        }
        private async Task RefreshBalance()
        {
            var result = await EconomyService.Instance.PlayerBalances.GetBalancesAsync();
            foreach (var item in result.Balances) if (item.CurrencyId == "COIN") { balanceText.text = $"골드  {item.Balance:N0}"; return; }
            throw new Exception("SETUP_REQUIRED: COIN 통화가 없습니다.");
        }
        private async Task Connect()
        {
            RequireLogin();
            var result = await wallet.Call(new() { action = "connect" });
            address = result.address;
            walletOnSepolia = string.Equals(result.chainId, "0xaa36a7", StringComparison.OrdinalIgnoreCase);
            walletText.text = address + "\nSepolia ETH: " + result.balanceEth;
            await LoadPending();
            SetMessage(hasPending ? "확인할 결제가 있습니다. 결제 확인을 눌러주세요." : "지갑 연결 완료. 구매 금액과 가스비는 MetaMask에서 확인하세요.");
        }
        private async Task LoadPending()
        {
            var result = await wallet.Call(new() { action = "pending", storageKey = storageKey });
            pendingHash = result.txHash ?? ""; hasPending = result.status == "PENDING";
            if (!string.IsNullOrEmpty(pendingHash)) receiptInput.text = pendingHash;
        }
        private async Task BuyGold()
        {
            RequireLogin();
            await LoadPending();
            if (hasPending) throw new Exception("이전 결제를 먼저 확인하세요.");
            var quote = await Endpoint<Quote>("Gold_GetQuote");
            if (quote.goldAmount != 10000 || quote.priceEth != "0.0001" || quote.chainId != "0xaa36a7") throw new Exception("서버 상품 설정을 확인하세요.");
            SetMessage("MetaMask에서 0.0001 Sepolia ETH + 가스비를 승인하세요.");
            try
            {
                var result = await wallet.Call(new() { action = "send", storageKey = storageKey, from = address, to = quote.to, value = quote.value, data = quote.data });
                pendingHash = result.txHash; hasPending = true; receiptInput.text = pendingHash;
                SetMessage("전송 완료. 세폴리아 승인과 UGS 지급을 확인하고 있습니다...");
                // Server calls stay short. The browser polls; it never sends a second transaction.
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    if (await CheckReceipt()) return;
                    await Task.Delay(8000);
                }
                SetMessage("아직 승인 대기 중입니다. 잠시 후 결제 확인을 누르세요. 다시 결제하지 않아도 됩니다.");
            }
            finally { await LoadPending(); }
        }
        private async Task<bool> CheckReceipt()
        {
            Claim result;
            try { result = await Endpoint<Claim>("Gold_Claim", new() { { "tx_hash", pendingHash } }); }
            catch (Exception e) when (e.Message.Contains("TRANSACTION_FAILED"))
            {
                await wallet.Call(new() { action = "clear", storageKey = storageKey, txHash = pendingHash });
                hasPending = false; pendingHash = ""; receiptInput.text = "";
                throw new Exception("블록체인에서 실패한 거래입니다. 골드는 지급되지 않았습니다. MetaMask에서 가스비 내역을 확인하세요.");
            }
            if (result.status == "PENDING") return false;
            if (result.status != "GRANTED") throw new Exception("REVIEW_REQUIRED");
            await wallet.Call(new() { action = "clear", storageKey = storageKey, txHash = pendingHash });
            hasPending = false; pendingHash = ""; receiptInput.text = "";
            await RefreshBalance();
            SetMessage("결제 확인 완료. 10,000골드가 UGS에 지급되었습니다.");
            return true;
        }
        private async Task Recover()
        {
            RequireLogin();
            string manualHash = receiptInput.text.Trim();
            await LoadPending();
            if (!string.IsNullOrEmpty(manualHash) && manualHash != pendingHash)
            {
                await wallet.Call(new() { action = "remember", storageKey = storageKey, txHash = manualHash });
                pendingHash = manualHash; hasPending = true;
            }
            if (string.IsNullOrEmpty(pendingHash)) throw new Exception("거래 해시가 없습니다. MetaMask 활동의 거래 해시를 입력하세요. 승인 요청이 열려 있다면 먼저 확인하세요.");
            if (!await CheckReceipt()) SetMessage("세폴리아 승인 대기 중입니다. 잠시 후 다시 확인하세요.");
        }
        private string ItemName(string id) => itemVisuals != null ? itemVisuals.GetMapping(id)?.itemName ?? id : id;
        private void SetMessage(string text) { if (message != null) message.text = text; }

        private void BuildUi()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvas = new GameObject("Simple Market Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 900); scaler.matchWidthOrHeight = 0.5f;
            var root = Box(canvas.transform, "Root", new Color(0.055f, 0.075f, 0.12f));
            Stretch(root.GetComponent<RectTransform>(), 20);
            Vertical(root, 12, 20);
            Label(root, "SIMPLE RPG MARKET", 30, 44);
            Label(root, "UGS 아이템 거래소  /  MetaMask · Sepolia 테스트 결제", 17, 26);
            message = Label(root, "UGS 연결 준비 중...", 18, 75);

            loginPanel = Box(root, "Login", PanelColor).gameObject;
            Vertical(loginPanel.transform, 12, 18);
            Label(loginPanel.transform, "게임 계정으로 시작하기", 24, 40);
            username = Input(loginPanel.transform, "아이디 (영문/숫자)");
            password = Input(loginPanel.transform, "비밀번호 · 8~30자, 대/소문자·숫자·기호 포함");
            password.contentType = TMP_InputField.ContentType.Password;
            var authRow = Horizontal(loginPanel.transform, "Account actions", 52);
            AddButton(authRow, "회원가입", () => Run(() => Authenticate(true)), 170);
            AddButton(authRow, "로그인", () => Run(() => Authenticate(false)), 170);

            gamePanel = Box(root, "Game", PanelColor).gameObject;
            Vertical(gamePanel.transform, 10, 14);
            gamePanel.AddComponent<LayoutElement>().flexibleHeight = 1;
            var top = Horizontal(gamePanel.transform, "Balances", 40);
            playerText = Label(top, "계정", 18, 40); Flex(playerText.gameObject);
            balanceText = Label(top, "골드 -", 25, 40); Flex(balanceText.gameObject);
            AddButton(top, "로그아웃", () => Run(Logout), 105);
            var actions = Horizontal(gamePanel.transform, "Market actions", 44);
            AddButton(actions, "새로고침", () => Run(Refresh), 145);
            AddButton(actions, "가챠 · 100골드", () => Run(async () =>
            {
                var item = await Endpoint<Gacha>("Mkt_Gacha", new() { { "request_id", Guid.NewGuid().ToString("N") } });
                await Refresh(); SetMessage(ItemName(item.inventoryItemId) + " 획득! (각 아이템 확률 1/3)");
            }), 185);
            AddButton(actions, "판매대금 받기", () => Run(async () =>
            {
                var result = await Endpoint<PortfolioMarketDemo.ClaimResult>("Mkt_ClaimEarnings", new() { { "currency_id", "COIN" } });
                await RefreshBalance(); SetMessage($"판매대금 {result.claimed:N0}골드 수령.");
            }), 185);
            var lists = Horizontal(gamePanel.transform, "Items", 240);
            lists.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1;
            inventoryRows = Scroll(lists, "보유 아이템 · 0개", out inventoryTitle);
            marketRows = Scroll(lists, "거래소 · 최근 50개", out _);
            walletText = Label(gamePanel.transform, "지갑 미연결 · 결제할 때만 연결하면 됩니다.", 17, 50);
            var walletActions = Horizontal(gamePanel.transform, "Wallet actions", 45);
            AddButton(walletActions, "지갑 연결", () => Run(Connect), 150);
            buyGoldButton = AddButton(walletActions, "10,000골드 구매 · 0.0001 ETH + 가스비", () => Run(BuyGold), 435);
            AddButton(walletActions, "결제 확인", () => Run(Recover), 145);
            AddButton(walletActions, "기록 해제", () => Run(async () =>
            {
                RequireLogin();
                await wallet.Call(new() { action = "forget", storageKey = storageKey });
                hasPending = false; pendingHash = "";
                SetMessage("로컬 확인 기록만 해제했습니다. 환불·거래 취소는 아닙니다. 재결제 전 MetaMask 활동을 확인하세요. 이전 거래는 해시를 입력해 확인할 수 있습니다.");
            }), 125);
            receiptInput = Input(gamePanel.transform, "복구용 거래 해시 (0x...) · 전송 후 자동 저장됩니다");
            gamePanel.SetActive(false);
        }
        private Transform Box(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color; return go.transform;
        }
        private void Vertical(Transform target, float spacing, int padding)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing; layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
        }
        private Transform Horizontal(Transform parent, string name, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)); row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = row.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 10; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            return row.transform;
        }
        private TextMeshProUGUI Label(Transform parent, string text, float size, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            label.fontSize = size; label.color = Ink; label.text = text; label.raycastTarget = false; label.richText = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            go.GetComponent<LayoutElement>().preferredHeight = height; return label;
        }
        private TMP_InputField Input(Transform parent, string placeholder)
        {
            var box = Box(parent, "Input", new Color(0.16f, 0.20f, 0.28f));
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = 45;
            var input = box.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = box.GetComponent<Image>();
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)); area.transform.SetParent(box, false);
            Stretch(area.GetComponent<RectTransform>(), 9);
            var value = Label(area.transform, "", 18, 45); Stretch(value.rectTransform, 0);
            var hint = Label(area.transform, placeholder, 17, 45); Stretch(hint.rectTransform, 0); hint.color = new Color(0.6f, 0.66f, 0.75f);
            input.textViewport = area.GetComponent<RectTransform>(); input.textComponent = value; input.placeholder = hint;
            input.characterLimit = 160; input.lineType = TMP_InputField.LineType.SingleLine; return input;
        }
        private Button AddButton(Transform parent, string title, Func<Task> clicked, float width)
        {
            var box = Box(parent, title, new Color(0.18f, 0.34f, 0.53f));
            box.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            var button = box.gameObject.AddComponent<Button>(); button.targetGraphic = box.GetComponent<Image>();
            button.interactable = !busy;
            var text = Label(box, title, 17, 44); text.alignment = TextAlignmentOptions.Center; Stretch(text.rectTransform, 4);
            button.onClick.AddListener(() => { _ = clicked(); }); buttons.Add(button); return button;
        }
        private Transform Row(Transform parent, string title)
        {
            var row = Horizontal(parent, "Item", 50);
            var name = Label(row, title, 17, 50); Flex(name.gameObject); return row;
        }
        private Transform Scroll(Transform parent, string title, out TextMeshProUGUI heading)
        {
            var panel = Box(parent, title, new Color(0.075f, 0.105f, 0.16f)); Flex(panel.gameObject); Vertical(panel, 5, 10);
            heading = Label(panel, title, 21, 38);
            var scroll = Box(panel, "Scroll", new Color(0, 0, 0, 0)); Flex(scroll.gameObject);
            scroll.GetComponent<LayoutElement>().flexibleHeight = 1;
            var viewport = Box(scroll, "Viewport", new Color(0, 0, 0, 0)); Stretch(viewport.GetComponent<RectTransform>(), 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(viewport, false);
            var rect = content.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = Vector2.zero;
            Vertical(content.transform, 8, 0); content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var view = scroll.gameObject.AddComponent<ScrollRect>(); view.viewport = viewport.GetComponent<RectTransform>(); view.content = rect; view.horizontal = false; view.scrollSensitivity = 25;
            return content.transform;
        }
        private static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(margin, margin); rect.offsetMax = new Vector2(-margin, -margin);
        }
        private static void Flex(GameObject target)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>(); layout.flexibleWidth = 1;
        }
        private void ClearRows(Transform parent)
        {
            foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            buttons.RemoveAll(x => x == null);
        }
    }
}
