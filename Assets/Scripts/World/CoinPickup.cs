// CoinPickup.cs (GEAYI Racing) — monedas doradas sobre la pista.
// Al tocarlas el jugador: +monedas y boost de velocidad (3 s).
// Se crean con CoinPickup.Spawn(padre, posición); giran y flotan solas.
using UnityEngine;
using Geayi.Core;

namespace Geayi.World
{
    public class CoinPickup : MonoBehaviour
    {
        public int value = 5;

        private Transform playerTr;
        private float bobPhase;
        private Vector3 basePos;

        private static Material coinMat;
        private static Mesh coinMesh;

        public static void Spawn(Transform parent, Vector3 pos, int coinValue = 5)
        {
            if (coinMesh == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coinMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
            }
            if (coinMat == null)
            {
                Shader s = Shader.Find("Standard");
                if (s == null) s = Shader.Find("UI/Default");
                coinMat = new Material(s);
                coinMat.color = new Color(1f, 0.78f, 0.15f);
                coinMat.EnableKeyword("_EMISSION");
                coinMat.SetColor("_EmissionColor", new Color(1f, 0.70f, 0.10f) * 0.9f);
            }
            GameObject go = new GameObject("Coin");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            // Moneda GRANDE y VERTICAL como la web (aro dorado de pie)
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = coinMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = coinMat;
            var cp = go.AddComponent<CoinPickup>();
            cp.basePos = pos;
            cp.value = coinValue;
        }

        void Start()
        {
            // El carro del jugador lleva tag "Player" (lo pone RaceManager)
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTr = p.transform;
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            // Moneda grande de pie (1.3m de diámetro), como los aros de la web
            transform.localScale = new Vector3(1.3f, 1.3f, 0.32f);
        }

        void Update()
        {
            // Gira sobre su eje vertical como las monedas de la web + flota
            transform.Rotate(Vector3.up, 200f * Time.deltaTime, Space.World);
            transform.position = basePos + new Vector3(0f, 0.25f + Mathf.Sin(Time.time * 2.5f + bobPhase) * 0.15f, 0f);

            if (playerTr == null || GameManager.Instance == null) return;
            if (RaceManager.Instance == null || !RaceManager.Instance.RaceRunning)
                return; // solo cuentan durante la carrera

            Vector3 pp = playerTr.position + new Vector3(0f, 1f, 0f);
            float d = Vector3.Distance(transform.position, pp);

            if (d < 2.2f)
            {
                GameManager.Instance.AddCoins(value);
                RaceManager.Instance.BoostPlayer(); // boost de 3 segundos
                gameObject.SetActive(false); // recogida
            }
        }
    }
}
