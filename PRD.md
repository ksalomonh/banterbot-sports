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
- Creación de torneos vía **wizard de 5 pasos**: Basics → Scoring → Prizes → Matches → Review (ver sección abajo)
- **Integración API-Football**: búsqueda y selección de partidos por competición y fecha
- **Asignación de partidos a jornadas** por el organizador (desde catálogo de la API)
- **Carga progresiva de partidos de fase final** cuando ya tienen equipos definidos
- **Sincronización automática de resultados** desde API-Football al finalizar cada partido
- Invitación de participantes vía link
- **Bot de Telegram** para envío de lista de partidos a predecir por jornada
- **Ingreso de predicciones por Telegram**: texto libre y mensajes de voz
- **Transcripción de audio** (Whisper API) + extracción de predicciones (Claude API)
- Confirmación de predicciones por el bot + posibilidad de corrección hasta el deadline
- **Recordatorios de partidos vía Telegram**: notificación automática 15 minutos antes del kick-off (configurable por el jugador en su perfil)
- **Vinculación de cuenta Telegram** con usuario del sistema
- Ingreso de predicciones vía web (alternativa a Telegram)
- Pronóstico de goles totales por jornada
- Bloqueo automático de predicciones al inicio del primer partido de la jornada
- Cálculo automático de puntos (resultado, marcador exacto, goles de jornada)
- **Multiplicador de puntos por jornada**: configurable por el organizador desde la consola (valor por defecto: 1x)
- Tabla general acumulada del torneo
- Tabla de posiciones completa por torneo (pantalla dedicada con ranking expandido)
- Tabla de posiciones por jornada
- **Resumen post-jornada**: vista del jugador con resultados reales, puntos ganados, variación en ranking y banter recap
- Gestión del prize pool (quién pagó, cuánto, ganadores)
- **Pantalla de cierre de torneo**: posiciones finales y distribución de premios calculada
- El organizador puede participar como jugador
- **IA Banter Engine vía Telegram**: mensajes personalizados por jugador al cerrar cada jornada (máx. 280 caracteres por mensaje)
- **Banter Rail**: feed en tiempo real visible en las pantallas principales (dashboard, vista de torneo, predicciones, consola del organizador). Componente glassmórfico asimétrico en el lado derecho. No es un chat entre jugadores — es el BanterBot comentando en vivo.
- Historial de torneos (activos e historial de torneos completados)

### Out of Scope (MVP)
- Integración de pagos reales (el dinero se gestiona fuera de la app). El perfil no tendrá sección "Wallet".
- App móvil nativa (web responsiva + Telegram)
- Chat directo entre jugadores (el Banter Rail es solo BanterBot, no mensajería peer-to-peer)
- Autenticación social (Google, Facebook, Discord) — solo ASP.NET Core Identity
- Otros canales de mensajería (WhatsApp, Discord) — post-MVP
- Predicciones grupales o en equipo
- Sistema de logros/trofeos (Achievements/Trophies) — post-MVP
- Ranking global cross-torneo (Career Rank) — post-MVP. El dashboard MVP muestra únicamente la posición del jugador dentro de cada torneo activo.

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

## Navegación Principal

La app tiene una navegación global con los siguientes ítems:

| Ítem | Acceso | Descripción |
|------|--------|-------------|
| My Tournaments | Todos | Dashboard con torneos activos, deadlines y accesos rápidos |
| Create Tournament | Todos | Inicia el wizard de creación |
| Bot Settings | Todos | Preferencias del bot de Telegram (notificaciones, audio, recordatorios) |
| Profile | Todos | Perfil del jugador con stats, vinculación Telegram y gestión de cuenta |

En pantallas específicas (vista de torneo, consola del organizador) aparecen ítems adicionales contextuales como Leaderboards y Stats.

---

## Wizard de Creación de Torneo

La creación de un torneo es un flujo lineal de **5 pasos** con un sidebar de progreso y un "Save Draft" persistente en cada paso:

| Paso | Nombre | Contenido |
|------|--------|-----------|
| 1 | **Basics** | Nombre del torneo, **cantidad de jornadas** (stepper `-`/`+`), monto de inscripción (USD), descripción, imagen de portada |
| 2 | **Scoring** | Puntos por resultado correcto (W/D/L), puntos bonus por marcador exacto, puntos por goles totales de jornada |
| 3 | **Prizes** | Cantidad de lugares premiados, porcentaje por lugar, validación de que el total = 100% |
| 4 | **Matches** | Selección de partidos para la primera jornada desde el catálogo de API-Football (búsqueda por competición) |
| 5 | **Review** | Resumen completo del torneo antes de publicar. Botón "Publish Live". |

### Campo "Cantidad de Jornadas" — Especificación de UX

El campo se implementa como un **stepper touch-friendly** (`-` / número / `+`), optimizado para mobile:
- Rango válido: 1 – 32 jornadas
- Valor por defecto: 8
- El campo ocupa una fila completa entre "Entry Fee" y "Description"
- En desktop: los botones `-`/`+` están a los costados del número
- En mobile: los botones tienen mínimo 44px de área táctil

> **Mockup pendiente**: los mockups `create_tournament_basics` (desktop y mobile) deben actualizarse para incluir este campo con el stepper.

---

## Banter Rail

El Banter Rail es un componente exclusivo de la identidad visual de BanterBot Sports:

- **Posición**: panel vertical glassmórfico en el lado derecho de las pantallas principales
- **Contenido**: mensajes del BanterBot comentando predicciones, resultados y movimientos en el ranking en tiempo real. NO es un chat entre jugadores.
- **Comportamiento**: se actualiza en tiempo real vía SignalR. Los jugadores pueden ver el rail pero no escribir directamente — la única interacción es el botón "Join Conversation" que abre el bot de Telegram.
- **Pantallas donde aparece**: Dashboard, Tournament Overview, Matchday Predictions, Organizer Console, Create Tournament wizard.
- **Límite de mensajes**: 280 caracteres por mensaje (misma regla que el banter de Telegram).

---

## Decisiones de Diseño — Resueltas

| # | Decisión | Resolución |
|---|----------|------------|
| 1 | Discord social login | **OUT OF SCOPE**. Solo ASP.NET Core Identity. Los mockups de Login y Register muestran Discord pero esa opción no se implementa. Los mockups deben actualizarse para eliminar los botones de Discord. |
| 2 | Wallet en el perfil | **OUT OF SCOPE**. No hay procesamiento de pagos. El ítem "Wallet" del sidebar en `user_profile_bot_settings` se ignora — el mockup tiene una nota al respecto. En el diseño final se elimina o reemplaza por "Prize History" (historial de premios ganados). |
| 3 | Global Career Ranking | **POST-MVP**. El dashboard MVP muestra únicamente la posición del jugador dentro de cada torneo activo. El "Career Rank #432" visible en el mockup del dashboard no se implementa en MVP. |
| 4 | Jornada count en wizard | **RESUELTO**. Se agrega como stepper `-`/`+` en el Basics step. Ver especificación en la sección "Wizard de Creación de Torneo" arriba. Los mockups de ese paso (desktop y mobile) se actualizan. |

---

## Mockups Existentes

| Mockup | Pantalla |
|--------|----------|
| `login_banterbot_sports` | Login |
| `register_banterbot_sports` | Registro |
| `dashboard_my_tournaments` | Dashboard principal |
| `tournament_overview` | Vista general del torneo |
| `matchday_predictions` | Predicciones de jornada (jugador) |
| `organizer_console` | Consola del organizador |
| `create_tournament_basics` | Wizard paso 1: Datos básicos |
| `create_tournament_scoring` | Wizard paso 2: Configuración de puntos |
| `create_tournament_prizes` | Wizard paso 3: Distribución de premios |
| `create_tournament_match_selection` | Wizard paso 4: Selección de partidos |
| `user_profile_bot_settings` | Perfil + configuración del bot |

## Mockups Pendientes de Creación

Las siguientes pantallas están especificadas en el PRD pero no tienen mockup:

| # | Pantalla | Descripción |
|---|----------|-------------|
| 1 | **Leaderboard completo** | Tabla de posiciones expandida con paginación, filtros por jornada y posición del jugador resaltada. Accesible desde "VIEW FULL LEADERBOARD" en Tournament Overview. |
| 2 | **Join Tournament via invite link** | Pantalla que ve un jugador al seguir un link de invitación: preview del torneo (nombre, organizador, prize pool, jornadas), botón de confirmación de inscripción. |
| 3 | **Create Tournament — Step 5: Review & Publish** | Resumen completo del torneo configurado (nombre, scoring, premios, partidos de jornada 1) antes de publicar. Botón "Publish Live". |
| 4 | **Organizer: Asignar partidos a jornada existente** | Flujo para que el organizador agregue partidos de fase final a una jornada ya creada, una vez que los equipos clasificados están definidos. |
| 5 | **Resumen post-jornada (jugador)** | Vista que aparece al jugador cuando cierra una jornada: resultados reales vs predicciones, puntos ganados por categoría, variación en el ranking, y mensajes de banter personalizados. |
| 6 | **Cierre de torneo / Distribución final de premios** | Pantalla de fin de torneo: posiciones finales, cálculo del prize pool distribuido por lugar, indicador de empates resueltos. |
| 7 | **Historial de torneos terminados** | Sección del dashboard o pantalla separada con torneos completados: nombre, fecha, posición final del jugador, premio ganado. |
| 8 | **Forgot Password** | Flujo de recuperación de contraseña (referenciado con link en el mockup de Login). |
| 9 | **Create Tournament Basics — actualización** | Agregar campo "Jornadas" con stepper `-`/`+` entre Entry Fee y Description. Aplica a desktop y mobile. |
| 10 | **Login / Register — actualización** | Eliminar los botones de Discord (out of scope). Aplica a desktop y mobile. |

---

## Rollback Plan

- El repositorio legado (`quinielas-legacy/`) se mantiene intacto como referencia.
- PostgreSQL schema se versiona con EF Core Migrations — reversible.
- El bot de Telegram y el banter engine son módulos aislados — desactivarlos no rompe el core.
- API-Football tiene fallback: el organizador puede ingresar resultados manualmente si la API falla.
