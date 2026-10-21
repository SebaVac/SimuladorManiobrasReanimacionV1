using System;
using UnityEngine;

/// <summary>
/// Tracking inyectable para pruebas en el Editor (sin visor): quien prueba decide cuándo la pose es válida y
/// dispara a mano el evento de adquisición y el de recentrado. No se usa en juego.
/// </summary>
public class TrackingInicioSintetico : ITrackingInicio
{
    public bool poseValida;
    public string origen = "Sintético (FloorLevel)";
    public bool conectado;

    public event Action TrackingAdquirido;
    public event Action Recentrado;

    public bool PoseCabezaValida(Transform cabeza) => poseValida;
    public void Conectar() { conectado = true; }
    public void Desconectar() { conectado = false; }
    public string DescribirOrigen() => origen;

    // Diagnóstico sintético: valores fijos o inyectables para el banco de pruebas
    public string condicion = "sintética (poseValida)";
    public string origenPedido = "Sintético (FloorLevel pedido)";
    public string origenEfectivo = "Sintético (FloorLevel efectivo)";
    public float alturaNodo = float.NaN;
    public string CondicionCumplida => condicion;
    public string DescribirOrigenPedido() => origenPedido;
    public string DescribirOrigenEfectivo() => origenEfectivo;
    public bool TryAlturaNodo(out float alturaY) { alturaY = alturaNodo; return !float.IsNaN(alturaNodo); }
    public string Diagnosticar(Transform cabeza) => "sintético: poseValida=" + poseValida;

    public void DispararTrackingAdquirido() { poseValida = true; TrackingAdquirido?.Invoke(); }
    public void DispararRecentrado() => Recentrado?.Invoke();
}
