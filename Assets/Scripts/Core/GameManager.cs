// GameManager.cs — cerebro de GEAYI Racing (versión nativa Unity)
// Maneja: estados del juego, monedas y calidad gráfica automática.
// La lógica de carrera (conteo, vueltas, posiciones) vive en RaceManager.
using UnityEngine;
using Geayi.Player;

namespace Geayi.Core
{
    // Estados principales del juego
    public enum GameState
    {
        Menu,       // menú principal
        Countdown,  // cuenta regresiva 3-2-1-YA
        Racing,     // corriendo
        Finished    // pantalla de resultados
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Estado actual")]
        public GameState State = GameState.Menu;

        [Header("Monedas del jugador")]
        public int Coins = 0;
        private const string CoinsKey = "geayi_racing_coins";

        void Awake()
        {
            // Singleton: solo uno en toda la app
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // SaveSystem (personaje elegido, monedas): crearlo si la
            // escena no lo trae. Su Awake carga el JSON guardado.
            if (SaveSystem.Instance == null)
            {
                GameObject s = new GameObject("SaveSystem");
                s.AddComponent<SaveSystem>();
            }
            // Monedas: el JSON manda; migrar la llave vieja una sola vez
            int oldCoins = PlayerPrefs.GetInt(CoinsKey, 0);
            if (oldCoins > 0 && SaveSystem.Instance.Data.coins == 0)
            {
                SaveSystem.Instance.Data.coins = oldCoins;
                SaveSystem.Instance.Save();
            }
            Coins = SaveSystem.Instance.Data.coins;

            // Calidad: respeta la elegida en AJUSTES (auto/baja/alta)
            if (PlayerPrefs.GetInt("geayi_racing_fastmode", -1) == 1)
                SetFastMode(true);
            else
                ApplyQualityMode(SaveSystem.Instance.Data.qualityMode);
        }

        // ---------------- Monedas ----------------
        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            SaveCoins();
            if (UI.HUD.Instance != null) UI.HUD.Instance.RefreshCoins();
        }

        public bool SpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (Coins < amount) return false;
            Coins -= amount;
            SaveCoins();
            if (UI.HUD.Instance != null) UI.HUD.Instance.RefreshCoins();
            return true;
        }

        private void SaveCoins()
        {
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.Save();
        }

        // ---------------- Estados ----------------
        public void SetState(GameState newState)
        {
            State = newState;
        }

        // Volver al menú principal (desde la carrera o resultados)
        public void ReturnToMenu()
        {
            SetState(GameState.Menu);
            Time.timeScale = 1f;
            if (RaceManager.Instance != null) RaceManager.Instance.CleanupRace();
            // Soltar el joystick por si se quedó a mitad de un arrastre
            if (UI.HUD.Instance != null)
            {
                UI.HUD.Instance.ResetInput();
                UI.HUD.Instance.SetVisible(false);
            }
            // Mostrar el menú principal (referencia directa: Find no ve inactivos)
            if (UI.MainMenu.Instance != null) UI.MainMenu.Instance.SetMenuVisible(true);
        }

        // ---------------- Calidad gráfica automática ----------------
        // Estilo Roblox: si el teléfono es de gama baja, baja la calidad solo.
        private void ApplyAutoQuality()
        {
            if (DetectLowEnd())
            {
                // Gama baja: sin sombras, sin luces extra, 30 FPS
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.pixelLightCount = 0;
                QualitySettings.antiAliasing = 0;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;
                Debug.Log("[GameManager] Teléfono gama baja: calidad reducida.");
            }
            else
            {
                // Gama media/alta: calidad normal, 60 FPS
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.pixelLightCount = 2;
                QualitySettings.antiAliasing = 2;
                Application.targetFrameRate = 60;
                Debug.Log("[GameManager] Calidad normal activada.");
            }
        }

        private bool DetectLowEnd()
        {
            // Poca memoria RAM (< 3 GB) = gama baja
            if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3072)
                return true;
            // Pocos núcleos de CPU = gama baja
            if (SystemInfo.processorCount > 0 && SystemInfo.processorCount <= 4)
                return true;
            return false;
        }

        // Cambia la calidad: auto -> baja -> alta -> auto (desde AJUSTES)
        public void ToggleQuality()
        {
            string mode = SaveSystem.Instance != null
                ? SaveSystem.Instance.Data.qualityMode : "auto";
            string next = (mode == "auto") ? "low"
                      : (mode == "low") ? "high" : "auto";
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.Data.qualityMode = next;
                SaveSystem.Instance.SaveNow();
            }
            ApplyQualityMode(next);
            Debug.Log("[GameManager] Calidad: " + next);
        }

        private void ApplyQualityMode(string mode)
        {
            if (mode == "low")
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.pixelLightCount = 0;
                QualitySettings.antiAliasing = 0;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;
            }
            else if (mode == "high")
            {
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.pixelLightCount = 2;
                QualitySettings.antiAliasing = 2;
                Application.targetFrameRate = 60;
            }
            else
            {
                ApplyAutoQuality();
            }
        }

        // Modo rápido manual (true = sin sombras, más fluido)
        public void SetFastMode(bool on)
        {
            if (on)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.pixelLightCount = 0;
                QualitySettings.antiAliasing = 0;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;
            }
            else
            {
                ApplyAutoQuality();
            }
            PlayerPrefs.SetInt("geayi_racing_fastmode", on ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
