#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Configura el punto de inicio del jugador en la escena activa (SceneDiagnostico o SceneSimulador; rechaza
/// SceneInicio). Idempotente:
///   - Crea el marcador <c>PuntoInicioJugador</c> si no existe (si existe, respeta su posición y giro).
///   - Añade <see cref="AlineadorInicioJugador"/> al OVRCameraRig y lo cablea.
///   - OVRManager.trackingOriginType = FloorLevel y rig con Y = 0 (no se fuerza la altura de la cabeza).
/// No toca CalibradorPosicion, MotorEvaluacion ni nada de puntaje. No ejecuta ConstruirArranqueSeccion2.
/// Menú: Tools/Inicio del jugador/Configurar escena actual
/// </summary>
public static class ConfigurarInicioJugador
{
    const string NOMBRE_MARCADOR = "PuntoInicioJugador";

    [MenuItem("Tools/Inicio del jugador/Configurar escena actual")]
    public static void Configurar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico" && escena.name != "SceneSimulador")
        {
            EditorUtility.DisplayDialog("Escena no admitida",
                "El punto de inicio solo se configura en SceneDiagnostico y SceneSimulador.\nActual: " + escena.name, "OK");
            return;
        }

        var rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig == null) { Debug.LogError("[InicioJugador] No hay OVRCameraRig en la escena."); return; }

        // Marcador
        var marcador = Object.FindFirstObjectByType<PuntoInicioJugador>();
        if (marcador == null)
        {
            var go = new GameObject(NOMBRE_MARCADOR);
            Undo.RegisterCreatedObjectUndo(go, "Crear PuntoInicioJugador");
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            marcador = go.AddComponent<PuntoInicioJugador>();
        }

        // Origen de tracking y altura del rig
        var ovr = rig.GetComponent<OVRManager>();
        if (ovr != null && ovr.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel)
        {
            ovr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            EditorUtility.SetDirty(ovr);
        }
        var p = rig.transform.position;
        if (Mathf.Abs(p.y) > 1e-5f) rig.transform.position = new Vector3(p.x, 0f, p.z);

        // Alineador
        var alineador = rig.GetComponent<AlineadorInicioJugador>();
        if (alineador == null) alineador = Undo.AddComponent<AlineadorInicioJugador>(rig.gameObject);
        var so = new SerializedObject(alineador);
        so.FindProperty("marcador").objectReferenceValue = marcador;
        so.FindProperty("cabeza").objectReferenceValue = rig.centerEyeAnchor;
        so.FindProperty("raizRig").objectReferenceValue = rig.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(alineador);
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log("[InicioJugador] " + escena.name + ": marcador en " + marcador.transform.position + ", alineador en " + rig.name
                  + ", origen " + (ovr != null ? ovr.trackingOriginType.ToString() : "sin OVRManager") + ". Guarda la escena.");
    }
}
#endif
