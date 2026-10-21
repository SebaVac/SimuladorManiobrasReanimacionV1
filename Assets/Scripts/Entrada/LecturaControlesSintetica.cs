using UnityEngine;

/// <summary>
/// Lectura inyectable para pruebas en el Editor (Play Mode sin controles reales): quien prueba fija
/// origen, dirección, gatillo y presencia de cada mano/control. Se asigna a
/// <see cref="EntradaControlesTouch.Lectura"/>. No se usa en juego.
/// </summary>
public class LecturaControlesSintetica : ILecturaControles
{
    public readonly bool[] controlPresente = new bool[2];
    public readonly Vector3[] origen = new Vector3[2];
    public readonly Vector3[] direccion = { Vector3.forward, Vector3.forward };
    public readonly bool[] gatillo = new bool[2];
    public readonly bool[] manoPresente = new bool[2];
    public readonly Vector3[] posicionMano = new Vector3[2];

    public bool ControlPresente(int mano) => controlPresente[mano];
    public bool LeerPoseControl(int mano, out Vector3 o, out Vector3 d) { o = origen[mano]; d = direccion[mano]; return controlPresente[mano]; }
    public bool GatilloPulsado(int mano) => gatillo[mano];
    public bool ManoPresente(int mano) => manoPresente[mano];
    public bool LeerPosicionMano(int mano, out Vector3 p) { p = posicionMano[mano]; return manoPresente[mano]; }
}
