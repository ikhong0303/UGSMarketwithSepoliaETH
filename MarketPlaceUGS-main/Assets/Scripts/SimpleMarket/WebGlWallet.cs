using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace SimpleMarket
{
    // The private key stays in MetaMask. Unity receives only a public address / tx hash.
    public sealed class WebGlWallet : MonoBehaviour
    {
        [Serializable] public class Request
        {
            public string id, action, storageKey, to, value, data, from, txHash;
        }
        [Serializable] public class Result
        {
            public string id, address, chainId, balanceEth, txHash, error, status, signature;
        }
        private readonly Dictionary<string, TaskCompletionSource<Result>> requests = new();
        public event Action Changed;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void SimpleMarketWalletRequest(string target, string json);
#endif
        public async Task<Result> Call(Request request)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            request.id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<Result>();
            requests.Add(request.id, completion);
            try
            {
                SimpleMarketWalletRequest(gameObject.name, JsonUtility.ToJson(request));
                var timeout = Task.Delay(TimeSpan.FromMinutes(3));
                if (await Task.WhenAny(completion.Task, timeout) != completion.Task)
                    throw new Exception("지갑 응답 대기 시간이 지났습니다. 새 결제 전에 '결제 확인'을 눌러주세요.");
                return await completion.Task;
            }
            finally { requests.Remove(request.id); }
#else
            await Task.Yield();
            throw new Exception("지갑은 WebGL 빌드를 PC 크롬에서 실행해야 사용할 수 있습니다. Editor에서는 UGS 기능을 테스트하세요.");
#endif
        }
        // Called by the .jslib plugin using SendMessage.
        public void OnWalletResult(string json)
        {
            var result = JsonUtility.FromJson<Result>(json);
            if (result.id == "event") { Changed?.Invoke(); return; }
            if (!requests.TryGetValue(result.id, out var completion)) return;
            if (!string.IsNullOrEmpty(result.error)) completion.TrySetException(new Exception(result.error));
            else completion.TrySetResult(result);
        }
        private void OnDestroy()
        {
            foreach (var completion in requests.Values) completion.TrySetCanceled();
            requests.Clear();
        }
    }
}
