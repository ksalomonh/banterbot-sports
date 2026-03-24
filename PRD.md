# PRD: BanterBot Sports

## Contexto e Intent

BanterBot Sports es la evolución de un sistema de quinielas de fútbol usado por un grupo de amigos desde la **Eurocopa 2016** (Excel) y el **Mundial Rusia 2018** (app .NET Core 2.0). El objetivo es modernizar y extender esa app a **.NET 10 LTS** con todas las reglas reales del juego ya validadas en producción, incorporando:

1. Integración con API externa para obtener partidos y resultados en tiempo real.
2. Un bot de Telegram con IA que permite a los jugadores recibir y enviar predicciones desde su teléfono, por texto o por audio.

El punto de partida es el repositorio legado: [Adriansillo/Quinielas](https://github.com/Adriansillo/Quinielas) (.NET Core 2.0 + PostgreSQL).

---

## Estructura de la Quiniela

La quiniela es un **torneo de largo aliento**, similar a una liga de fútbol:

- El **organizador** crea el torneo, define la cantidad de jornadas y **selecciona qué partidos (obtenidos de la API) corresponden a cada jornada**. No crea los partidos manualmente.
- Los partidos de **fase de grupos** están disponibles desde el inicio del torneo.
- Los partidos de **fase final** (octavos, cuartos, semis, final) se cargan progresivamente conforme se conocen los equipos clasificados — el organizador los asigna a la jornada correspondiente cuando ya tienen contendientes definidos.
- Los **jugadores** participan durante toda la duración del torneo.
- Hay una **tabla general** que acumula puntos de todas las jornadas.
- Al final del torneo, los lugares del podio reciben su porcentaje del prize pool.

---

## Sistema de Puntos (configurable por el organizador)

| Concepto | Puntos por defecto | Descripción |
|---|---|---|
| Resultado correcto | 1 pt | Acertar ganador o empate (sin importar el marcador exacto) |
| Marcador exacto | 1 pt | Acertar los goles exactos de ambos equipos |
| Goles de la jornada | 3 pts | El jugador pronostica la suma total de goles de los partidos de la jornada. Si coincide con la realidad, gana los puntos |

> Los valores de puntos por cada concepto son **configurables por el organizador**.

**Nota sobre goles de jornada**: el jugador puede ingresar una suma personalizada o dejar que la app sume automáticamente sus predicciones de goles. Si la suma de goles oficiales coincide con su pronóstico, gana los puntos.

---

## Premio y Distribución (configurable)

- Cada jugador aporta un **monto fijo de inscripción**.
- El organizador configura **cuántos lugares reciben premio** y **qué porcentaje** corresponde a cada uno.
- Ejemplo Rusia 2018: 3 ganadores → 1° 70%, 2° 20%, 3° 10%.
- En caso de **empate entre posiciones**, el premio de esos lugares se divide en partes iguales.

---

## Deadlines y Bloqueo de Predicciones

- Los jugadores tienen hasta el **inicio del primer partido de la jornada** para ingresar sus predicciones (vía web o Telegram).
- Una vez iniciado el primer partido, los campos de predicción se **bloquean para los jugadores**.
- **Solo el organizador** puede ingresar o modificar marcadores una vez bloqueada la jornada.
- El organizador **puede ser también un jugador**.

---

## Flujo de Predicciones vía Telegram

El canal principal de interacción de los **jugadores** con el sistema es Telegram:

```
1. Apertura de jornada
      ↓
2. Bot envía mensaje a cada jugador con la lista de partidos a predecir
      ↓
3. El jugador responde:
     - Texto: "Argentina 2-0 Brasil, Francia 1-1 Alemania, ..."
     - Audio: mensaje de voz con los mismos datos
      ↓
4. Si es audio → transcripción automática (Whisper API)
      ↓
5. Claude API analiza el texto y extrae las predicciones
      ↓
6. El bot confirma las predicciones al jugador y las sube al sistema
      ↓
7. El jugador puede corregir hasta el deadline (primer kick-off de la jornada)
```

**Vinculación de cuenta Telegram:**
- Al registrarse o en su perfil, el jugador vincula su cuenta de Telegram.
- El flujo de vinculación: el usuario hace `/start` en el bot → el bot asocia su `telegram_user_id` con su cuenta en el sistema.
- Sin vinculación, el jugador puede ingresar predicciones solo vía web.

---

## Scope

### In Scope (MVP)
- Registro/login de usuarios (migrar el sistema de auth existente)
- Creación de torneos: nombre, jornadas, monto de inscripción, puntos configurables, distribución de premios configurable
- **Integración API-Football**: búsqueda y selección de partidos por competición y fecha
- **Asignación de partidos a jornadas** por el organizador (desde catálogo de la API)
- **Carga progresiva de partidos de fase final** cuando ya tienen equipos definidos
- **Sincronización automática de resultados** desde API-Football al finalizar cada partido
- Invitación de participantes vía link
- **Bot de Telegram** para envío de lista de partidos a predecir por jornada
- **Ingreso de predicciones por Telegram**: texto libre y mensajes de voz
- **Transcripción de audio** (Whisper API) + extracción de predicciones (Claude API)
- Confirmación de predicciones por el bot + posibilidad de corrección hasta el deadline
- **Vinculación de cuenta Telegram** con usuario del sistema
- Ingreso de predicciones vía web (alternativa a Telegram)
- Pronóstico de goles totales por jornada
- Bloqueo automático de predicciones al inicio del primer partido de la jornada
- Cálculo automático de puntos (resultado, marcador exacto, goles de jornada)
- Tabla general acumulada del torneo
- Tabla de posiciones por jornada
- Gestión del prize pool (quién pagó, cuánto, ganadores)
- El organizador puede participar como jugador
- **IA Banter Engine vía Telegram**: mensajes personalizados por jugador al cerrar cada jornada (máx. 280 caracteres por mensaje)
- Historial de torneos

### Out of Scope (MVP)
- Integración de pagos reales (el dinero se gestiona fuera de la app)
- App móvil nativa (web responsiva + Telegram)
- Chat entre jugadores
- Autenticación social (Google, Facebook)
- Otros canales de mensajería (WhatsApp, Discord) — post-MVP
- Predicciones grupales o en equipo

---

## Stack

| Capa | Tecnología | Justificación |
|---|---|---|
| Framework | .NET 10 LTS | Migración desde .NET Core 2.0 |
| Web | ASP.NET Core MVC / Razor Pages | Misma arquitectura del legado |
| ORM | Entity Framework Core 10 | Ya usado en legado con EF Core 2.0 |
| Base de datos | PostgreSQL | Ya usado en legado (Npgsql). Gratuito. |
| Auth | ASP.NET Core Identity | Ya integrado en legado |
| Real-time | SignalR | Actualizaciones de tabla en vivo |
| Bot de Telegram | Telegram.Bot (NuGet) | SDK oficial .NET para Telegram Bot API |
| Transcripción de voz | OpenAI Whisper API | Convierte audio OGG de Telegram a texto |
| IA: extracción predicciones + banter | Claude API (claude-haiku-4-5-20251001) | Parsea texto libre → predicciones estructuradas; genera banter |
| Datos de partidos y resultados | API-Football | Catálogo de partidos, resultados en tiempo real |

---

## Arquitectura del Proyecto

```
BanterBotSports/
├── BanterBotSports.Web/              # ASP.NET Core — controllers, views, wwwroot, SignalR hubs
├── BanterBotSports.BL/               # Business Logic — puntos, premios, deadlines
├── BanterBotSports.DAL/              # EF Core DbContext, Repositories, Migrations
├── BanterBotSports.Entities/         # Domain entities, ViewModels, DTOs
├── BanterBotSports.BanterAI/         # Claude API — banter engine + extracción de predicciones
├── BanterBotSports.Integrations/     # API-Football client, Telegram bot, Whisper transcription
└── BanterBotSports.sln
```

---

## Entidades Principales

- `Torneo` — nombre, configuración de puntos, configuración de premios, monto inscripción, organizador
- `Jornada` — número, fecha límite de predicciones, estado (abierta / cerrada / finalizada)
- `Partido` — externalId (API-Football), equipo1, equipo2, fecha/hora kick-off UTC, goles oficiales, estado
- `Participante` — relación usuario ↔ torneo, rol (organizador / jugador / ambos), estado de pago
- `UsuarioTelegram` — telegramUserId, telegramUsername, fechaVinculacion, userId (FK)
- `PrediccionPartido` — golesEquipo1, golesEquipo2, puntos obtenidos, fuente (web / telegram)
- `PrediccionJornada` — goles totales pronosticados, puntos obtenidos

---

## Risks

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Breaking changes .NET Core 2.0 → .NET 10 | Alta | Migración incremental, smoke tests en cada upgrade mayor |
| Transcripción de voz con acentos/nombres de equipos incorrectos | Alta | Claude API valida y corrige el texto transcrito antes de parsear. Confirmación explícita al jugador. |
| Predicción en texto libre mal parseada por Claude | Media | Prompt estructurado con ejemplos. Si no puede parsear, el bot pide reformular. |
| Partidos de fase final sin equipos definidos | Media | Jornada queda en estado "pendiente de partidos". El organizador la activa cuando asigna los partidos. |
| Rate limits de API-Football | Media | Caché de resultados en PostgreSQL. Polling solo para partidos activos. |
| Entrega de mensajes Telegram fallida | Baja | Retry con backoff exponencial. Fallback a web. |
| Lógica de empates en premios compleja | Media | Tests unitarios exhaustivos para todos los escenarios de empate |
| Regulación legal de pools de dinero | Media | La app no procesa pagos. Gestión fuera del sistema. |

---

## Success Criteria

- [ ] Migración completa a .NET 10 sin pérdida de funcionalidad existente
- [ ] El organizador puede buscar y asignar partidos de una jornada desde la API en < 3 minutos
- [ ] Los partidos de fase final aparecen disponibles para asignar una vez conocidos los equipos
- [ ] Los resultados se sincronizan automáticamente desde API-Football al finalizar cada partido
- [ ] Un jugador puede enviar sus predicciones completas de una jornada por Telegram (texto o voz)
- [ ] La IA extrae correctamente las predicciones del mensaje con > 95% de precisión
- [ ] El bloqueo de predicciones ocurre automáticamente al iniciar el primer partido de la jornada
- [ ] Los puntos se calculan correctamente para los 3 conceptos
- [ ] La tabla general refleja puntos acumulados en tiempo real
- [ ] Cada jugador recibe al menos 1 mensaje de banter personalizado por Telegram al cerrar cada jornada
- [ ] La distribución de premios con empates se calcula correctamente

---

## Rollback Plan

- El repositorio legado (`quinielas-legacy/`) se mantiene intacto como referencia.
- PostgreSQL schema se versiona con EF Core Migrations — reversible.
- El bot de Telegram y el banter engine son módulos aislados — desactivarlos no rompe el core.
- API-Football tiene fallback: el organizador puede ingresar resultados manualmente si la API falla.
