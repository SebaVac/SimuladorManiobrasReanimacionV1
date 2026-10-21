# Errores frecuentes sin condición (no se detectan automáticamente)

Generado por `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` (borrador pendiente de validación clínica).

Un error frecuente solo resta puntaje (−5 cada uno, tope −15) si su `idReferenciaCondicion` tiene el formato `prefijo:valor` con prefijo `omitio`, `pregunto`, `examino` o `solicito`. Los de esta lista tienen la condición **vacía** (o con un formato que el motor no reconoce): hoy se muestran en el asset pero **nunca se detectan ni restan**. Decidir para cada uno: ¿qué conducta del jugador debería dispararlo?

- **CASO-TBC** — No usar EPP (mascarilla de alta eficiencia) antes de examinar · condición: (vacía) · retroalimentación: Ante la sospecha de TBC bacilífera, la protección respiratoria del examinador debe colocarse ANTES del contacto. · Validación: ____ · Condición propuesta: ____
- **CASO-EPA** — Confundir el cuadro con una infección respiratoria pura · condición: (vacía) · retroalimentación: La ausencia de fiebre, la ortopnea, las yugulares ingurgitadas y el edema orientan a causa cardiogénica, no infecciosa. · Validación: ____ · Condición propuesta: ____
- **CASO-EPOC** — Administrar oxígeno a alto flujo sin control · condición: (vacía) · retroalimentación: En el retenedor de CO2 la hiperoxia puede deprimir el estímulo respiratorio; titular O2 a una SatO2 objetivo de 88-92%. · Validación: ____ · Condición propuesta: ____
- **CASO-SARS** — Retrasar el inicio de la oxigenoterapia · condición: (vacía) · retroalimentación: Con SatO2 90% y disnea progresiva, la oxigenoterapia no debe esperar a la confirmación diagnóstica. · Validación: ____ · Condición propuesta: ____
- **CASO-BRONQ** — Indicar antibióticos sin justificación · condición: (vacía) · retroalimentación: La bronquitis aguda es habitualmente viral; el antibiótico no acorta el curso y expone a efectos adversos y resistencia. · Validación: ____ · Condición propuesta: ____

Total: 5 errores sin condición válida.
