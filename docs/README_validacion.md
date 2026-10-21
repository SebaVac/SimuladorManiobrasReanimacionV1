# Paquete de validación clínica — índice

Estos documentos se generan desde los assets del módulo de Razonamiento Clínico (borrador pendiente de validación clínica). Nada de lo que contienen ha sido validado por un profesional todavía.

| Documento | Qué contiene | Quién lo regenera |
|---|---|---|
| [validacion_clinica_flags.md](validacion_clinica_flags.md) / [.csv](validacion_clinica_flags.csv) | Por caso: preguntas, hallazgos, acciones críticas, errores frecuentes y diagnóstico, con las marcas inferidas (`esPreguntaClave`, `esHallazgoRelevante`) y dos columnas vacías para validar | `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` |
| [conjuntos_manejo.md](conjuntos_manejo.md) | Los 14 conjuntos de palabras clave de las acciones de Manejo (7 casos × 2) y su frase de prueba | `Tools/Razonamiento Clínico/Conjuntos de Manejo/2. Generar docs/conjuntos_manejo.md` |
| [matriz_examenes.md](matriz_examenes.md) / [.csv](matriz_examenes.csv) | Clasificación de cada examen del catálogo en cada caso (Pertinente / Indiferente / No pertinente) y los resultados | `Tools/Razonamiento Clínico/Catálogo de exámenes/3. Generar matriz de exámenes (docs/)` |
| [errores_sin_condicion.md](errores_sin_condicion.md) / [.csv](errores_sin_condicion.csv) | Los 5 errores frecuentes con condición vacía, que hoy no se detectan solos | `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` |

## Instrucciones para quien revisa

Empieza por la nota inicial de `validacion_clinica_flags.md`, que explica qué marcas se infirieron y cuántos puntos afecta cada una (anamnesis 15, examen físico 10, exámenes 20). En cada sección, escribe **OK** en la columna «Validación (OK / Cambiar)» si estás de acuerdo con la fila, o **Cambiar** si no, y anota en «Comentario» qué debería ser (por ejemplo, que una pregunta no es clave, que un hallazgo sí es relevante, o qué palabra falta en un conjunto). Revisa aparte la matriz de exámenes (¿es pertinente, indiferente o no pertinente pedir cada examen en cada caso?) y los conjuntos de Manejo, y para cada error de la lista sin condición propón qué conducta del jugador debería dispararlo. Puedes trabajar sobre los `.csv` (se abren en Excel) o sobre los `.md`; devuelve el archivo con las columnas completadas.
