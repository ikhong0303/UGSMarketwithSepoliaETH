using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using UnityEngine;
using UnityEngine.UI;

public class PortfolioMarketDemo : MonoBehaviour
{
    [Header("Cloud Save Market")]
    [SerializeField] private string currencyId = "COIN";
    [SerializeField] private int defaultPrice = 100;

    [Header("Random Give Pool (Resource IDs)")]

    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI debugLine;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TMP_InputField priceInput;
    [SerializeField] private Button refreshBtn;
    [SerializeField] private Button giveEquipmentBtn;
    [SerializeField] private Button addCoinBtn;
    [SerializeField] private Button claimBtn;

    [Header("Inventory UI")]
    [SerializeField] private Transform inventoryContent;
    [SerializeField] private InventoryRowUI inventoryRowPrefab;

    [Header("Market UI")]
    [SerializeField] private Transform marketContent;
    [SerializeField] private MarketRowUI marketRowPrefab;
    [SerializeField] private Button marketRefreshBtn;

    [Header("Market Options")]
    [SerializeField] private int marketLimit = 30;
    [SerializeField] private string marketSort = "NEWEST"; // NEWEST / PRICE_ASC / PRICE_DESC

    [Header("Visuals")]
    [SerializeField] private ItemVisualData itemVisuals;

    private Task refreshTask;
    private int inventoryRefreshVersion;
    private int marketRefreshVersion;
    private MarketLiveUpdates live;

    private void Start()
    {
        ConfigureScrollableList(inventoryContent);
        ConfigureScrollableList(marketContent);
        live = gameObject.AddComponent<MarketLiveUpdates>();
        live.Changed += OnMarketChanged;
        if (refreshBtn != null) refreshBtn.onClick.AddListener(() => _ = RefreshAllAsync());
        if (giveEquipmentBtn != null) giveEquipmentBtn.onClick.AddListener(() => _ = GiveRandomItemAsync());
        if (addCoinBtn != null) addCoinBtn.onClick.AddListener(() => _ = AddCoinAsync(100));
        if (claimBtn != null) claimBtn.onClick.AddListener(() => _ = ClaimEarningsAsync());
        if (marketRefreshBtn != null) marketRefreshBtn.onClick.AddListener(() => _ = RefreshMarketAsync());
    }

    private void OnMarketChanged() { if (isActiveAndEnabled && (refreshTask == null || refreshTask.IsCompleted)) refreshTask = RefreshDataAsync(); }
    private void OnDestroy() { if (live != null) live.Changed -= OnMarketChanged; }
    public Task RefreshAllAsync()
    {
        if (refreshTask != null && !refreshTask.IsCompleted) return refreshTask;
        return refreshTask = RefreshDataAsync(true);
    }
    private async Task RefreshDataAsync(bool syncNft = false)
    {
        if (!AuthenticationService.Instance.IsSignedIn) return;
        await RefreshCoinsAsync();
        if (syncNft) { var nft = FindFirstObjectByType<SimpleMarket.MythicNftPanel>(); if (nft) await nft.SyncInventoryAsync(); }
        await RefreshInventoryAsync();
        await RefreshMarketAsync();
    }

    private void SetMessage(string message)
    {
        if (debugLine != null) debugLine.text = message;
        Debug.Log(message);
    }

    private int GetIntemPriceFromData(string resourceId)
    {
        if(itemVisuals != null) {
            var mapping = itemVisuals.GetMapping(resourceId);
            if(mapping != null && mapping.price > 0) {
                return mapping.price;
            }
        }
        return defaultPrice;
    }

    // -------------------------
    // Cloud Save: Coin
    // -------------------------
    public async Task RefreshCoinsAsync()
    {
        try { var p = await MarketCloudClient.GetPlayer(); if (coinText != null) coinText.text = p.balance.ToString(); }
        catch (Exception e) { Debug.LogWarning(e); SetMessage("코인 조회 실패: Cloud Save / Cloud Code 설정 확인"); }
    }
    private async Task AddCoinAsync(long amount)
    {
        try
        {
            await MarketCloudClient.Mutate<object>("Mkt_GrantDemo", new() { { "kind", "coin" } });
            await RefreshCoinsAsync(); SetMessage("수업용 코인 +100");
        }
        catch (Exception e) { Debug.LogWarning(e); SetMessage("지급 실패: demoEnabled 및 수업 지급 한도 확인"); }
    }

    // -------------------------
    // Cloud Save: Inventory
    // -------------------------
    public async Task RefreshInventoryAsync()
    {
        int version = ++inventoryRefreshVersion;
        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            var player = await MarketCloudClient.GetPlayer();
            if (this == null || version != inventoryRefreshVersion ||
                !AuthenticationService.Instance.IsSignedIn || AuthenticationService.Instance.PlayerId != playerId) return;
            ClearChildren(inventoryContent);
            List<MarketCloudClient.Item> items = player.items;

            foreach (var item in items)
            {
                if (inventoryRowPrefab == null || inventoryContent == null)
                {
                    Debug.Log($"Inventory: {item.InventoryItemId} / {item.PlayersInventoryItemId}");
                    continue;
                }

                InventoryRowUI row = Instantiate(inventoryRowPrefab, inventoryContent);

                Sprite icon = null;
                string displayName = item.InventoryItemId;
                int price = defaultPrice;

                if(itemVisuals != null) {
                    var mapping = itemVisuals.GetMapping(item.InventoryItemId);
                    if(mapping != null) {
                        icon = mapping.icon;
                        if (!string.IsNullOrEmpty(mapping.itemName)) displayName = mapping.itemName;

                        if (mapping.price > 0) price = mapping.price;
                    }
                }

                row.Bind(item, displayName, icon, price, CreateListingAsync);
            }

            SetMessage($"인벤 로드 완료: {items.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("인벤 조회 실패 (Cloud Code 배포/로그인 상태 확인)");
        }
    }

    // -------------------------
    // Give Random: SWORD / REDPOTION / BLUEPOTION
    // -------------------------
    private async Task GiveRandomItemAsync()
    {
        try
        {
            await MarketCloudClient.Mutate<object>("Mkt_GrantDemo", new() { { "kind", "item" } });
            await RefreshInventoryAsync();
        }
        catch (Exception e) { Debug.LogWarning(e); SetMessage("지급 실패: demoEnabled 및 수업 지급 한도 확인"); }
    }

    // -------------------------
    // Cloud Code: Marketplace
    // -------------------------
    private async Task CreateListingAsync(string playersInventoryItemId, int price)
    {
        try
        {
            if (priceInput != null && !string.IsNullOrWhiteSpace(priceInput.text))
            {
                if (!int.TryParse(priceInput.text, out price) || price < 1 || price > 1000000)
                { SetMessage("가격은 1~1,000,000 정수로 입력하세요."); return; }
            }
            var args = new Dictionary<string, object>
            {
                { "players_inventory_item_id", playersInventoryItemId },
                { "price", price },
                { "currency_id", currencyId }
            };

            CreateListingResult res = await MarketCloudClient.Mutate<CreateListingResult>(
                "Mkt_CreateListing",
                args
            );

            SetMessage($"등록 완료: {res.listingId}");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("등록 실패 (Cloud Code/권한/스크립트명/환경 확인)");
        }
    }

    private async Task RefreshMarketAsync()
    {
        int version = ++marketRefreshVersion;
        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            var args = new Dictionary<string, object>
            {
                { "limit", marketLimit },
                { "sort", marketSort }
            };

            MarketListResult res = await CloudCodeService.Instance.CallEndpointAsync<MarketListResult>(
                "Mkt_GetActiveListings",
                args
            );
            if (this == null || version != marketRefreshVersion ||
                !AuthenticationService.Instance.IsSignedIn || AuthenticationService.Instance.PlayerId != playerId) return;
            ClearChildren(marketContent);

            if (res.listings == null)
            {
                SetMessage("거래소 목록 0개");
                return;
            }

            foreach (var listing in res.listings)
            {
                if (marketRowPrefab == null || marketContent == null)
                {
                    Debug.Log($"Listing: {listing.listingId} price={listing.price}");
                    continue;
                }

                MarketRowUI row = Instantiate(marketRowPrefab, marketContent);

                Sprite icon = null;
                string displayName = listing.inventoryItemId;

                if(itemVisuals != null) {
                    var mapping = itemVisuals.GetMapping(listing.inventoryItemId);
                    if(mapping != null) {
                        icon = mapping.icon;
                        if (!string.IsNullOrEmpty(mapping.itemName)) displayName = mapping.itemName;
                    }
                }


                row.Bind(listing, displayName, icon, BuyListingAsync, CancelListingAsync);
            }

            SetMessage($"거래소 로드: {res.listings.Length}개");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("거래소 조회 실패 (Cloud Code/스크립트명 확인)");
        }
    }

    private async Task BuyListingAsync(string listingId)
    {
        try
        {
            // ★ snake_case로 변경
            var args = new Dictionary<string, object> { { "listing_id", listingId } };

            BuyResult res = await MarketCloudClient.Mutate<BuyResult>(
                "Mkt_BuyListing",
                args
            );

            SetMessage($"구매 완료: newInstance={res.newPlayersInventoryItemId}");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("구매 실패 (코인 부족/Listing 상태/Cloud Code 확인)");
        }
    }

    private async Task CancelListingAsync(string listingId)
    {
        try
        {
            // ★ snake_case로 변경
            var args = new Dictionary<string, object> { { "listing_id", listingId } };

            CancelResult res = await MarketCloudClient.Mutate<CancelResult>(
                "Mkt_CancelListing",
                args
            );

            SetMessage($"취소 완료: returnedInstance={res.returnedPlayersInventoryItemId}");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("취소 실패 (판매자만 가능/상태 확인)");
        }
    }

    private async Task ClaimEarningsAsync()
    {
        try
        {
            // ★ snake_case로 변경
            var args = new Dictionary<string, object> { { "currency_id", currencyId } };

            ClaimResult res = await MarketCloudClient.Mutate<ClaimResult>(
                "Mkt_ClaimEarnings",
                args
            );

            SetMessage($"정산 수령: {res.claimed}");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("정산 실패 (Cloud Code/스크립트명 확인)");
        }
    }

    private static void ConfigureScrollableList(Transform content)
    {
        if (!(content is RectTransform rect)) return;
        // Grow downwards from the viewport top using the actual row prefab heights.
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
        var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll && scroll.content == rect)
        {
            scroll.horizontal = false;
            scroll.vertical = true;
        }
        LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            parent.GetChild(i).gameObject.SetActive(false);
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    // -------------------------
    // DTOs
    // -------------------------
    [Serializable]
    public class CreateListingResult { public string listingId; }

    [Serializable]
    public class MarketListResult { public long revision; public ListingDto[] listings; }

    [Serializable]
    public class ListingDto
    {
        public string listingId;
        public string status;
        public string sellerPlayerId;
        public string inventoryItemId;
        public Dictionary<string, object> instanceData;
        public string currencyId;
        public int price;
        public long createdAt;
    }

    [Serializable]
    public class BuyResult { public bool ok; public string newPlayersInventoryItemId; }

    [Serializable]
    public class CancelResult { public bool ok; public string returnedPlayersInventoryItemId; }

    [Serializable]
    public class ClaimResult { public bool ok; public long claimed; }
}
