using UnityEngine;

/// <summary>Entrada activa en este momento: manos (seguimiento nativo) o controles Touch.</summary>
public enum ModoEntrada { Manos = 0, Controles = 1 }

/// <summary>Dispositivo de entrada usado durante un caso (se registra en ResultadoCaso; no afecta el puntaje).</summary>
public enum DispositivoEntrada { Manos = 0, Controles = 1, Mixto = 2 }

/// <summary>
/// Lectura CRUDA del dispositivo, separada de la lógica (selección por rayo, punta virtual, elección de
/// entrada activa) para poder probar esa lógica en el Editor inyectando origen, dirección del rayo y estado
/// del gatillo (<see cref="LecturaControlesSintetica"/>). La implementación real es
/// <see cref="LecturaControlesOVR"/>. Índice de mano: 0 = izquierda, 1 = derecha.
/// </summary>
public interface ILecturaControles
{
    /// <summary>El control está conectado y con tracking de posición.</summary>
    bool ControlPresente(int mano);

    /// <summary>Pose de apuntado del control (mundo). false si no hay pose válida.</summary>
    bool LeerPoseControl(int mano, out Vector3 origen, out Vector3 direccion);

    /// <summary>Gatillo de índice pulsado (estado, no flanco: el flanco lo calcula EntradaControlesTouch).</summary>
    bool GatilloPulsado(int mano);

    /// <summary>La mano está siendo seguida por el hand tracking.</summary>
    bool ManoPresente(int mano);

    /// <summary>Posición de la mano seguida (para detectar actividad). false si no hay.</summary>
    bool LeerPosicionMano(int mano, out Vector3 posicion);
}
