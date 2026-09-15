using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.UI;

public class InventoryRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI instanceText;
    [SerializeField] private TextMeshProUGUI optionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button sellBtn;

    private string playersInventoryItemId;
    private int sellPrice;
    private Func<string, int, Task> createListingAsync;

    public void Bind(
        PlayersInventoryItem item,
        string displayName,
        Sprite icon,
        int price,
        Func<string, int, Task> createListingFunc)
    {
        // ★ 디버그: 전달받은 값 확인
        Debug.Log($"[InventoryRowUI.Bind] InventoryItemId: {item.InventoryItemId}");
        Debug.Log($"[InventoryRowUI.Bind] PlayersInventoryItemId: {item.PlayersInventoryItemId}");

        playersInventoryItemId = item.PlayersInventoryItemId;
        sellPrice = price;
        createListingAsync = createListingFunc;

        if (titleText != null) {
            titleText.text = !string.IsNullOrEmpty(displayName) ? displayName : item.InventoryItemId;
        }

        if(iconImage != null) {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        if(priceText != null) {
            priceText.text = $"Coin: {sellPrice.ToString()}"; 
        }

        string shortInstance = !string.IsNullOrEmpty(playersInventoryItemId) && playersInventoryItemId.Length > 8
            ? playersInventoryItemId.Substring(0, 8)
            : playersInventoryItemId ?? "(null)";

        

        if (instanceText != null) instanceText.text = $"instance: {shortInstance}";

        if (optionText != null) optionText.text = "option: -";

        if (sellBtn != null)
        {
            sellBtn.onClick.RemoveAllListeners();
            sellBtn.onClick.AddListener(() => { _ = SellAsync(); });
        }
    }

    private async Task SellAsync()
    {
        // ★ 디버그: Sell 시점에 ID 확인
        Debug.Log($"[InventoryRowUI.SellAsync] playersInventoryItemId: '{playersInventoryItemId}'");

        if (string.IsNullOrEmpty(playersInventoryItemId))
        {
            Debug.LogError("[SellAsync] playersInventoryItemId is null or empty!");
            return;
        }

        if (createListingAsync == null)
        {
            Debug.LogError("[SellAsync] createListingAsync is null!");
            return;
        }

        
        Debug.Log($"[SellAsync] Calling createListingAsync with ID: {playersInventoryItemId}, Price: {sellPrice}");

        await createListingAsync.Invoke(playersInventoryItemId, sellPrice);
    }

    private static string TryGetString(Dictionary<string, object> data, string key, string defaultValue)
    {
        if (data == null) return defaultValue;
        if (!data.TryGetValue(key, out object value)) return defaultValue;
        return value != null ? value.ToString() : defaultValue;
    }

    private static int TryGetInt(Dictionary<string, object> data, string key, int defaultValue)
    {
        if (data == null) return defaultValue;
        if (!data.TryGetValue(key, out object value)) return defaultValue;

        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (int.TryParse(value.ToString(), out int parsed)) return parsed;

        return defaultValue;
    }
}