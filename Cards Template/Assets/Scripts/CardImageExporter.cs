using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Renders an isolated visual copy; gameplay scripts and the scene camera never run on the copy.</summary>
public static class CardImageExporter
{
    public static Texture2D Capture(RectTransform source)
    {
        if (source == null || source.rect.width <= 0 || source.rect.height <= 0)
            throw new ArgumentException("Kart boyutları geçersiz.", nameof(source));
        var previous = RenderTexture.active;
        RenderTexture target = null;
        Texture2D texture = null;
        GameObject root = null;
        try
        {
            float scale = Mathf.Min(1, 2048f / Mathf.Max(source.rect.width, source.rect.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.rect.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.rect.height * scale));
            root = new GameObject("Card Export", typeof(RectTransform));
            root.SetActive(false);
            var copy = UnityEngine.Object.Instantiate(source.gameObject, root.transform, false);
            // Strip while inactive, before Awake/OnEnable can affect the actual game.
            var scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--)
            {
                var script = scripts[i];
                if (!(script is Graphic) && !(script is BaseMeshEffect) && !(script is Mask) &&
                    !(script is RectMask2D) && !(script is LayoutGroup) &&
                    !(script is ContentSizeFitter) && !(script is AspectRatioFitter))
                    UnityEngine.Object.DestroyImmediate(script);
            }

            var canvasObject = new GameObject("Export Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 31;
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = source.rect.size;
            canvasRect.position = new Vector3(10000, 10000, 0);
            copy.transform.SetParent(canvasRect, false);
            var copyRect = (RectTransform)copy.transform;
            copyRect.anchorMin = copyRect.anchorMax = new Vector2(0.5f, 0.5f);
            copyRect.pivot = new Vector2(0.5f, 0.5f);
            copyRect.sizeDelta = source.rect.size;
            copyRect.anchoredPosition = Vector2.zero;
            copyRect.localRotation = Quaternion.identity;
            copyRect.localScale = Vector3.one;
            foreach (var item in copy.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;

            var cameraObject = new GameObject("Export Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = source.rect.height * 0.5f;
            camera.aspect = (float)width / height;
            camera.transform.position = new Vector3(10000, 10000, -10);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            canvas.worldCamera = camera;
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            foreach (var text in copy.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
            else
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new NotSupportedException("Etkin render pipeline kart dışa aktarımını desteklemiyor.");
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            RenderTexture.active = target;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            return texture;
        }
        catch
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            throw;
        }
        finally
        {
            RenderTexture.active = previous;
            if (root != null)
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
            }
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }
    }
}
