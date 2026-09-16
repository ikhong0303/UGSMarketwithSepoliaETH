using System.Linq;
using SimpleMarket;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MythicNftSetup
{
    [MenuItem("Simple Market/NFT - Connect Selected MythicNftPanel")]
    public static void Connect()
    {
        var go = Selection.activeGameObject;
        if (EditorApplication.isPlaying || !go || go.name != "MythicNftPanel" || !go.scene.IsValid())
        { Debug.LogError("Play 종료 후 Scene의 MythicNftPanel을 선택하세요."); return; }
        var panel = go.GetComponent<MythicNftPanel>() ?? Undo.AddComponent<MythicNftPanel>(go);
        Undo.RecordObject(panel, "Connect Mythic NFT UI");
        panel.couponInput = go.GetComponentsInChildren<TMP_InputField>(true).FirstOrDefault(x => x.name == "NftCouponInput");
        panel.statusText = go.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => x.name == "NftStatusText");
        var buttons = go.GetComponentsInChildren<Button>(true);
        panel.connectButton = buttons.FirstOrDefault(x => x.name == "NftConnectButton");
        panel.redeemButton = buttons.FirstOrDefault(x => x.name == "NftRedeemButton");
        panel.checkButton = buttons.FirstOrDefault(x => x.name == "NftCheckButton");
        var wallets = go.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ReownWalletBridge>(true)).ToArray();
        if (wallets.Length == 1) panel.wallet = wallets[0];
        EditorUtility.SetDirty(panel); EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("NFT UI 연결 완료. 빠진 필드는 Inspector에 직접 연결하고 Ctrl+S로 저장하세요.");
    }
}
