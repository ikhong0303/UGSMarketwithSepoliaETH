using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace ClassroomMarket;

public class ModuleSetup : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config) => config.AddGameApiClient();
}

public class Notifications
{
    private sealed class LastSent { public readonly SemaphoreSlim Gate = new(1); public long Revision = -1; public DateTime Time; }
    private static readonly ConcurrentDictionary<string, LastSent> Sent = new();

    [CloudCodeFunction("NotifyChanged")]
    public async Task<object> NotifyChanged(IExecutionContext context, IGameApiClient api, IPushClient push)
    {
        if (string.IsNullOrEmpty(context.PlayerId)) throw new Exception("LOGIN_REQUIRED");
        // Clients cannot supply notification text, recipients, balances or revision numbers.
        var last = Sent.GetOrAdd(context.ProjectId + ":" + context.EnvironmentId, _ => new LastSent());
        await last.Gate.WaitAsync();
        try
        {
            if ((DateTime.UtcNow - last.Time).TotalSeconds < 1) return new { sent = false };
            var response = await api.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, "classroom_market", new List<string> { "state" });
            var row = response.Data.Results.FirstOrDefault(x => x.Key == "state");
            if (row == null) throw new Exception("SETUP_REQUIRED");
            var state = JObject.FromObject(row.Value);
            if (state["players"]?[context.PlayerId] == null) throw new Exception("PLAYER_NOT_REGISTERED");
            long revision = state.Value<long>("revision");
            if (revision <= last.Revision) return new { sent = false };
            await push.SendProjectMessageAsync(context, "{\"revision\":" + revision + "}", "MarketChanged");
            last.Revision = revision; last.Time = DateTime.UtcNow;
            return new { sent = true };
        }
        finally { last.Gate.Release(); }
    }
}
