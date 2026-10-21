# Matriz de exámenes — 7 casos × catálogo

Generada por `Tools/Razonamiento Clínico/Catálogo de exámenes/3. Generar matriz` (borrador pendiente de validación clínica).

- **P** = pertinente: pedirlo suma y entra en el denominador del componente Exámenes (20 puntos).
- **I** = indiferente: ni suma ni resta (no cuenta en el denominador ni en la penalización). Son los resultados anormales PROVISIONALES.
- **N** = no pertinente: pedirlo resta (0,25 × el valor de un examen pertinente). Es el valor por defecto de todo lo que la ficha no trae.

## Clasificación

| Categoría | Examen | CASO-SCA | CASO-TBC | CASO-EPA | CASO-EPOC | CASO-NAC | CASO-SARS | CASO-BRONQ |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Imágenes | Radiografía de tórax | N | P | P | P | P | P | P |
| Imágenes | Tomografía de tórax | N | N | N | N | N | I | N |
| Cardiología | Electrocardiograma | P | N | P | N | N | N | N |
| Cardiología | Ecocardiograma | N | N | P | N | N | N | N |
| Cardiología | Troponina | P | N | P | N | N | N | N |
| Cardiología | BNP/NT-proBNP | N | N | P | N | N | N | N |
| Laboratorio general | Hemograma | N | I | N | N | P | P | N |
| Laboratorio general | Proteína C reactiva | N | N | N | N | I | P | N |
| Laboratorio general | Electrolitos plasmáticos | N | N | N | N | N | N | N |
| Laboratorio general | Función renal | N | N | I | N | N | N | N |
| Laboratorio general | Glicemia | I | N | N | N | N | N | N |
| Laboratorio general | Dímero D | N | N | N | N | N | N | N |
| Gases | Gases arteriales | N | N | I | P | I | N | N |
| Microbiología | Baciloscopia | N | P | N | N | N | N | N |
| Microbiología | Cultivo de expectoración | N | P | N | N | P | N | N |
| Microbiología | PCR viral / panel respiratorio | N | N | N | N | N | P | N |
| Microbiología | PPD | N | P | N | N | N | N | N |
| Función pulmonar | Espirometría | N | N | N | P | N | N | N |
| Evaluación clínica | Exploración clínica del dolor | P | I | I | I | I | I | I |

## Resultados que trae cada ficha (P e I)

Todo examen que no aparece aquí devuelve el resultado normal del catálogo (sección siguiente).

### CASO-SCA — Síndrome Coronario Agudo (STEMI)

| Examen | Clasif. | Resultado |
|---|:-:|---|
| Electrocardiograma | P | Supradesnivel del segmento ST en derivaciones anteriores. |
| Troponina | P | Troponina elevada, con curva ascendente. |
| Exploración clínica del dolor | P | Dolor retroesternal opresivo, EVA 8/10. |
| Glicemia | I | Elevada (aprox. 210 mg/dL). |

### CASO-TBC — Tuberculosis pulmonar

| Examen | Clasif. | Resultado |
|---|:-:|---|
| Baciloscopia | P | Positiva (++), presencia de bacilos ácido-alcohol resistentes. |
| Cultivo de expectoración | P | Positivo para Mycobacterium tuberculosis (resultado diferido). |
| Radiografía de tórax | P | Infiltrado cavitado en lóbulo superior derecho. |
| PPD | P | Induración >15 mm, positiva. |
| Hemograma | I | Anemia leve (Hb aprox. 11 g/dL), sin leucocitosis. |
| Exploración clínica del dolor | I | Sin dolor referido en reposo (EVA 0/10). |

### CASO-EPA — Edema Pulmonar Agudo (cardiogénico)

| Examen | Clasif. | Resultado |
|---|:-:|---|
| Radiografía de tórax | P | Infiltrado alveolar bilateral en "alas de mariposa", redistribución vascular, cardiomegalia. |
| Electrocardiograma | P | Taquicardia sinusal, signos de sobrecarga ventricular izquierda. |
| BNP/NT-proBNP | P | Elevado (>500 pg/mL). |
| Troponina | P | Levemente elevada. |
| Ecocardiograma | P | FEVI reducida (~35%), disfunción diastólica. |
| Gases arteriales | I | Hipoxemia (PaO2 aprox. 55 mmHg) con hipocapnia leve. |
| Función renal | I | Creatinina levemente elevada (1,4 mg/dL). |
| Exploración clínica del dolor | I | Sin dolor referido en reposo (EVA 0/10). |

### CASO-EPOC — Exacerbación de EPOC

| Examen | Clasif. | Resultado |
|---|:-:|---|
| Espirometría | P | Patrón obstructivo, VEF1/CVF < 0,70 post-broncodilatador. |
| Gases arteriales | P | Hipoxemia leve-moderada, PaCO2 normal o levemente elevada. |
| Radiografía de tórax | P | Hiperinsuflación, aplanamiento diafragmático. |
| Exploración clínica del dolor | I | Sin dolor referido en reposo (EVA 0/10). |

### CASO-NAC — Neumonía adquirida en la comunidad

| Examen | Clasif. | Resultado |
|---|:-:|---|
| Radiografía de tórax | P | Consolidación con broncograma aéreo en lóbulo inferior derecho. |
| Hemograma | P | Leucocitosis con desviación a izquierda. |
| Cultivo de expectoración | P | Pendiente / positivo según el agente causal. |
| Proteína C reactiva | I | Elevada (aprox. 120 mg/L). |
| Gases arteriales | I | Hipoxemia leve (PaO2 aprox. 62 mmHg), pH normal. |
| Exploración clínica del dolor | I | Dolor pleurítico al inspirar (EVA 4/10). |

### CASO-SARS — Síndrome Respiratorio Agudo Grave (SARS)

| Examen | Clasif. | Resultado |
|---|:-:|---|
| Hemograma | P | Leucopenia con linfopenia. |
| Proteína C reactiva | P | Elevada. |
| PCR viral / panel respiratorio | P | PCR positiva para el agente causal. |
| Radiografía de tórax | P | Infiltrados bilaterales, patrón en vidrio esmerilado. |
| Tomografía de tórax | I | Opacidades en vidrio esmerilado bilaterales. |
| Exploración clínica del dolor | I | Sin dolor referido en reposo (EVA 0/10). |

### CASO-BRONQ — Bronquitis aguda

| Examen | Clasif. | Resultado |
|---|:-:|---|
| Radiografía de tórax | P | Sin infiltrados ni consolidación; campos pulmonares limpios (el diagnóstico es principalmente clínico). |
| Exploración clínica del dolor | I | Sin dolor referido en reposo (EVA 0/10). |

## Resultado normal del catálogo

| Categoría | Examen | Resultado normal |
|---|---|---|
| Imágenes | Radiografía de tórax | Sin infiltrados ni consolidación; silueta cardiomediastínica normal y senos costofrénicos libres. |
| Imágenes | Tomografía de tórax | Parénquima pulmonar sin lesiones; mediastino y pleura normales. |
| Cardiología | Electrocardiograma | Ritmo sinusal, FC 75 lpm, eje normal, sin alteraciones del ST-T. |
| Cardiología | Ecocardiograma | Función sistólica conservada (FEVI aprox. 60%), sin alteraciones valvulares ni derrame pericárdico. |
| Cardiología | Troponina | Dentro de rango normal (< 0,04 ng/mL). |
| Cardiología | BNP/NT-proBNP | Normal (BNP < 100 pg/mL; NT-proBNP < 300 pg/mL). |
| Laboratorio general | Hemograma | Hb 14 g/dL, leucocitos 7.000 por mm³ con fórmula normal, plaquetas 250.000 por mm³. |
| Laboratorio general | Proteína C reactiva | Normal (< 5 mg/L). |
| Laboratorio general | Electrolitos plasmáticos | Na 140 mEq/L, K 4,2 mEq/L, Cl 102 mEq/L; dentro de rango. |
| Laboratorio general | Función renal | Creatinina 0,9 mg/dL, BUN 14 mg/dL; normal. |
| Laboratorio general | Glicemia | Normal (aprox. 90 mg/dL en ayunas). |
| Laboratorio general | Dímero D | Negativo (< 500 ng/mL). |
| Gases | Gases arteriales | pH 7,40, PaO2 95 mmHg, PaCO2 40 mmHg, HCO3 24 mEq/L; normales. |
| Microbiología | Baciloscopia | Negativa, sin bacilos ácido-alcohol resistentes. |
| Microbiología | Cultivo de expectoración | Flora normal de vía aérea superior, sin patógeno aislado. |
| Microbiología | PCR viral / panel respiratorio | Negativo para los agentes virales del panel. |
| Microbiología | PPD | Induración < 5 mm, negativa. |
| Función pulmonar | Espirometría | Normal: VEF1/CVF > 0,70 y VEF1 > 80% del predicho. |
| Evaluación clínica | Exploración clínica del dolor | Sin dolor referido en reposo (EVA 0/10). |
