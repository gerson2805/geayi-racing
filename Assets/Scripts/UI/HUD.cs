// HUD.cs (GEAYI Racing) — joystick izquierdo (solo giro) + botón ACELERAR.
// El código de VirtualJoystick y su zona táctil se mantiene intacto.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Geayi.Player;

namespace Geayi.UI
{
    // (SIN CAMBIOS: joystick izquierdo con entrada táctil nativa + cámara 360°)
    public class VirtualJoystick : MonoBehaviour, IDragHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public float MaxRange = 120f;
        public RectTransform Base;
        public RectTransform Knob;
        private CameraFollow camFollow;

        public float Horizontal { get; private set; }
        public float Vertical { get; private set; }
        public bool IsActive { get; private set; }
        // Dedo que está usando el joystick (pointerId == fingerId en táctil)
        public int ActivePointerId { get; private set; } = -1;

        public void Setup(RectTransform baseRt, RectTransform knobRt,
            CameraFollow cam)
        {
            Base = baseRt;
            Knob = knobRt;
            camFollow = cam;
        }

        public void OnPointerDown(PointerEventData e)
        {
            IsActive = true;
            ActivePointerId = e.pointerId;
            OnDrag(e);
        }

        // ¿Este dedo es el del joystick? (lo usa CameraFollow para la cámara 360°)
        public bool IsJoystickPointer(int pointerId)
        {
            return IsActive && pointerId == ActivePointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 center = Base.position;
            Vector2 offset = e.position - center;
            if (offset.magnitude > MaxRange)
                offset = offset.normalized * MaxRange;
            Knob.position = center + offset;
            Horizontal = Mathf.Clamp(offset.x / MaxRange, -1f, 1f);
            Vertical = Mathf.Clamp(offset.y / MaxRange, -1f, 1f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Release();
        }

        public void Release()
        {
            IsActive = false;
            ActivePointerId = -1;
            Horizontal = 0f;
            Vertical = 0f;
            if (Knob != null && Base != null) Knob.position = Base.position;
        }

        public void CenterOn(GameObject owner, Vector2 touchPos)
        {
            if (Base != null)
            {
                owner.GetComponent<RectTransform>().position = touchPos;
                Base.position = touchPos;
            }
        }

        // Suelta el joystick sin datos del dedo (al volver al menú)
        public void ForceRelease()
        {
            Release();
        }
    }

    public class HUD : MonoBehaviour
    {
        public static HUD Instance { get; private set; }
        public VirtualJoystick Joystick { get; private set; }

        private RectTransform root;
        private Text lblRace;    // VUELTA 1/3 · POS 1/4
        private Text lblCoins;   // monedas
        private Text lblCountdown;
        private Image btnAccelImg;
        private Color accelNormal = new Color(0.15f, 0.75f, 0.30f, 0.9f);
        private Color accelPressed = new Color(0.10f, 0.55f, 0.20f, 0.9f);
        private System.Action<bool> accelerateCb;
        private EventTrigger touchZone;
        private Canvas canvas;
        private static readonly Color Clear = new Color(0, 0, 0, 0);

        // Singleton tolerante: si ya hay uno, este se destruye
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildHUD();
        }

        private void BuildHUD()
        {
            GameObject canvasGo = new GameObject("HUDCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            root = new GameObject("HUDRoot")
                .AddComponent<RectTransform>();
            root.transform.SetParent(canvasGo.transform, false);
            StretchFull(root);

            // Vuelta y posición (arriba centro)
            lblRace = UILabel.CreateLabel(root, "VUELTA 1/3 · POS 4/4", 34,
                Color.white);
            Anchor(lblRace.GetComponent<RectTransform>(),
                new Vector2(0.30f, 0.90f), new Vector2(0.70f, 0.99f));
            Outline(lblRace);

            // Monedas (arriba izquierda)
            lblCoins = UILabel.CreateLabel(root, "0", 34, Color.yellow);
            Anchor(lblCoins.GetComponent<RectTransform>(),
                new Vector2(0.02f, 0.90f), new Vector2(0.20f, 0.99f));
            Outline(lblCoins);

            // Cuenta regresiva (centro)
            lblCountdown = UILabel.CreateLabel(root, "", 96, Color.white);
            Anchor(lblCountdown.GetComponent<RectTransform>(),
                new Vector2(0.30f, 0.35f), new Vector2(0.70f, 0.65f));
            Outline(lblCountdown);

            BuildTouchZone();
            BuildJoystick();
            BuildAccelerateButton();

            // Menú (arriba derecha; DESPUÉS de la zona táctil para que
            // sus toques no queden tapados por ella)
            GameObject menuGo = new GameObject("BtnMenu");
            menuGo.transform.SetParent(root, false);
            var menuImg = menuGo.AddComponent<Image>();
            menuImg.color = new Color(0.30f, 0.32f, 0.38f, 0.8f);
            var menuBtn = menuGo.AddComponent<Button>();
            Anchor(menuGo.GetComponent<RectTransform>(),
                new Vector2(0.90f, 0.90f), new Vector2(0.99f, 0.99f));
            var menuLabel = UILabel.CreateLabel(menuGo.transform,
                "MENU", 28, Color.white);
            StretchFull(menuLabel.GetComponent<RectTransform>());
            menuBtn.onClick.AddListener(
                () => Geayi.Core.GameManager.Instance.ReturnToMenu());
            SetVisible(false);
        }

        // (SIN CAMBIOS: zona táctil invisible; la cámara 360° se maneja
        // sola con toques nativos en CameraFollow)
        private void BuildTouchZone()
        {
            GameObject tz = new GameObject("TouchZone");
            tz.transform.SetParent(root, false);
            var img = tz.AddComponent<Image>();
            img.color = Clear;
            img.raycastTarget = true;
            touchZone = tz.AddComponent<EventTrigger>();
            var rt = tz.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            EventTrigger.Entry down = new EventTrigger.Entry();
            down.eventID = EventTriggerType.PointerDown;
            down.callback.AddListener((e) => OnTouchDown((PointerEventData)e));
            touchZone.triggers.Add(down);

            EventTrigger.Entry drag = new EventTrigger.Entry();
            drag.eventID = EventTriggerType.Drag;
            drag.callback.AddListener((e) => OnTouchDrag((PointerEventData)e));
            touchZone.triggers.Add(drag);

            EventTrigger.Entry up = new EventTrigger.Entry();
            up.eventID = EventTriggerType.PointerUp;
            up.callback.AddListener((e) => OnTouchUp((PointerEventData)e));
            touchZone.triggers.Add(up);

            EventTrigger.Entry cancel = new EventTrigger.Entry();
            cancel.eventID = EventTriggerType.PointerCancel;
            cancel.callback.AddListener((e) => OnTouchUp((PointerEventData)e));
            touchZone.triggers.Add(cancel);
        }

        private void OnTouchDown(PointerEventData e)
        {
            // Primer dedo en la mitad izquierda = joystick donde tocó.
            // Los demás dedos los gira la cámara sola (CameraFollow).
            if (Joystick != null && !Joystick.IsActive &&
                e.position.x < Screen.width * 0.5f)
            {
                Joystick.gameObject.SetActive(true);
                Joystick.CenterOn(Joystick.gameObject, e.position);
                Joystick.OnPointerDown(e);
            }
        }

        private void OnTouchDrag(PointerEventData e)
        {
            if (Joystick != null && Joystick.IsJoystickPointer(e.pointerId))
                Joystick.OnDrag(e);
        }

        private void OnTouchUp(PointerEventData e)
        {
            if (Joystick != null && Joystick.IsJoystickPointer(e.pointerId))
                Joystick.Release();
        }

        // ¿El toque cae sobre la base del joystick? (lo usa CameraFollow)
        public bool IsTouchOnJoystick(Vector2 screenPos)
        {
            if (Joystick == null || !Joystick.gameObject.activeSelf)
                return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                Joystick.GetComponent<RectTransform>(), screenPos, null);
        }

        // (SIN CAMBIOS: construcción visual del joystick)
        private void BuildJoystick()
        {
            GameObject jgo = new GameObject("Joystick");
            jgo.transform.SetParent(root, false);
            var jrt = jgo.AddComponent<RectTransform>();
            jrt.anchorMin = new Vector2(0.06f, 0.06f);
            jrt.anchorMax = new Vector2(0.06f, 0.06f);
            jrt.sizeDelta = new Vector2(280f, 280f);
            jrt.anchoredPosition = Vector2.zero;

            GameObject baseGo = new GameObject("Base");
            baseGo.transform.SetParent(jgo.transform, false);
            var baseImg = baseGo.AddComponent<Image>();
            baseImg.color = new Color(1, 1, 1, 0.25f);
            StretchFull(baseGo.GetComponent<RectTransform>());

            GameObject knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(baseGo.transform, false);
            var knobImg = knobGo.AddComponent<Image>();
            knobImg.color = new Color(1, 1, 1, 0.55f);
            var krt = knobGo.GetComponent<RectTransform>();
            StretchFull(krt);
            krt.sizeDelta = new Vector2(-140f, -140f);
            krt.anchoredPosition = Vector2.zero;

            Joystick = jgo.AddComponent<VirtualJoystick>();
            Joystick.MaxRange = 100f;
            var cam = Camera.main != null
                ? Camera.main.GetComponent<CameraFollow>() : null;
            Joystick.Setup(baseGo.GetComponent<RectTransform>(),
                knobGo.GetComponent<RectTransform>(), cam);
            jgo.SetActive(false);
        }

        // Botón ACELERAR (derecha abajo) — pointer down/up
        private void BuildAccelerateButton()
        {
            GameObject ago = new GameObject("BtnAccel");
            ago.transform.SetParent(root, false);
            var img = ago.AddComponent<Image>();
            img.color = accelNormal;
            btnAccelImg = img;
            var trig = ago.AddComponent<EventTrigger>();
            Anchor(ago.GetComponent<RectTransform>(),
                new Vector2(0.80f, 0.06f), new Vector2(0.96f, 0.34f));

            var label = UILabel.CreateLabel(ago.transform,
                "ACELERAR", 32, Color.white);
            StretchFull(label.GetComponent<RectTransform>());
            Outline(label);

            EventTrigger.Entry down = new EventTrigger.Entry();
            down.eventID = EventTriggerType.PointerDown;
            down.callback.AddListener((e) => SetAccel(true));
            trig.triggers.Add(down);

            EventTrigger.Entry up = new EventTrigger.Entry();
            up.eventID = EventTriggerType.PointerUp;
            up.callback.AddListener((e) => SetAccel(false));
            trig.triggers.Add(up);

            EventTrigger.Entry cancel = new EventTrigger.Entry();
            cancel.eventID = EventTriggerType.PointerCancel;
            cancel.callback.AddListener((e) => SetAccel(false));
            trig.triggers.Add(cancel);
        }

        private void SetAccel(bool on)
        {
            IsAccelPressed = on;
            if (btnAccelImg != null)
                btnAccelImg.color = on ? accelPressed : accelNormal;
            if (accelerateCb != null) accelerateCb(on);
        }

        // ¿El dedo sigue sobre el botón ACELERAR?
        public bool IsAccelPressed { get; private set; }

        public void BindAccelerate(System.Action<bool> cb)
        {
            accelerateCb = cb;
        }

        // ---------- información en pantalla ----------

        public void SetRaceInfo(int position, int lap, int totalLaps)
        {
            if (lblRace != null)
                lblRace.text = "VUELTA " + lap + "/" + totalLaps +
                               " · POS " + position + "/4";
        }

        public void ShowCountdown(string text)
        {
            if (lblCountdown != null) lblCountdown.text = text;
        }

        public void HideCountdown()
        {
            if (lblCountdown != null) lblCountdown.text = "";
        }

        public void RefreshCoins()
        {
            if (lblCoins != null && Geayi.Core.GameManager.Instance != null)
                lblCoins.text = Geayi.Core.GameManager.Instance.Coins + "";
        }

        public void OnCoinsChanged(int coins)
        {
            if (lblCoins != null) lblCoins.text = coins + "";
        }

        public void SetVisible(bool v)
        {
            if (root != null) root.gameObject.SetActive(v);
        }

        // Suelta el joystick y el botón ACELERAR (al volver al menú)
        public void ResetInput()
        {
            SetAccel(false);
            if (Joystick != null) Joystick.ForceRelease();
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
