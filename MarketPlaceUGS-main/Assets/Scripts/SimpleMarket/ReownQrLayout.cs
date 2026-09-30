using System;
#if SIMPLE_MARKET_REOWN && !UNITY_WEBGL
using Reown.AppKit.Unity;
using Reown.AppKit.Unity.Components;
using UnityEngine;
using UnityEngine.UIElements;
#endif

namespace SimpleMarket
{
    internal static class ReownQrLayout
    {
        // Change the live view, not PackageCache assets which Unity can replace.
        public static Action Enlarge()
        {
#if SIMPLE_MARKET_REOWN && !UNITY_WEBGL
            if (AppKit.ModalController is not ModalControllerUtk controller) return () => { };
            var root = controller.UIDocument.rootVisualElement;
            var modal = controller.Modal;
            var originalWidth = modal.style.width;
            QrCode changedQr = null;
            StyleLength oldQrWidth = default, oldQrHeight = default;
            Image changedImage = null;
            FilterMode oldFilter = default;
            Label changedSubtitle = null;
            string oldSubtitle = null;
            bool applied = false;
            var resize = modal.schedule.Execute(() =>
            {
                if (applied) return;
                var qr = root.Q<QrCode>();
                if (qr == null || !float.IsFinite(qr.resolvedStyle.width) || qr.resolvedStyle.width <= 0 ||
                    !float.IsFinite(modal.body.resolvedStyle.height) || modal.body.resolvedStyle.height <= qr.resolvedStyle.height)
                    return;
                float qrSize = qr.resolvedStyle.width;
                float horizontalChrome = modal.resolvedStyle.width - qrSize;
                float verticalChrome = modal.header.resolvedStyle.height + modal.body.resolvedStyle.height - qr.resolvedStyle.height;
                // Double both QR dimensions, constrained only by the visible Game view.
                float size = Mathf.Min(qrSize * 2f, root.resolvedStyle.width - horizontalChrome - 24f,
                    root.resolvedStyle.height - verticalChrome - 24f);
                if (size > qrSize) modal.style.width = size + horizontalChrome;
                changedQr = qr;
                oldQrWidth = qr.style.width; oldQrHeight = qr.style.height;
                qr.style.width = size; qr.style.height = size;
                var subtitle = root.Q<Label>(QrCodeView.NameSubtitle);
                if (subtitle != null) { changedSubtitle = subtitle; oldSubtitle = subtitle.text; subtitle.text = "MetaMask 모바일 앱의 스캐너로 읽으세요"; }
                var image = qr.Q<Image>("qrcode-image");
                if (image?.image != null) { changedImage = image; oldFilter = image.image.filterMode; image.image.filterMode = FilterMode.Point; }
                Debug.Log($"Reown QR layout: original={qrSize}, requested={qrSize * 2f}, constrained={size}, root={root.resolvedStyle.width}x{root.resolvedStyle.height}. Screen pixels and mobile scan remain unverified.");
                applied = true;
            }).Every(16);
            return () => {
                resize.Pause(); modal.style.width = originalWidth;
                if (changedQr != null) { changedQr.style.width = oldQrWidth; changedQr.style.height = oldQrHeight; }
                if (changedSubtitle != null) changedSubtitle.text = oldSubtitle;
                if (changedImage?.image != null) changedImage.image.filterMode = oldFilter;
            };
#else
            return () => { };
#endif
        }
    }
}
