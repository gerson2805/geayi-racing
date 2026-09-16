// SceneBootstrapper.cs — arma la escena completa por código en Awake:
// cámara + CameraFollow, luz, GameManager, TrackBuilder, RaceManager,
// MainMenu, HUD y EventSystem. La escena Main.unity solo trae este objeto.
using UnityEngine;
using UnityEngine.EventSystems;
using Geayi.Core;
using Geayi.UI;
using Geayi.World;
using Geayi.Player;

public class SceneBootstrapper : MonoBehaviour
{
    void Awake()
    {
        // Cámara principal (con la cámara 360° probada)
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.53f, 0.81f, 0.98f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<CameraFollow>();

        // Luz de día
        GameObject lightGo = new GameObject("Directional Light");
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.2f;
        l.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Lógica del juego (orden: cámara primero para TrackBuilder)
        new GameObject("GameManager").AddComponent<GameManager>();
        new GameObject("TrackBuilder").AddComponent<TrackBuilder>();
        new GameObject("RaceManager").AddComponent<RaceManager>();
        new GameObject("MainMenu").AddComponent<MainMenu>();
        new GameObject("HUD").AddComponent<HUD>();

        // Entrada táctil para los botones de la UI
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        Debug.Log("[Bootstrap] Escena armada: cámara, pista, menú y HUD.");
    }
}
