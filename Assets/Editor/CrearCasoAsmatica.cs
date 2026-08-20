#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera el ScriptableObject de prueba "Crisis Asmática" con 4 nodos:
///
///   Estable ──(broncodilatador)──→ Estabilizado        (terminal positivo)
///           ──(tiempo 60s)──→ Deterioro leve
///                             ──(broncodilatador)──→ Estabilizado
///                             ──(tiempo 90s)──→ Deterioro severo  (terminal negativo)
///
/// Menú: Tools > Crear Caso Clínico — Crisis Asmática (Prueba)
/// </summary>
public static class CrearCasoAsmatica
{
    const string CARPETA_RESOURCES  = "Assets/Resources";
    const string CARPETA_CASOS      = "Assets/Resources/CasosClinicos";
    const string RUTA_ASSET         = "Assets/Resources/CasosClinicos/Caso_CrisisAsmatica.asset";

    [MenuItem("Tools/Crear Caso Clínico — Crisis Asmática (Prueba)")]
    static void Crear()
    {
        // Crear carpetas si no existen
        if (!AssetDatabase.IsValidFolder(CARPETA_RESOURCES))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(CARPETA_CASOS))
            AssetDatabase.CreateFolder(CARPETA_RESOURCES, "CasosClinicos");

        if (AssetDatabase.LoadAssetAtPath<FichaCaso>(RUTA_ASSET) != null)
        {
            EditorUtility.DisplayDialog("Asset ya existente",
                $"Ya existe un asset en:\n{RUTA_ASSET}\n\n" +
                "Eliminalo desde el Project antes de regenerarlo.", "OK");
            return;
        }

        var caso         = ScriptableObject.CreateInstance<FichaCaso>();
        caso.id          = "crisis_asmatica_01";
        caso.nombre      = "Crisis Asmática — Caso Tipo";
        caso.descripcion = "Paciente de 28 años, antecedente asmático conocido. Consulta por disnea " +
                           "progresiva de 2 horas de evolución. No utilizó broncodilatador en domicilio.";
        caso.faseInicialId = "estable";

        // ── Catálogo de acciones críticas ─────────────────────────────────────
        caso.accionesCriticas = new List<AccionCritica>
        {
            new AccionCritica
            {
                id          = "broncodilatador",
                nombre      = "Administrar broncodilatador",
                descripcion = "Salbutamol 2-4 puff con aerocámara. Acción de primera línea."
            },
            new AccionCritica
            {
                id          = "posicion_fowler",
                nombre      = "Colocar en posición Fowler",
                descripcion = "Paciente incorporado 45–90°. Reduce trabajo respiratorio."
            },
            new AccionCritica
            {
                id          = "oxigeno_mascara",
                nombre      = "Aplicar oxígeno con mascarilla",
                descripcion = "O₂ 4–8 L/min. Indicado si SatO₂ < 92%."
            }
        };

        // ── Signos vitales por fase ───────────────────────────────────────────
        var svEstable = new SignosVitales
            { frecuenciaCardiaca = 95,  frecuenciaRespiratoria = 22, saturacionO2 = 93, presionArterial = "130/85" };

        var svDeterioroLeve = new SignosVitales
            { frecuenciaCardiaca = 118, frecuenciaRespiratoria = 28, saturacionO2 = 88, presionArterial = "140/90" };

        var svDeterioroSevero = new SignosVitales
            { frecuenciaCardiaca = 142, frecuenciaRespiratoria = 36, saturacionO2 = 76, presionArterial = "90/60" };

        var svEstabilizado = new SignosVitales
            { frecuenciaCardiaca = 88,  frecuenciaRespiratoria = 17, saturacionO2 = 97, presionArterial = "125/80" };

        // ── Fase 1: Estable ───────────────────────────────────────────────────
        // Prioridad de evaluación (orden en lista):
        //   1. broncodilatador administrado → Estabilizado  (rescata antes de que el tiempo expire)
        //   2. tiempo excedido (60 s)        → Deterioro leve
        var faseEstable = new FaseClinica
        {
            id          = "estable",
            nombre      = "Estable",
            descripcion = "Paciente consciente, bradipneico leve. Sibilancias espiratorias. " +
                          "SatO₂ en límite inferior tolerable. Ventana de intervención abierta.",
            signosVitales        = svEstable,
            tiempoMaximoSegundos = 60f,
            transiciones = new List<TriggerTransicion>
            {
                new TriggerTransicion
                {
                    tipo               = TriggerTransicion.TipoTrigger.AccionCriticaRealizada,
                    referenciaAccionId = "broncodilatador",
                    faseDestinoId      = "estabilizado"
                },
                new TriggerTransicion
                {
                    tipo          = TriggerTransicion.TipoTrigger.TiempoExcedido,
                    faseDestinoId = "deterioro_leve"
                }
            }
        };

        // ── Fase 2: Deterioro leve ────────────────────────────────────────────
        // El broncodilatador aún puede revertir el cuadro si se administra aquí.
        // Sin intervención en 90 s → estado terminal negativo.
        var faseDeterioroLeve = new FaseClinica
        {
            id          = "deterioro_leve",
            nombre      = "Deterioro Leve",
            descripcion = "Broncoespasmo progresivo. Uso de músculos accesorios. " +
                          "SatO₂ en descenso. Paciente ansioso. Ventana de rescate aún abierta.",
            signosVitales        = svDeterioroLeve,
            tiempoMaximoSegundos = 90f,
            transiciones = new List<TriggerTransicion>
            {
                new TriggerTransicion
                {
                    tipo               = TriggerTransicion.TipoTrigger.AccionCriticaRealizada,
                    referenciaAccionId = "broncodilatador",
                    faseDestinoId      = "estabilizado"
                },
                new TriggerTransicion
                {
                    tipo          = TriggerTransicion.TipoTrigger.TiempoExcedido,
                    faseDestinoId = "deterioro_severo"
                }
            }
        };

        // ── Fase 3a: Deterioro severo — terminal negativo ─────────────────────
        var faseDeterioroSevero = new FaseClinica
        {
            id          = "deterioro_severo",
            nombre      = "Deterioro Severo",
            descripcion = "Insuficiencia respiratoria crítica. Cianosis perioral. " +
                          "Paciente agotado. Requiere intubación urgente. Caso finalizado.",
            signosVitales        = svDeterioroSevero,
            tiempoMaximoSegundos = -1f,                       // sin límite — estado terminal
            transiciones         = new List<TriggerTransicion>() // sin salida
        };

        // ── Fase 3b: Estabilizado — terminal positivo ─────────────────────────
        var faseEstabilizado = new FaseClinica
        {
            id          = "estabilizado",
            nombre      = "Estabilizado",
            descripcion = "Respuesta favorable al broncodilatador. SatO₂ en recuperación. " +
                          "Paciente calmado, frecuencia respiratoria normalizando. Caso finalizado.",
            signosVitales        = svEstabilizado,
            tiempoMaximoSegundos = -1f,
            transiciones         = new List<TriggerTransicion>()
        };

        caso.fases = new List<FaseClinica>
        {
            faseEstable,
            faseDeterioroLeve,
            faseDeterioroSevero,
            faseEstabilizado
        };

        // ── Guardar asset ─────────────────────────────────────────────────────
        AssetDatabase.CreateAsset(caso, RUTA_ASSET);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = caso;
        EditorUtility.FocusProjectWindow();

        Debug.Log($"[CrearCasoAsmatica] Asset creado → {RUTA_ASSET}");
        EditorUtility.DisplayDialog(
            "Caso creado ✓",
            $"Asset guardado en:\n{RUTA_ASSET}\n\n" +
            "Pasos para conectar en escena:\n" +
            "1. Agregar GestorCasoClinico a un GameObject\n" +
            "2. Asignar el asset al campo 'Caso Activo'\n" +
            "3. Agregar MotorEvolucionPaciente al mismo (u otro) GO\n" +
            "4. Asignar el GestorCasoClinico al Motor\n" +
            "5. Suscribirse a OnFaseCambiada para recibir transiciones\n\n" +
            "Prueba rápida en Play Mode:\n" +
            "  motor.ReportarAccionRealizada(\"broncodilatador\")  → Estabilizado\n" +
            "  Esperar 60 s sin acción                            → Deterioro leve\n" +
            "  Esperar 90 s más                                   → Deterioro severo",
            "OK");
    }
}
#endif
