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

## 2. Botones y acciones

| Botón / control | Dónde está | Qué hace | Endpoint principal | Efecto esperado |
|---|---|---|---|---|
| Generate Signals | Barra superior | Genera señales técnicas desde snapshots e histórico. | `POST /api/signals/run` | Crea/actualiza señales persistidas y luego recarga el panel. |
| Generate AI Briefing | Barra superior | Genera briefing con IA a partir de snapshots/señales. | `POST /api/briefing/run` | Usa Azure OpenAI si está habilitado. Puede tener costo. |
| Actualizar precios | Barra superior | Fuerza actualización de precios para la watchlist del usuario. | `POST /api/watchlist/hydrate` con `force: true` | Actualiza snapshots y señales para los símbolos seleccionados. |
| Actualizar panel | Barra superior | Refresca datos ya persistidos. | Varios `GET` | No ingiere precios, no genera señales, no llama IA. |
| Actualizar watchlist | Panel Mi watchlist | Hidrata la watchlist sin forzar si el dato ya es fresco. | `POST /api/watchlist/hydrate` con `force: false` | Evita llamadas innecesarias. |
| Agregar ticker | Panel Mi watchlist | Agrega un símbolo elegible a la watchlist local. | Ninguno | Guarda en `localStorage`. No actualiza precios automáticamente. |
| Quitar ticker | Chip de cada ticker | Remueve un símbolo de la watchlist local. | Ninguno | Guarda en `localStorage`. No borra datos del backend. |
| Filtros de señales | Panel Todas las señales | Filtra por score, RS, RVOL, setup, riesgo, oportunidad, etc. | Ninguno | Solo afecta la vista actual. |
| Limpiar filtros | Estado vacío / filtros | Restablece filtros por defecto. | Ninguno | Vuelve a mostrar señales según watchlist. |
| Expandir/colapsar sección | Header de cada sección | Oculta o muestra una sección. | Ninguno | Se guarda en `localStorage`. |
| Seleccionar símbolo | Tablas/cards/listas | Cambia el detalle visible. | Ninguno | Solo navegación interna del dashboard. |

## 3. Cómo se deshabilita cada botón

### Generate AI Briefing

Este botón ya se deshabilita por configuración.

Condición en frontend:

```ts
const aiBriefingDisabled = systemStatus?.azureOpenAIEnabled !== true;
```

Configuración backend:

```json
"AzureOpenAI": {
  "Enabled": false
}
```

En Azure App Service:

```txt
AzureOpenAI__Enabled=false
```

Con `false`, el botón queda visible pero deshabilitado.

### Generate Signals

Actualmente no tiene feature flag. Siempre aparece, salvo estado de carga.

Para deshabilitarlo en UI:

```tsx
<ActionButton
  ...
  disabled={true}
/>
```

Para ocultarlo:

```tsx
{signalsEnabled ? (
  <ActionButton ... />
) : null}
```

Recomendación si se quiere hacerlo configurable: agregar una variable frontend tipo `VITE_ENABLE_MANUAL_SIGNALS=false` y usarla para renderizar/deshabilitar el botón.

### Actualizar precios

Actualmente siempre aparece y se deshabilita solo mientras hay otra acción manual corriendo.

Hace hidratación forzada:

```ts
hydrateWatchlist({ symbols: userWatchlistSymbols, force: true })
```

Para deshabilitarlo en UI:

```tsx
<ActionButton
  label="Actualizar precios"
  disabled={true}
  ...
/>
```

Para ocultarlo, envolverlo con un flag:

```tsx
{priceRefreshEnabled ? (
  <ActionButton label="Actualizar precios" ... />
) : null}
```

### Actualizar panel

Actualmente siempre aparece. Es una acción barata: solo lee datos persistidos.

Para deshabilitarlo:

```tsx
<ActionButton
  label="Actualizar panel"
  disabled={true}
  ...
/>
```

### Actualizar watchlist

Se deshabilita automáticamente cuando:

- Ya está hidratando.
- La watchlist está vacía.

Código:

```tsx
disabled={hydrating || symbols.length === 0}
```

### Agregar ticker

Se deshabilita automáticamente cuando:

- La watchlist llegó al máximo.
- No hay símbolo seleccionado.
- No quedan símbolos disponibles.

El máximo está en:

```ts
MarketAgent.Web/src/watchlistMetadata.ts
export const maxUserWatchlistSymbols = 10;
```

### Quitar ticker

Actualmente siempre está habilitado para cada símbolo visible.

Para bloquearlo, habría que agregar una prop al componente `MyWatchlistPanel`, por ejemplo `canRemove={false}`, y usarla en el botón de remover.

## 4. Diferencia entre acciones manuales y scheduler

| Mecanismo | Corre solo | Qué hace | Control |
|---|---:|---|---|
| Auto-refresh frontend | Sí, cada 60s | Lee datos persistidos. | `AUTO_REFRESH_INTERVAL_MS` en `App.tsx`. |
| Actualizar panel | No | Lee datos persistidos manualmente. | Botón UI. |
| Actualizar precios | No | Fuerza hidratación de watchlist. | Botón UI. |
| Actualizar watchlist | No | Hidrata si el dato no está fresco. | Botón UI. |
| Generate Signals | No | Genera señales manualmente. | Botón UI. |
| Generate AI Briefing | No | Genera briefing IA manualmente. | `AzureOpenAI:Enabled`. |
| Scheduler backend | Sí, si está habilitado | Corre ciclo completo programado. | `MarketAgentScheduler`. |

Configuración del scheduler:

```json
"MarketAgentScheduler": {
  "Enabled": false,
  "IntervalMinutes": 5,
  "RunEmailDelivery": false,
  "MarketHoursOnly": true,
  "RunOnStartup": false
}
```

En Azure App Service:

```txt
MarketAgentScheduler__Enabled=true
MarketAgentScheduler__IntervalMinutes=5
MarketAgentScheduler__MarketHoursOnly=true
MarketAgentScheduler__RunOnStartup=true
```

Importante: el scheduler no habilita/deshabilita botones del frontend. Solo controla procesos automáticos del backend.

## 5. Badges de estado

| Badge | Qué significa | Cómo interpretarlo |
|---|---|---|
| API connected / API offline | Indica si el frontend puede comunicarse con el backend. | Si está offline, el dashboard puede mostrar vista previa o datos incompletos. |
| Auto-refresh activo | El frontend refresca datos existentes cada 60 segundos. | No genera señales ni llama IA; solo lee endpoints GET. |
| Mercado abierto/cerrado | Estado aproximado de mercado USA regular. | Cerrado no implica error: puede mostrar el último snapshot válido. |
| Vista previa | Se está mostrando fallback/mock porque la API no respondió. | Útil para layout, no para análisis real. |
| Última actualización | Última vez que el dashboard refrescó datos. | Sirve para validar que la UI está viva. |
| Precios actualizados | Última vez que se forzó hidratación de precios desde la UI. | Confirma que el botón Actualizar precios terminó. |
| Snapshot de mercado | Timestamp del dato de mercado más reciente disponible. | Ayuda a validar frescura de datos. |
| Últimas señales | Última vez que se generaron señales manualmente desde la UI. | No necesariamente coincide con el último precio. |
| Scheduler | Última ejecución del scheduler backend. | Solo aparece si el backend reporta `lastCycleRunUtc`. |

## 6. Campos de la tabla de señales

| Campo | Significado | Cálculo / aproximación | Interpretación rápida |
|---|---|---|---|
| Precio | Precio actual o último precio persistido. | Prioriza el último snapshot; si falta, usa precio asociado a la señal. | Valida frescura y escala del activo. |
| Símbolo | Ticker del activo. | Watchlist filtrada del usuario. | Click para ver detalle. |
| Tendencia | Sparkline de cierres recientes. | Cierres históricos compactos. | Lectura visual rápida del movimiento. |
| Score | Puntuación técnica calibrada. | Reglas de momentum, tendencia, riesgo, RS, RVOL, etc. | Más alto = mejor setup técnico, no recomendación. |
| Setup | Tipo de patrón detectado. | Reglas del analizador técnico. | Ej: `MomentumContinuation`, `Pullback`, `Risk`. |
| Señal | Marcador visual de evento técnico. | Ej: ORR, opening red reversal. | Ayuda a detectar eventos sin leer todo el detalle. |
| Acción | Acción sugerida por el motor. | Derivada del score/setup/riesgo. | `Candidate`, `Watch`, `Avoid`. |
| Confianza | Nivel cualitativo. | Derivado de score, setup y confirmaciones. | Alta/Media/Baja no es probabilidad exacta. |
| Timeframe | Contexto temporal. | Puede ser intraday, swing o persistido. | `Persisted` indica dato guardado. |
| RS | Relative Strength vs SPY. | Rendimiento del símbolo contra SPY. | Positivo = outperform. |
| RVOL | Volumen relativo. | `volumen actual / promedio volumen 20d`. | < 1 = volumen débil; > 1 = mayor participación. |
| EXT | Extensión contra EMA20. | Distancia porcentual desde EMA20. | EXT alto puede indicar entrada tardía. |
| RSI14 | Oscilador de momentum. | RSI estándar de 14 períodos. | < 30 sobreventa; > 70 sobrecompra. |
| EMA9 | Media exponencial corta. | 9 períodos. | Momentum corto plazo. |
| EMA20 | Media exponencial intermedia. | 20 períodos. | Pullbacks/tendencia intermedia. |
| EMA50 | Media exponencial lenta. | 50 períodos. | Tendencia amplia. |
| ATR14 | Rango promedio. | Volatilidad de 14 períodos. | Útil para stops y sizing. |

Nota sobre RVOL: si la señal persistida no trae `relativeVolume`, el frontend intenta calcularlo usando el volumen del último snapshot y el promedio de volumen histórico de 20 días.

## 7. Mi watchlist

`Mi watchlist` es la lista local de activos que el usuario quiere monitorear. Se guarda en `localStorage`, no requiere login y no modifica la base de datos.

Importante: estar en la watchlist no significa tener señal activa.

| Estado | Qué significa |
|---|---|
| Setup activo | Hay una señal/setup persistido para ese símbolo. |
| Monitoreando | Hay precio/snapshot reciente, pero no setup activo. |
| Pendiente de actualizar | El ticker fue agregado, pero todavía no se hidrató. |
| Sin datos | Se intentó hidratar, pero el provider no devolvió datos. |
| Error | Falló algo inesperado en la hidratación. |

El badge `CEDEAR` marca activos que tienen o suelen tener representación CEDEAR para el público argentino. Algunos tickers pueden ser seleccionables aunque no tengan provider disponible; en ese caso pueden aparecer como `Sin datos`.

## 8. Dónde editar tickers elegibles

Frontend:

```txt
MarketAgent.Web/src/watchlistMetadata.ts
```

Ahí se controla:

- Universo elegible del selector.
- Tickers populares.
- Watchlist default.
- Máximo de símbolos.

Backend watchlist global:

```txt
src/MarketAgent.Infrastructure/Watchlists/StaticWatchlistProvider.cs
```

Providers de datos:

```txt
src/MarketAgent.Infrastructure/MarketData/EquityMarketDataProvider.cs
src/MarketAgent.Infrastructure/MarketData/HistoricalMarketDataProvider.cs
```

Hidratación manual:

```txt
src/MarketAgent.Application/Watchlists/WatchlistHydrationService.cs
```

Para que un ticker funcione completo, debe existir en:

- Universo frontend, si querés seleccionarlo.
- Provider actual, si querés precio.
- Provider histórico, si querés indicadores, sparkline, RVOL, EMAs.
- Watchlist backend, si querés que entre en ingesta/scheduler global.

## 9. Endpoints útiles para diagnóstico

| Endpoint | Uso |
|---|---|
| `GET /api/system/status` | Estado general, AI, scheduler, mercado. |
| `GET /api/system/scheduler-status` | Diagnóstico del scheduler backend. |
| `GET /api/ingestion/snapshots` | Últimos snapshots por símbolo. |
| `POST /api/ingestion/run` | Ingesta global backend. |
| `POST /api/watchlist/hydrate` | Hidratación específica de watchlist. |
| `POST /api/signals/run` | Generación manual de señales. |
| `GET /api/signals/latest` | Última corrida persistida de señales. |
| `GET /api/signals/outcomes` | Señales/outcomes persistidos usados por el dashboard. |
| `GET /api/historical/candles` | Velas históricas usadas por sparklines e indicadores. |

## 10. Caveats importantes

- Mercado cerrado o feriados pueden mostrar datos viejos pero válidos.
- Providers gratuitos pueden tener delay, cache o cobertura parcial.
- `n/a` significa que no hay datos suficientes o que ese campo no aplica.
- Las señales son candidatos técnicos, no recomendaciones de compra/venta.
- El score ayuda a ordenar, pero no reemplaza análisis de riesgo, liquidez ni contexto macro.
- TradingView no debe usarse como fuente scrapeada de datos para backend; para precios realmente live conviene usar un proveedor licenciado.

## 11. Cómo lo explicaría en un demo

- "Esta pantalla monitorea una watchlist seleccionada, no todo el mercado."
- "El frontend refresca datos persistidos; no dispara IA automáticamente."
- "Actualizar precios fuerza una hidratación de los tickers seleccionados."
- "Actualizar panel solo lee lo que ya existe."
- "Cada ticker puede estar con setup activo, monitoreando, pendiente o sin datos."
- "El precio visible ayuda a validar frescura y escala del activo."
- "RVOL compara volumen actual contra el promedio de 20 días."
- "Top upside ordena candidatos por potencial entre entry y take profit."
- "El briefing de IA es manual y configurable para controlar costos."
- "Esto no toma decisiones por el usuario; prioriza señales técnicas para revisión."

## Disclaimer

MarketAgent es una herramienta de análisis técnico y monitoreo. No constituye recomendación financiera.
