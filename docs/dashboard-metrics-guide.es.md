# Guía rápida del dashboard de MarketAgent

## 1. Overview

MarketAgent es un dashboard de inteligencia técnica para seguir una watchlist seleccionada de activos, con foco en acciones, CEDEARs, ETFs y exposición Argentina/Latam.

El sistema:

- Monitorea una watchlist configurable.
- Usa snapshots de mercado persistidos en base de datos.
- Genera señales técnicas sobre esos snapshots.
- Muestra candidatos, oportunidades por upside y riesgos.
- Permite hidratar símbolos de la watchlist de forma manual.
- Mantiene el briefing de IA como acción manual/configurable para controlar costo.

La idea del demo: mostrar que el producto no depende de una vista estática, sino de datos persistidos, señales calculadas y estados observables.

## 2. Badges de estado

| Badge | Qué significa | Cómo interpretarlo |
|---|---|---|
| API online/offline | Indica si el frontend puede comunicarse con el backend. | Si está offline, el dashboard puede mostrar vista previa o datos incompletos. |
| Auto-refresh activo | El frontend refresca datos existentes cada 60 segundos. | No genera señales ni llama IA; solo lee endpoints GET. |
| Mercado abierto/cerrado | Estado aproximado de mercado USA regular. | Cerrado no implica error: puede mostrar el último snapshot válido. |
| Vista previa | Se está mostrando fallback/mock porque la API no respondió. | Útil para layout, no para análisis real. |
| Última actualización | Última vez que el dashboard refrescó datos. | Sirve para validar que la UI está viva. |
| Último snapshot | Timestamp del dato de mercado más reciente disponible. | Ayuda a validar frescura de datos. |
| Última señal | Timestamp de la señal persistida más reciente. | Indica cuándo se generó el último set de señales. |

## 3. Campos de la tabla de señales

| Campo | Significado | Cálculo / aproximación | Interpretación rápida |
|---|---|---|---|
| Precio | Precio actual o último precio persistido. | Viene del snapshot o del precio asociado a la señal. | Valida frescura y escala del activo. Ej: NVDA a 900 no se analiza igual que RGTI a 2. |
| Señal | Marcador visual de eventos técnicos destacados. | Derivado de condiciones como reversión, momentum o riesgo. | Ayuda a detectar patrones sin leer todo el detalle. |
| Acción / Símbolo | Símbolo del activo y acción sugerida por el motor. | Acción calculada por reglas técnicas. | `Candidato` indica oportunidad técnica; `Esperar confirmación` indica vigilancia. |
| Confianza | Nivel cualitativo de confianza. | Derivado de score, setup y confirmaciones técnicas. | Alta/Media/Baja no es probabilidad exacta; es una etiqueta operacional. |
| Timeframe | Contexto temporal de la señal. | Puede venir del scanner o de señales persistidas. | `Persisted` indica que viene de datos guardados. |
| RS | Relative Strength vs benchmark, normalmente SPY. | Comparación del rendimiento relativo contra referencia de mercado. | Positivo = outperform. Ej: RS +3% sugiere fuerza relativa. |
| RVOL | Volumen relativo. | Volumen actual vs volumen promedio. | < 1 = volumen débil; > 1 = mayor participación. |
| EXT | Extensión del precio contra EMA o nivel de tendencia. | Distancia porcentual desde una media/referencia. | EXT alto puede indicar sobreextensión y peor punto de entrada. |
| RSI14 | Oscilador de momentum de 14 períodos. | RSI estándar sobre 14 períodos. | < 30 suele ser sobreventa; > 70 sobrecompra; 50-70 puede ser momentum constructivo. |
| EMA9 | Media móvil exponencial corta. | Promedio exponencial de 9 períodos. | Precio arriba de EMA9 puede indicar momentum de corto plazo. |
| EMA20 | Media móvil exponencial intermedia. | Promedio exponencial de 20 períodos. | Referencia común de tendencia/pullback. |
| EMA50 | Media móvil exponencial más lenta. | Promedio exponencial de 50 períodos. | Ayuda a leer tendencia más amplia. |
| ATR14 | Average True Range de 14 períodos. | Rango promedio de volatilidad, no dirección. | Útil para stops y sizing. ATR alto = activo más volátil. |

Ejemplo rápido: si un activo tiene precio arriba de EMA9/20/50, RS positivo, RVOL > 1 y RSI entre 50 y 70, puede estar mostrando momentum saludable. Si además EXT es muy alto, el riesgo de entrada tardía aumenta.

## 4. Campos de oportunidad y riesgo

| Campo | Qué significa | Cómo leerlo |
|---|---|---|
| Entry point | Precio técnico de entrada estimado. | Se usa como referencia, no como orden automática. |
| Take profit | Objetivo técnico de salida parcial o total. | El dashboard prioriza targets válidos por encima de entry. |
| Stop loss | Nivel de invalidación/riesgo. | Ayuda a estimar pérdida máxima técnica. |
| Risk/reward | Relación beneficio/riesgo. | > 1 indica que el upside potencial supera el riesgo estimado. |
| Upside | Potencial desde entry hasta take profit. | `((takeProfit - entry) / entry) * 100`. |
| Pullback | Setup donde se espera retroceso o confirmación. | No necesariamente entrada inmediata. |
| Momentum continuation | Continuación de tendencia con fuerza. | Suele combinar RS, RVOL y alineación de EMAs. |
| Strong bullish | Setup alcista con múltiples señales favorables. | Mirar si está extendido antes de interpretar como oportunidad. |
| Extended momentum | Momentum fuerte pero posiblemente sobreextendido. | Puede ser interesante, pero con mayor riesgo de mal timing. |

## 5. Mi watchlist

`Mi watchlist` es la lista local de activos que el usuario quiere monitorear. Se guarda en `localStorage`, no requiere login y no modifica la base de datos.

Importante: estar en la watchlist no significa tener señal activa.

| Estado | Qué significa |
|---|---|
| Setup activo | Hay una señal/setup persistido para ese símbolo. |
| Monitoreando | Hay precio/snapshot reciente, pero no setup activo. |
| Pendiente de actualizar | El ticker fue agregado, pero todavía no se hidrató. |
| Sin datos | Se intentó hidratar, pero el provider no devolvió datos. |
| Error | Falló algo inesperado en la hidratación. |

El badge `CEDEAR` marca activos que tienen o suelen tener representación CEDEAR para el público argentino. Algunos tickers pueden ser seleccionables aunque no tengan provider disponible todavía; en ese caso pueden aparecer como `Sin datos`.

## 6. Auto-refresh vs acciones manuales

MarketAgent separa lectura pasiva de acciones costosas.

| Acción | Qué hace | Costo / efecto |
|---|---|---|
| Auto-refresh | Lee datos persistidos con endpoints GET. | No genera señales, no llama IA, no ingiere mercado. |
| Generate Signals | Genera señales manualmente. | Acción explícita del usuario. |
| Generate AI Briefing | Genera briefing con IA si está habilitada. | Manual y configurable para controlar costo. |
| Actualizar watchlist | Hidrata solo los símbolos seleccionados. | No corre todo el ciclo; evita llamadas innecesarias. |

Esto es clave para el demo: el dashboard se siente vivo, pero las operaciones caras o mutantes son intencionales.

## 7. Caveats importantes

- Mercado cerrado o feriados pueden mostrar datos “viejos” pero válidos.
- Si la base free-tier está pausada o lenta, producción puede parecer sin datos aunque el frontend esté bien.
- `n/a` significa que no hay datos suficientes o que ese campo no aplica al símbolo/setup.
- Las señales son candidatos técnicos, no recomendaciones de compra/venta.
- Los providers gratuitos pueden no cubrir todos los tickers locales o CEDEAR-related.
- El score ayuda a ordenar, pero no reemplaza análisis de riesgo, liquidez ni contexto macro.

## 8. Cómo lo explicaría en un demo

- “Esta pantalla monitorea una watchlist seleccionada, no todo el mercado.”
- “El frontend refresca solo datos persistidos; no dispara IA ni operaciones caras automáticamente.”
- “Cada ticker puede estar con setup activo, monitoreando, pendiente o sin datos.”
- “El precio visible ayuda a validar frescura y escala del activo.”
- “La tabla combina score, momentum, volumen relativo, medias móviles y riesgo.”
- “Top upside ordena candidatos por potencial entre entry y take profit.”
- “El briefing de IA es manual y configurable para controlar costos.”
- “Esto no toma decisiones por el usuario; prioriza señales técnicas para revisión.”

## Disclaimer

MarketAgent es una herramienta de análisis técnico y monitoreo. No constituye recomendación financiera.
