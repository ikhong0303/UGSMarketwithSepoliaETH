using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

public class UnityServiceInit : MonoBehaviour
{
    public static bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;
    private static Task initializationTask;

    private async void Awake()
    {
        try { await InitializeAsync(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return Task.CompletedTask;
        }

        if (initializationTask == null || initializationTask.IsCompleted)
            initializationTask = UnityServices.InitializeAsync(
                new InitializationOptions().SetEnvironmentName(MarketCloudClient.EnvironmentName));
        return initializationTask;
    }
}
