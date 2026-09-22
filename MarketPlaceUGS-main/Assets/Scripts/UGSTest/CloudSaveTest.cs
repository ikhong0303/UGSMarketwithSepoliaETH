using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserData { public List<string> items = new(); public int coins; }
// Retains the old scene button entry points, now using the same server-owned ledger.
public class CloudSaveTest : MonoBehaviour
{
    public GameObject[] itemprefab;
    public Transform itemParent;
    public int coins;
    public Button btn_addCoin;
    public TextMeshProUGUI coinText;
    public async Task LoadItem() { await GetCoin(); await LoadInventory(); }
    private async Task GetCoin()
    {
        var player = await MarketCloudClient.GetPlayer();
        coins = (int)Math.Min(int.MaxValue, player.balance);
        if (coinText) coinText.text = player.balance.ToString();
    }
    private async Task LoadInventory()
    {
        var player = await MarketCloudClient.GetPlayer();
        if (itemParent) for (int i = itemParent.childCount - 1; i >= 0; i--) Destroy(itemParent.GetChild(i).gameObject);
        foreach (var item in player.items) SpawnItem(item.InventoryItemId);
    }
    public async void OnAddCoin() { try { await AddCoin(); } catch (Exception e) { Debug.LogWarning(e); } }
    public async void OnAddItem(string id) { try { await AddItem(id); } catch (Exception e) { Debug.LogWarning(e); } }
    public async Task AddCoin()
    {
        await MarketCloudClient.Mutate<object>("Mkt_GrantDemo", new() { { "kind", "coin" } });
        await GetCoin();
    }
    public async Task AddItem(string id)
    {
        // Catalog selection is performed on the server; clients cannot mint paid/NFT items.
        await MarketCloudClient.Mutate<object>("Mkt_GrantDemo", new() { { "kind", "item" } });
        await LoadInventory();
    }
    private void SpawnItem(string id)
    {
        int index = id == "REDPOTION" ? 0 : id == "BLUEPOTION" ? 1 : -1;
        if (index >= 0 && itemprefab != null && index < itemprefab.Length && itemParent) Instantiate(itemprefab[index], itemParent);
    }
    public Task RemoveItem() => Task.CompletedTask;
}
