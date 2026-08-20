using UnityEngine;
using TMPro;

public class GestorGuiaManos : MonoBehaviour
{
    public LogicaRCP logica;
    public TextMeshProUGUI textoEstado;
    public Renderer rendererCirculoEstado;

    void Update()
    {
        if (logica == null || logica.txtInfoManos == null) return;

        string texto = logica.txtInfoManos.text;
        Color color = logica.txtInfoManos.color;

        if (textoEstado != null)
        {
            textoEstado.text = texto;
            textoEstado.color = color;
        }

        if (rendererCirculoEstado != null)
            rendererCirculoEstado.material.color = color;
    }
}
