using System.Collections.Generic;
using UnityEngine;

// ── TIPOS DE DATOS ────────────────────────────────────────────────────────────
// Todos los tipos en este archivo son datos puros (sin lógica ni MonoBehaviour).
// FaseClinica y TriggerTransicion son parte integral de FichaCaso y no tienen
// sentido fuera de él, por lo que conviven en el mismo archivo.

[System.Serializable]
public class SignosVitales
{
    [Range(0, 300)] public int frecuenciaCardiaca;     // lpm
    [Range(0, 60)]  public int frecuenciaRespiratoria; // rpm
    [Range(0, 100)] public int saturacionO2;           // %
    public string presionArterial;                     // formato "120/80"
}

[System.Serializable]
public class AccionCritica
{
    public string id;
    public string nombre;
    [TextArea] public string descripcion;
}

[System.Serializable]
public class TriggerTransicion
{
    public enum TipoTrigger
    {
        AccionCriticaRealizada,   // acción reportada por sistema externo
        AccionCriticaOmitida,     // tiempo de fase expirado SIN haber realizado la acción
        TiempoExcedido,           // tiempo de fase expirado (independiente de acciones)
        SignoVitalFueraDeRango    // evaluación diferida — implementar en próxima iteración
    }

    public TipoTrigger tipo;
    public string referenciaAccionId; // id de AccionCritica definida en FichaCaso
    public string faseDestinoId;

    // Campos opcionales — solo relevantes cuando tipo == SignoVitalFueraDeRango
    public string signoVitalId;
    public float  rangoMin;
    public float  rangoMax;
}

[System.Serializable]
public class FaseClinica
{
    public string id;
    public string nombre;
    [TextArea] public string descripcion;
    public SignosVitales signosVitales;
    [Tooltip("Segundos máximos en esta fase. 0 o -1 = sin límite.")]
    public float tiempoMaximoSegundos;
    public List<TriggerTransicion> transiciones = new List<TriggerTransicion>();
}

// ── SCRIPTABLE OBJECT ─────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "NuevoCasoClinico", menuName = "Simulador/Ficha de Caso Clínico")]
public class FichaCaso : ScriptableObject
{
    [Header("Identificación")]
    public string id;
    public string nombre;
    [TextArea] public string descripcion;

    [Header("Fases")]
    [Tooltip("id de la fase en la que comienza el caso")]
    public string faseInicialId;
    public List<FaseClinica> fases = new List<FaseClinica>();

    [Header("Catálogo de Acciones Críticas")]
    public List<AccionCritica> accionesCriticas = new List<AccionCritica>();

    // Búsqueda lineal — lista pequeña (< 20 fases), llamada solo en transiciones
    public FaseClinica ObtenerFase(string idFase)
    {
        for (int i = 0; i < fases.Count; i++)
            if (fases[i].id == idFase) return fases[i];
        return null;
    }
}
