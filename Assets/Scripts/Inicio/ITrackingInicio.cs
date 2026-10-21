using System;
using UnityEngine;

/// <summary>
/// Origen de las señales de tracking que necesita <see cref="AlineadorInicioJugador"/>, separado de la lógica para
/// poder probarla en el Editor sin visor (<see cref="TrackingInicioSintetico"/>). La implementación real es
/// <see cref="TrackingInicioOVR"/>.
/// </summary>
public interface ITrackingInicio
{
    /// <summary>true si la cabeza ya tiene una pose de tracking válida (visor presente y pose distinta de la de defecto).</summary>
    bool PoseCabezaValida(Transform cabeza);

    /// <summary>El visor acaba de adquirir tracking.</summary>
    event Action TrackingAdquirido;

    /// <summary>El usuario recentró (botón Oculus) y el origen de tracking cambió.</summary>
    event Action Recentrado;

    /// <summary>Empieza a escuchar los eventos del SDK.</summary>
    void Conectar();

    /// <summary>Deja de escuchar (se llama al destruir el alineador).</summary>
    void Desconectar();

    /// <summary>Texto con el tipo de origen de tracking, solo para el log de diagnóstico.</summary>
    string DescribirOrigen();

    // ── Diagnóstico (solo para el log; ninguno cambia el comportamiento) ──

    /// <summary>Nombre de la condición con la que PoseCabezaValida dio por bueno el tracking la última vez que devolvió true.</summary>
    string CondicionCumplida { get; }

    /// <summary>Tipo de origen de tracking PEDIDO (el valor serializado en OVRManager).</summary>
    string DescribirOrigenPedido();

    /// <summary>Tipo de origen de tracking EFECTIVO que informa el runtime (OVRPlugin.GetTrackingOriginType).</summary>
    string DescribirOrigenEfectivo();

    /// <summary>Altura (Y) de la cabeza según el nodo de tracking, SIN pasar por la jerarquía del rig. false si no se puede leer.</summary>
    bool TryAlturaNodo(out float alturaY);

    /// <summary>Estado de cada subcondición de tracking (para el aviso de tiempo agotado).</summary>
    string Diagnosticar(Transform cabeza);
}
