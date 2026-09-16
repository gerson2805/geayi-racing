// RaceManager.cs — controla la carrera: parrilla, conteo, vueltas,
// posiciones en vivo, boost por monedas y pantalla de resultados.
// Los carros se crean en Awake (antes del Start de la cámara) para que
// CameraFollow encuentre al jugador desde el primer frame.
using UnityEngine;
using System.Collections.Generic;
using Geayi.Vehicles;
using Geayi.AI;
using Geayi.UI;
using Geayi.Characters;
using Geayi.World;

namespace Geayi.Core
{
    public class Racer
    {
        public string driverName;
        public bool isPlayer;
        public VehicleController vehicle;
        public RaceAI ai; // null para el jugador
        public int lap;
        public float progress;
        public float baseMaxSpeed;
    }

    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Carrera")]
        public int totalLaps = 3;
        public bool RaceRunning { get; private set; }
        public VehicleController PlayerCar { get; private set; }

        private TrackBuilder track;
        private readonly List<Racer> racers = new List<Racer>();
        private int playerWp = 0;
        private int playerLap = 0;
        private float countdownT = 0f;
        private int countdownShown = -1;
        private float boostTimer = 0f;
        private float goHideTimer = 0f;
        private bool finished = false;
        private GameObject resultsPanel;

        private readonly Color[] carColors = {
            new Color(0.90f, 0.15f, 0.20f), // jugador: rojo
            new Color(0.15f, 0.35f, 0.90f), // azul
            new Color(0.15f, 0.70f, 0.30f), // verde
            new Color(0.95f, 0.75f, 0.15f), // amarillo
        };

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            track = FindAnyObjectByType<TrackBuilder>();
            if (track != null) track.Build(); // explícito: no depende del orden de Awake
            SpawnCars();
        }

        // ---------- parrilla ----------

        private void SpawnCars()
        {
            string playerId = "gerson";
            if (SaveSystem.Instance != null &&
                !string.IsNullOrEmpty(SaveSystem.Instance.Data.characterId))
                playerId = SaveSystem.Instance.Data.characterId;

            // 3 rivales: otros miembros de la familia
            List<string> rivals = new List<string>();
            foreach (var c in FamilyData.All)
            {
                if (c.id != playerId && rivals.Count < 3)
                    rivals.Add(c.id);
            }

            for (int i = 0; i < 4; i++)
            {
                bool isPlayer = (i == 0);
                string charId = isPlayer ? playerId : rivals[i - 1];
                CharacterDef def = FamilyData.Get(charId);

                Vector3 pos; float yaw;
                track.GetStartSlot(i, out pos, out yaw);

                VehicleController vc = VehicleController.CreateSimpleCar(pos);
                vc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                vc.name = isPlayer ? "PlayerCar" : "AICar" + i;
                vc.occupied = true; // en carreras siempre hay conductor
                if (isPlayer)
                {
                    vc.gameObject.tag = "Player"; // la cámara y las monedas lo buscan
                    PlayerCar = vc;
                }

                // Color del carro
                Transform body = vc.transform.Find("Body");
                if (body != null)
                {
                    var rend = body.GetComponent<Renderer>();
                    if (rend != null) rend.material.color = carColors[i];
                }

                // Conductor: el personaje de la familia elegido (sentado)
                GameObject avatar = CharacterBuilder.Build(def);
                avatar.transform.SetParent(vc.transform, false);
                avatar.transform.localPosition = new Vector3(0f, 0.72f, -0.3f);
                avatar.transform.localScale = Vector3.one * 0.5f;

                RaceAI ai = null;
                float baseMax;
                if (isPlayer)
                {
                    baseMax = 20f;
                }
                else
                {
                    ai = vc.gameObject.AddComponent<RaceAI>();
                    ai.vehicle = vc;
                    ai.waypoints = track.Waypoints;
                    ai.baseMaxSpeed = 17.2f + i * 0.5f;
                    baseMax = ai.baseMaxSpeed;
                }
                vc.maxSpeed = baseMax;

                racers.Add(new Racer
                {
                    driverName = def.name + (isPlayer ? " (TU)" : ""),
                    isPlayer = isPlayer,
                    vehicle = vc,
                    ai = ai,
                    lap = 0,
                    progress = 0f,
                    baseMaxSpeed = baseMax,
                });
            }
            Debug.Log("[RaceManager] 4 carros en parrilla.");
        }

        private void DespawnCars()
        {
            foreach (Racer r in racers)
            {
                if (r.vehicle != null)
                    Destroy(r.vehicle.gameObject);
            }
            racers.Clear();
            PlayerCar = null;
        }

        // Llamado al elegir otro corredor: reconstruye la parrilla
        public void RespawnCars()
        {
            DespawnCars();
            SpawnCars();
        }

        private void ResetCars()
        {
            for (int i = 0; i < racers.Count; i++)
            {
                Racer r = racers[i];
                Vector3 pos; float yaw;
                track.GetStartSlot(i, out pos, out yaw);
                r.vehicle.transform.position = pos;
                r.vehicle.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                r.vehicle.ResetMotion();
                r.vehicle.maxSpeed = r.baseMaxSpeed;
                r.lap = 0;
                r.progress = 0f;
                if (r.ai != null) { r.ai.wpIndex = 0; r.ai.lap = 0; }
            }
            playerWp = 0;
            playerLap = 0;
            boostTimer = 0f;
        }

        // ---------- flujo de carrera ----------

        public void StartRace()
        {
            ResetCars();
            if (resultsPanel != null) resultsPanel.SetActive(false);
            finished = false;
            RaceRunning = false;
            GameManager.Instance.SetState(GameState.Countdown);
            if (MainMenu.Instance != null) MainMenu.Instance.SetMenuVisible(false);
            if (HUD.Instance != null)
            {
                HUD.Instance.SetVisible(true);
                HUD.Instance.BindAccelerate(OnAccelerate);
                HUD.Instance.RefreshCoins();
                HUD.Instance.SetRaceInfo(4, 1, totalLaps);
            }
            var cf = Camera.main != null
                ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cf != null && PlayerCar != null) cf.target = PlayerCar.transform;
            countdownT = 3.2f;
            countdownShown = -1;
            Debug.Log("[RaceManager] ¡Arranca la carrera!");
        }

        public void CleanupRace()
        {
            RaceRunning = false;
            finished = false;
            ResetCars();
            if (resultsPanel != null) resultsPanel.SetActive(false);
            if (HUD.Instance != null) HUD.Instance.HideCountdown();
        }

        private void OnAccelerate(bool on)
        {
            if (RaceRunning && PlayerCar != null)
                PlayerCar.SetThrottle(on);
        }

        // Boost de 3 segundos al agarrar una moneda (lo llama CoinPickup)
        public void BoostPlayer()
        {
            if (!RaceRunning || finished) return;
            boostTimer = 3f;
            Debug.Log("[RaceManager] ¡BOOST!");
        }

        void Update()
        {
            if (GameManager.Instance == null) return;
            GameState st = GameManager.Instance.State;

            if (st == GameState.Countdown)
            {
                countdownT -= Time.deltaTime;
                if (countdownT > 0f)
                {
                    int n = Mathf.CeilToInt(countdownT);
                    if (n != countdownShown)
                    {
                        countdownShown = n;
                        if (HUD.Instance != null) HUD.Instance.ShowCountdown(n.ToString());
                    }
                }
                else
                {
                    GameManager.Instance.SetState(GameState.Racing);
                    RaceRunning = true;
                    // Si el dedo ya estaba en ACELERAR, arrancar de una vez
                    if (HUD.Instance != null && HUD.Instance.IsAccelPressed &&
                        PlayerCar != null)
                        PlayerCar.SetThrottle(true);
                    if (HUD.Instance != null) HUD.Instance.ShowCountdown("¡YA!");
                    goHideTimer = 0.8f;
                }
            }
            else if (st == GameState.Racing && !finished)
            {
                if (goHideTimer > 0f)
                {
                    goHideTimer -= Time.deltaTime;
                    if (goHideTimer <= 0f && HUD.Instance != null)
                        HUD.Instance.HideCountdown();
                }

                // Dirección del jugador: joystick izquierdo SOLO gira (eje X)
                var joy = (HUD.Instance != null) ? HUD.Instance.Joystick : null;
                if (joy != null && PlayerCar != null)
                    PlayerCar.SetSteer(joy.IsActive ? joy.Horizontal : 0f);

                // Boost por moneda
                if (boostTimer > 0f)
                {
                    boostTimer -= Time.deltaTime;
                    PlayerCar.maxSpeed = 30f;
                    if (boostTimer <= 0f) PlayerCar.maxSpeed = 20f;
                }

                UpdateProgress();
                ApplyRubberBand();

                if (HUD.Instance != null)
                    HUD.Instance.SetRaceInfo(GetPlayerPosition(),
                        Mathf.Min(playerLap + 1, totalLaps), totalLaps);

                if (playerLap >= totalLaps)
                    FinishRace();
            }
        }

        // ---------- progreso y posiciones ----------

        private void UpdateProgress()
        {
            int n = track.Waypoints.Count;
            // Jugador
            {
                Racer me = racers[0];
                Vector3 p = Flat(me.vehicle.transform.position);
                Vector3 t = Flat(track.Waypoints[playerWp]);
                if (Vector3.Distance(p, t) < 7f)
                {
                    playerWp++;
                    if (playerWp >= n) { playerWp = 0; playerLap++; }
                }
                float d = Vector3.Distance(p, Flat(track.Waypoints[playerWp]));
                me.lap = playerLap;
                me.progress = playerLap * n + playerWp - d / 100f;
            }
            // IA
            foreach (Racer r in racers)
            {
                if (r.isPlayer || r.ai == null) continue;
                Vector3 p = Flat(r.vehicle.transform.position);
                float d = Vector3.Distance(p, Flat(track.Waypoints[r.ai.wpIndex]));
                r.lap = r.ai.lap;
                r.progress = r.ai.lap * n + r.ai.wpIndex - d / 100f;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private int GetPlayerPosition()
        {
            List<Racer> order = new List<Racer>(racers);
            order.Sort((a, b) => b.progress.CompareTo(a.progress));
            return order.IndexOf(racers[0]) + 1;
        }

        // Goma elástica sutil: la IA no se escapa ni se queda muy atrás
        private void ApplyRubberBand()
        {
            float meP = racers[0].progress;
            foreach (Racer r in racers)
            {
                if (r.isPlayer || r.ai == null) continue;
                float diff = meP - r.progress; // + = el jugador va adelante
                r.vehicle.maxSpeed = Mathf.Clamp(
                    r.baseMaxSpeed + diff * 0.15f,
                    r.baseMaxSpeed - 2f, r.baseMaxSpeed + 3f);
            }
        }

        // ---------- final ----------

        private void FinishRace()
        {
            finished = true;
            RaceRunning = false;
            GameManager.Instance.SetState(GameState.Finished);
            if (PlayerCar != null)
            {
                PlayerCar.SetThrottle(false);
                PlayerCar.SetSteer(0f);
            }

            List<Racer> order = new List<Racer>(racers);
            order.Sort((a, b) => b.progress.CompareTo(a.progress));
            int pos = order.IndexOf(racers[0]) + 1;
            int[] prize = { 50, 30, 20, 10 };
            int won = prize[Mathf.Clamp(pos - 1, 0, 3)];
            GameManager.Instance.AddCoins(won);
            ShowResults(order, pos, won);
            Debug.Log("[RaceManager] Fin de carrera. Posición: " + pos + "°");
        }

        private void ShowResults(List<Racer> order, int pos, int won)
        {
            if (resultsPanel != null) Destroy(resultsPanel);
            resultsPanel = new GameObject("ResultsPanel");
            // Canvas propio encima de todo
            GameObject canvasGo = new GameObject("ResultsCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();
            resultsPanel.transform.SetParent(canvasGo.transform, false);

            var bg = resultsPanel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            StretchFull(resultsPanel.GetComponent<RectTransform>());

            var title = UILabel.CreateLabel(resultsPanel.transform,
                "¡CARRERA TERMINADA!", 54, Color.yellow);
            Anchor(title.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.93f));

            for (int i = 0; i < order.Count; i++)
            {
                Racer r = order[i];
                bool isMe = r.isPlayer;
                var row = UILabel.CreateLabel(resultsPanel.transform,
                    (i + 1) + "°  " + r.driverName, 40,
                    isMe ? Color.green : Color.white);
                Anchor(row.GetComponent<RectTransform>(),
                    new Vector2(0.10f, 0.62f - i * 0.11f),
                    new Vector2(0.90f, 0.71f - i * 0.11f));
            }

            var prizeLabel = UILabel.CreateLabel(resultsPanel.transform,
                "Premio: +" + won + " MONEDAS", 36, Color.yellow);
            Anchor(prizeLabel.GetComponent<RectTransform>(),
                new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.25f));

            MakeButton(resultsPanel.transform, "OTRA VEZ", 32,
                new Color(0.15f, 0.70f, 0.30f),
                new Vector2(0.08f, 0.04f), new Vector2(0.38f, 0.14f),
                () => StartRace());
            MakeButton(resultsPanel.transform, "MENÚ", 32,
                new Color(0.45f, 0.47f, 0.55f),
                new Vector2(0.62f, 0.04f), new Vector2(0.92f, 0.14f),
                () => GameManager.Instance.ReturnToMenu());
        }

        private void MakeButton(Transform parent, string text, int fontSize,
            Color color, Vector2 anchorMin, Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Btn");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            var b = go.AddComponent<UnityEngine.UI.Button>();
            Anchor(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var label = UILabel.CreateLabel(go.transform, text, fontSize, Color.white);
            StretchFull(label.GetComponent<RectTransform>());
            if (onClick != null) b.onClick.AddListener(onClick);
        }

        private void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
