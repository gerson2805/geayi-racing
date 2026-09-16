// RaceAI.cs — piloto automático: sigue los waypoints del circuito
// Usa el mismo VehicleController arcade que el jugador (misma física).
// Solo acelera y gira cuando la carrera está en marcha.
using UnityEngine;
using System.Collections.Generic;
using Geayi.Vehicles;

namespace Geayi.AI
{
    public class RaceAI : MonoBehaviour
    {
        [Header("Referencias (las pone RaceManager)")]
        public VehicleController vehicle;
        public List<Vector3> waypoints;

        [Header("Progreso (lo lee RaceManager)")]
        public int wpIndex = 0;
        public int lap = 0;

        [Header("Manejo")]
        public float baseMaxSpeed = 18f;
        [Tooltip("Qué tan fuerte gira hacia el waypoint (grados de tolerancia)")]
        public float steerTolerance = 35f;
        [Tooltip("Distancia para dar el waypoint por alcanzado")]
        public float wpRadius = 7f;

        private Geayi.Core.RaceManager rm;

        void Start()
        {
            if (vehicle == null) vehicle = GetComponent<VehicleController>();
            rm = Geayi.Core.RaceManager.Instance;
            if (vehicle != null) vehicle.maxSpeed = baseMaxSpeed;
        }

        void Update()
        {
            if (vehicle == null || waypoints == null || waypoints.Count == 0) return;
            bool running = rm != null && rm.RaceRunning;
            vehicle.SetThrottle(running);
            if (!running)
            {
                vehicle.SetSteer(0f);
                return;
            }

            // Avanzar al siguiente waypoint al acercarse
            Vector3 pp = vehicle.transform.position; pp.y = 0f;
            Vector3 tp = waypoints[wpIndex]; tp.y = 0f;
            if (Vector3.Distance(pp, tp) < wpRadius)
            {
                wpIndex++;
                if (wpIndex >= waypoints.Count)
                {
                    wpIndex = 0;
                    lap++;
                }
                tp = waypoints[wpIndex]; tp.y = 0f;
            }

            // Girar hacia el waypoint (ángulo con signo)
            Vector3 to = tp - pp;
            if (to.sqrMagnitude < 0.01f) return;
            float ang = Vector3.SignedAngle(vehicle.transform.forward,
                                            to.normalized, Vector3.up);
            vehicle.SetSteer(Mathf.Clamp(ang / steerTolerance, -1f, 1f));
        }
    }
}
