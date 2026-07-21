using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MinimapMod
{
    public class MinimapManager : MonoBehaviour
    {
        // --- config ---
        private const int MapPixelSize = 220;
        private const float OrthoSize = 15f;      // zoom (plus petit = plus zoomé)
        private const float MaxIconRange = 15f;   // doit correspondre a OrthoSize
        private const float LootRefreshInterval = 1f;

        private Camera mapCamera;
        private RenderTexture mapTexture;
        private Canvas canvas;
        private RawImage mapImage;
        private RectTransform iconLayer;
        private Image playerFacingArrow;

        private PlayerControllerB localPlayer;

        private readonly List<Image> playerIcons = new List<Image>();
        private readonly List<Image> enemyIcons = new List<Image>();
        private readonly List<Image> lootIcons = new List<Image>();

        private float lootTimer;
        private GrabbableObject[] cachedLoot = new GrabbableObject[0];

        private void Start()
        {
            localPlayer = GetComponent<PlayerControllerB>();
            BuildCamera();
            BuildUI();
        }

        private void BuildCamera()
        {
            var camGO = new GameObject("MinimapCamera");
            camGO.transform.SetParent(transform, false);
            camGO.transform.localPosition = new Vector3(0f, 25f, 0f);
            camGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            mapCamera = camGO.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = OrthoSize;
            mapCamera.nearClipPlane = 0.3f;
            mapCamera.farClipPlane = 60f;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = Color.black;

            var mainCam = GameNetworkManager.Instance.localPlayerController.gameplayCamera;
            if (mainCam != null)
            {
                mapCamera.cullingMask = mainCam.cullingMask;
            }

            mapTexture = new RenderTexture(256, 256, 16);
            mapCamera.targetTexture = mapTexture;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("MinimapCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            canvasGO.AddComponent<CanvasScaler>();

            // fond / rendu camera
            var mapGO = new GameObject("MapRawImage");
            mapGO.transform.SetParent(canvasGO.transform, false);
            mapImage = mapGO.AddComponent<RawImage>();
            mapImage.texture = mapTexture;
            var mapRect = mapImage.rectTransform;
            mapRect.anchorMin = new Vector2(1f, 1f);
            mapRect.anchorMax = new Vector2(1f, 1f);
            mapRect.pivot = new Vector2(1f, 1f);
            mapRect.sizeDelta = new Vector2(MapPixelSize, MapPixelSize);
            mapRect.anchoredPosition = new Vector2(-20f, -20f);

            // bordure simple
            var borderImg = mapGO.AddComponent<Outline>();
            borderImg.effectColor = Color.white;
            borderImg.effectDistance = new Vector2(2f, 2f);

            // calque des icones, au-dessus de la map, meme rect
            var layerGO = new GameObject("IconLayer");
            layerGO.transform.SetParent(mapGO.transform, false);
            iconLayer = layerGO.AddComponent<RectTransform>();
            iconLayer.anchorMin = Vector2.zero;
            iconLayer.anchorMax = Vector2.one;
            iconLayer.offsetMin = Vector2.zero;
            iconLayer.offsetMax = Vector2.zero;

            // fleche du joueur local (fixe au centre, tourne avec la vue)
            var arrowGO = new GameObject("LocalArrow");
            arrowGO.transform.SetParent(iconLayer, false);
            playerFacingArrow = arrowGO.AddComponent<Image>();
            playerFacingArrow.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            playerFacingArrow.color = Color.cyan;
            var arRect = playerFacingArrow.rectTransform;
            arRect.sizeDelta = new Vector2(10f, 10f);
            arRect.anchoredPosition = Vector2.zero;
        }

        private Image CreateIcon(Color color, float size)
        {
            var go = new GameObject("icon");
            go.transform.SetParent(iconLayer, false);
            var img = go.AddComponent<Image>();
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            img.color = color;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        private void Update()
        {
            if (localPlayer == null || mapCamera == null) return;
            if (StartOfRound.Instance == null) return;

            UpdatePlayerIcons();
            UpdateEnemyIcons();

            lootTimer -= Time.deltaTime;
            if (lootTimer <= 0f)
            {
                lootTimer = LootRefreshInterval;
                cachedLoot = FindObjectsOfType<GrabbableObject>();
            }
            UpdateLootIcons();
        }

        private Vector2 WorldToMapPos(Vector3 worldPos)
        {
            Vector3 offset = worldPos - localPlayer.transform.position;
            float scale = (MapPixelSize / 2f) / MaxIconRange;
            Vector2 pos = new Vector2(offset.x, offset.z) * scale;
            float maxR = MapPixelSize / 2f - 6f;
            if (pos.magnitude > maxR) pos = pos.normalized * maxR;
            return pos;
        }

        private void EnsurePool(List<Image> pool, int needed, Color color, float size)
        {
            while (pool.Count < needed)
            {
                pool.Add(CreateIcon(color, size));
            }
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(i < needed);
            }
        }

        private void UpdatePlayerIcons()
        {
            var players = StartOfRound.Instance.allPlayerScripts;
            int needed = 0;
            foreach (var p in players)
            {
                if (p == null || p == localPlayer) continue;
                if (!p.isPlayerControlled || p.isPlayerDead) continue;
                needed++;
            }

            EnsurePool(playerIcons, needed, Color.green, 8f);

            int idx = 0;
            foreach (var p in players)
            {
                if (p == null || p == localPlayer) continue;
                if (!p.isPlayerControlled || p.isPlayerDead) continue;

                playerIcons[idx].rectTransform.anchoredPosition = WorldToMapPos(p.transform.position);
                idx++;
            }
        }

        private void UpdateEnemyIcons()
        {
            if (RoundManager.Instance == null) { EnsurePool(enemyIcons, 0, Color.red, 8f); return; }

            var enemies = RoundManager.Instance.SpawnedEnemies;
            int needed = 0;
            foreach (var e in enemies)
            {
                if (e == null || e.isEnemyDead) continue;
                needed++;
            }

            EnsurePool(enemyIcons, needed, Color.red, 9f);

            int idx = 0;
            foreach (var e in enemies)
            {
                if (e == null || e.isEnemyDead) continue;
                enemyIcons[idx].rectTransform.anchoredPosition = WorldToMapPos(e.transform.position);
                idx++;
            }
        }

        private void UpdateLootIcons()
        {
            int needed = 0;
            foreach (var item in cachedLoot)
            {
                if (item == null || item.itemProperties == null) continue;
                if (!item.itemProperties.isScrap) continue;
                if (item.isInShipRoom || item.isInElevator) continue;
                needed++;
            }

            EnsurePool(lootIcons, needed, Color.yellow, 6f);

            int idx = 0;
            foreach (var item in cachedLoot)
            {
                if (item == null || item.itemProperties == null) continue;
                if (!item.itemProperties.isScrap) continue;
                if (item.isInShipRoom || item.isInElevator) continue;

                lootIcons[idx].rectTransform.anchoredPosition = WorldToMapPos(item.transform.position);
                idx++;
            }
        }

        private void OnDestroy()
        {
            if (mapTexture != null) mapTexture.Release();
            if (canvas != null) Destroy(canvas.gameObject);
            if (mapCamera != null) Destroy(mapCamera.gameObject);
        }
    }
}
