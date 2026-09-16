// MainMenu.cs (GEAYI Racing) — menú principal construido por código.
// Botones: CORRER, FAMILIA, AJUSTES.
// CORRER inicia la carrera. FAMILIA abre el selector de personajes (22).
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Geayi.Core;
using Geayi.Characters;

namespace Geayi.UI
{
    public class MainMenu : MonoBehaviour
    {
        public static MainMenu Instance { get; private set; }

        private RectTransform root;
        private GameObject panelMenu;
        private GameObject panelFamily;
        private GameObject panelSettings;
        private Transform familyList;
        private GameObject settingsPanelQuality;
        private Text qualityText;
        private float menuCamAngle = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildMenu();
        }

        void Update()
        {
            // Cámara lenta girando alrededor de la pista en el menú
            if (GameManager.Instance != null &&
                GameManager.Instance.State == GameState.Menu &&
                Camera.main != null)
            {
                menuCamAngle += Time.deltaTime * 6f;
                float r = 130f;
                Vector3 c = Vector3.zero;
                Camera.main.transform.position =
                    c + new Vector3(Mathf.Sin(menuCamAngle * Mathf.Deg2Rad) * r,
                                    55f,
                                    Mathf.Cos(menuCamAngle * Mathf.Deg2Rad) * r);
                Camera.main.transform.LookAt(new Vector3(0f, 0f, 0f));
            }
        }

        // ---------- construcción ----------

        private void BuildMenu()
        {
            GameObject canvasGo = new GameObject("MenuCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            root = new GameObject("MenuRoot").AddComponent<RectTransform>();
            root.transform.SetParent(canvasGo.transform, false);
            StretchFull(root);

            // Título
            var title = UILabel.CreateLabel(root, "GEAYI RACING", 84,
                new Color(1f, 0.85f, 0.2f));
            Anchor(title.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f));
            Outline(title);

            // Panel de botones
            panelMenu = new GameObject("PanelMenu");
            panelMenu.transform.SetParent(root, false);
            var pmRt = panelMenu.AddComponent<RectTransform>();
            Anchor(pmRt, new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.70f));

            MakeButton(panelMenu.transform, "CORRER", 48,
                new Color(0.15f, 0.70f, 0.30f),
                new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.95f),
                () => RaceManager.Instance.StartRace());
            MakeButton(panelMenu.transform, "FAMILIA", 48,
                new Color(0.20f, 0.45f, 0.90f),
                new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.64f),
                () => ShowFamily(true));
            MakeButton(panelMenu.transform, "AJUSTES", 48,
                new Color(0.45f, 0.47f, 0.55f),
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.33f),
                () => ShowSettings(true));

            BuildFamilyPanel();
            BuildSettingsPanel();
        }

        private void MakeButton(Transform parent, string text, int fontSize,
            Color color, Vector2 anchorMin, Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Btn");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var b = go.AddComponent<Button>();
            Anchor(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var label = UILabel.CreateLabel(go.transform, text, fontSize,
                Color.white);
            StretchFull(label.GetComponent<RectTransform>());
            Outline(label);
            if (onClick != null) b.onClick.AddListener(onClick);
        }

        // ---------- selector de familia ----------

        private void BuildFamilyPanel()
        {
            panelFamily = new GameObject("PanelFamily");
            panelFamily.transform.SetParent(root, false);
            var pfRt = panelFamily.AddComponent<RectTransform>();
            StretchFull(pfRt);
            var bg = panelFamily.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.88f);

            var title = UILabel.CreateLabel(panelFamily.transform,
                "ELIGE TU CORREDOR", 48, Color.yellow);
            Anchor(title.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f));

            // Scroll con los 22 personajes
            GameObject scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(panelFamily.transform, false);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            Anchor(scrollGo.GetComponent<RectTransform>(),
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.84f));

            familyList = new GameObject("List").transform;
            familyList.SetParent(scrollGo.transform, false);
            var content = familyList.gameObject.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll.content = content;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(320f, 120f);
            grid.spacing = new Vector2(20f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.padding = new RectOffset(20, 20, 20, 20);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var c in FamilyData.All)
            {
                CharacterDef def = c;
                MakeButton(content, def.name.ToUpper(), 32,
                    new Color(0.20f, 0.45f, 0.90f),
                    Vector2.zero, Vector2.zero,
                    () => SelectCharacter(def.id));
            }

            MakeButton(panelFamily.transform, "ATRÁS", 36,
                new Color(0.45f, 0.47f, 0.55f),
                new Vector2(0.30f, 0.02f), new Vector2(0.70f, 0.11f),
                () => ShowFamily(false));
            panelFamily.SetActive(false);
        }

        private void SelectCharacter(string id)
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.Data.characterId = id;
                SaveSystem.Instance.SaveNow();
            }
            // Reconstruye la parrilla con el corredor elegido y vuelve al menú
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.RespawnCars();
                GameManager.Instance.ReturnToMenu();
            }
        }

        private void ShowFamily(bool show)
        {
            panelMenu.SetActive(!show);
            panelFamily.SetActive(show);
        }

        // ---------- ajustes ----------

        private void BuildSettingsPanel()
        {
            panelSettings = new GameObject("PanelSettings");
            panelSettings.transform.SetParent(root, false);
            var psRt = panelSettings.AddComponent<RectTransform>();
            StretchFull(psRt);
            var bg = panelSettings.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.88f);

            var title = UILabel.CreateLabel(panelSettings.transform,
                "AJUSTES", 48, Color.yellow);
            Anchor(title.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f));

            var qLabel = UILabel.CreateLabel(panelSettings.transform,
                "Calidad de gráficos:", 36, Color.white);
            Anchor(qLabel.GetComponent<RectTransform>(),
                new Vector2(0.10f, 0.55f), new Vector2(0.90f, 0.68f));

            qualityText = UILabel.CreateLabel(panelSettings.transform,
                QualityName(), 40, Color.green);
            Anchor(qualityText.GetComponent<RectTransform>(),
                new Vector2(0.10f, 0.40f), new Vector2(0.90f, 0.53f));

            MakeButton(panelSettings.transform, "CAMBIAR CALIDAD", 36,
                new Color(0.20f, 0.45f, 0.90f),
                new Vector2(0.20f, 0.24f), new Vector2(0.80f, 0.37f),
                () => {
                    GameManager.Instance.ToggleQuality();
                    if (qualityText != null)
                        qualityText.text = QualityName();
                });

            MakeButton(panelSettings.transform, "ATRÁS", 36,
                new Color(0.45f, 0.47f, 0.55f),
                new Vector2(0.30f, 0.04f), new Vector2(0.70f, 0.16f),
                () => ShowSettings(false));
            panelSettings.SetActive(false);
        }

        private string QualityName()
        {
            if (SaveSystem.Instance == null) return "AUTO";
            return SaveSystem.Instance.Data.qualityMode.ToUpper();
        }

        private void ShowSettings(bool show)
        {
            panelMenu.SetActive(!show);
            panelSettings.SetActive(show);
        }

        // ---------- visibilidad ----------

        public void SetMenuVisible(bool v)
        {
            if (root != null) root.gameObject.SetActive(v);
            if (v)
            {
                ShowFamily(false);
                ShowSettings(false);
            }
        }

        // ---------- utilidades ----------

        private void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Outline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            o.effectDistance = new Vector2(2f, -2f);
        }
    }
}
