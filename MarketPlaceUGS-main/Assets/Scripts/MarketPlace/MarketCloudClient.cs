using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using UnityEngine;

public static class MarketCloudClient
{
    public static string EnvironmentName = "production";
    [Serializable] public class Item
    {
        public string inventoryItemId, playersInventoryItemId;
        public Dictionary<string, object> instanceData;
        public string InventoryItemId => inventoryItemId;
        public string PlayersInventoryItemId => playersInventoryItemId;
    }
    [Serializable] public class Player { public long balance; public List<Item> items = new(); }
    public static Task<Player> GetPlayer() => CloudCodeService.Instance.CallEndpointAsync<Player>("Mkt_GetPlayer", new());

    public static async Task<T> Mutate<T>(string endpoint, Dictionary<string, object> args)
    {
        // Persist the request before sending. Lost responses/restarts replay the same operation.
        var payload = new Dictionary<string, object>(args);
        payload.Remove("request_id");
        using var sha = SHA256.Create();
        string scope = Application.cloudProjectId + ":" + EnvironmentName + ":" + AuthenticationService.Instance.PlayerId;
        string key = "market-v2:" + scope + ":" + Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(endpoint + JsonConvert.SerializeObject(payload))));
        string requestId = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(requestId)) { requestId = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(key, requestId); PlayerPrefs.Save(); }
        payload["request_id"] = requestId;
        T result = await CloudCodeService.Instance.CallEndpointAsync<T>(endpoint, payload);
        PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
        MarketLiveUpdates.NotifyCommitted();
        return result;
    }
}
