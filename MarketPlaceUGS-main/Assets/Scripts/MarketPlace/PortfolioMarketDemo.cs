using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.UI;

public class PortfolioMarketDemo : MonoBehaviour
{
    [Header("Economy IDs")]
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

    private bool isEconomyConfigSynced = false;

    private void Start()
    {
        if (refreshBtn != null) refreshBtn.onClick.AddListener(() => _ = RefreshAllAsync());
        if (giveEquipmentBtn != null) giveEquipmentBtn.onClick.AddListener(() => _ = GiveRandomItemAsync());
        if (addCoinBtn != null) addCoinBtn.onClick.AddListener(() => _ = AddCoinAsync(100));
        if (claimBtn != null) claimBtn.onClick.AddListener(() => _ = ClaimEarningsAsync());
        if (marketRefreshBtn != null) marketRefreshBtn.onClick.AddListener(() => _ = RefreshMarketAsync());
    }

    public async Task RefreshAllAsync()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetMessage("로그인 필요");
            return;
        }

        await EnsureEconomyConfigSyncedAsync();

        await RefreshCoinsAsync();
        var nftPanel = FindFirstObjectByType<SimpleMarket.MythicNftPanel>();
        if (nftPanel) await nftPanel.SyncInventoryAsync();
        await RefreshInventoryAsync();
        await RefreshMarketAsync();
    }

    private async Task EnsureEconomyConfigSyncedAsync()
    {
        if (isEconomyConfigSynced) return;

        try
        {
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
            isEconomyConfigSynced = true;
            Debug.Log("[Economy] Configuration synced");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("Economy Sync 실패 (Publish/환경/프로젝트 확인)");
        }
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
    // Economy: Coin
    // -------------------------
    public async Task RefreshCoinsAsync()
    {
        try
        {
            var balances = await EconomyService.Instance.PlayerBalances.GetBalancesAsync();
            long coin = 0;

            foreach (var b in balances.Balances)
            {
                if (b.CurrencyId == currencyId)
                {
                    coin = b.Balance;
                    break;
                }
            }

            if (coinText != null) coinText.text = coin.ToString();
        }
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage("코인 조회 실패 (Economy Publish/환경 확인)");
        }
    }

    private async Task AddCoinAsync(long amount)
    {
        try
        {
            await EnsureEconomyConfigSyncedAsync();

            int delta = ToSafeInt(amount);
            await EconomyService.Instance.PlayerBalances.IncrementBalanceAsync(currencyId, delta);
            await RefreshCoinsAsync();
            SetMessage($"코인 +{amount}");
        }
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage("코인 증가 실패 (Economy Publish/통화 ID 확인)");
        }
    }

    private int ToSafeInt(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return (int)value;
    }

    // -------------------------
    // Economy: Inventory
    // -------------------------
    public async Task RefreshInventoryAsync()
    {
        try
        {
            ClearChildren(inventoryContent);

            await EnsureEconomyConfigSyncedAsync();
            GetInventoryResult inv = await EconomyService.Instance.PlayerInventory.GetInventoryAsync();
            List<PlayersInventoryItem> items = new(inv.PlayersInventoryItems);
            while (inv.HasNext)
            {
                inv = await inv.GetNextAsync();
                items.AddRange(inv.PlayersInventoryItems);
            }

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
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage("인벤 조회 실패 (Economy Publish/로그인 상태 확인)");
        }
    }

    // -------------------------
    // Give Random: SWORD / REDPOTION / BLUEPOTION
    // -------------------------
    private async Task GiveRandomItemAsync()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetMessage("로그인 먼저");
            return;
        }

        await EnsureEconomyConfigSyncedAsync();

       

        string templateId = null;
        Debug.Log($"[GiveRandom] templateId='{templateId}'");

        try
        {
            if (itemVisuals == null || itemVisuals.items == null || itemVisuals.items.Count == 0) {
                SetMessage("오류: Item Visuals가 연결되지 않았거나 비었습니다.");
                Debug.LogError("Inspector에서 PortfolioMarketDemo의 'Item Visuals' 필드에 ScriptableObject를 연결했는지 확인하세요.");
                return;
            }

            SetMessage("랜덤 아이템 지급 요청 중...");

            // Paid/NFT visuals must not automatically join this legacy free test pool.
            var freeItems = itemVisuals.items.FindAll(x => x != null &&
                (x.id == "SWORD" || x.id == "REDPOTION" || x.id == "BLUEPOTION"));
            if (freeItems.Count == 0) { SetMessage("무료 테스트 아이템 목록이 비어 있습니다."); return; }
            int randomIndex = UnityEngine.Random.Range(0, freeItems.Count);
            string resourceId = freeItems[randomIndex].id;

            PlayersInventoryItem item = await EconomyService.Instance.PlayerInventory.AddInventoryItemAsync(resourceId);
 
            SetMessage($"지급 성공: {resourceId}");
            Debug.Log($"[GiveRandomItemAsync] {resourceId} 지급완료. InstanceID {item.PlayersInventoryItemId}");

            await RefreshInventoryAsync();
        }
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage($"지급 실패: '{templateId}' (Resource ID 확인)");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("지급 실패: 기타 예외");
        }
    }

    private string PickRandomId(string[] ids)
    {
        int index = UnityEngine.Random.Range(0, ids.Length);
        return (ids[index] ?? "").Trim();
    }

    // -------------------------
    // Cloud Code: Marketplace
    // -------------------------
    private async Task CreateListingAsync(string playersInventoryItemId, int price)
    {
        try
        {
            // ★ snake_case로 변경
            var args = new Dictionary<string, object>
            {
                { "players_inventory_item_id", playersInventoryItemId },
                { "price", price },
                { "currency_id", currencyId }
            };

            CreateListingResult res = await CloudCodeService.Instance.CallEndpointAsync<CreateListingResult>(
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
        try
        {
            ClearChildren(marketContent);

            var args = new Dictionary<string, object>
            {
                { "limit", marketLimit },
                { "sort", marketSort }
            };

            MarketListResult res = await CloudCodeService.Instance.CallEndpointAsync<MarketListResult>(
                "Mkt_GetActiveListings",
                args
            );

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

            BuyResult res = await CloudCodeService.Instance.CallEndpointAsync<BuyResult>(
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

            CancelResult res = await CloudCodeService.Instance.CallEndpointAsync<CancelResult>(
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

            ClaimResult res = await CloudCodeService.Instance.CallEndpointAsync<ClaimResult>(
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

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    // -------------------------
    // DTOs
    // -------------------------
    [Serializable]
    public class CreateListingResult { public string listingId; }

    [Serializable]
    public class MarketListResult { public ListingDto[] listings; }

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
    public class ClaimResult { public bool ok; public int claimed; }
}
