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
- **Banter Rail**: feed de anuncios en tiempo real del BanterBot — solo lectura. Visible en las pantallas principales (dashboard, vista de torneo, predicciones, consola del organizador). Componente glassmórfico asimétrico en el lado derecho. No recibe input del jugador.
- **Arena Chat**: chat interactivo peer-to-peer en tiempo real vía SignalR donde los jugadores pueden hablar, bromear e interactuar. El BanterBot participa activamente: comenta los resultados de los jugadores con humor (sin groserías), provoca a los que tuvieron mala jornada, celebra jugadas brillantes o rachas de suerte inusual. En mobile es flotante (FAB) y se expande al tocar.
- **Join Tournament via invite link con registro inline**: un usuario no registrado que recibe un invite link puede crear su cuenta directamente en esa pantalla. Al completar el registro, la cuenta queda automáticamente ligada al torneo sin pasos adicionales.
- Historial de torneos (activos e historial de torneos completados)

### Out of Scope (MVP)
- Integración de pagos reales (el dinero se gestiona fuera de la app). El perfil no tendrá sección "Wallet".
- App móvil nativa (web responsiva + Telegram)
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

### Desktop (barra superior)

| Ítem | Acceso | Descripción |
|------|--------|-------------|
| My Tournaments | Todos | Dashboard con torneos activos, deadlines y accesos rápidos |
| Create Tournament | Todos | Inicia el wizard de creación |
| Bot Settings | Todos | Preferencias del bot de Telegram (notificaciones, audio, recordatorios) |
| Profile | Todos | Perfil del jugador con stats, vinculación Telegram y gestión de cuenta |

En pantallas específicas (vista de torneo, consola del organizador) aparecen ítems adicionales contextuales como Leaderboards y Stats.

### Mobile (barra inferior fija)

En mobile la navegación principal migra a una **bottom nav bar** persistente con 4 ítems:

| Ícono | Destino |
|-------|---------|
| Home / Arena | Dashboard principal |
| Leagues | Lista de torneos activos |
| Predict | Predicciones de la jornada activa |
| Profile | Perfil del jugador |

---

## Wizard de Creación de Torneo

La creación de un torneo es un flujo lineal de **5 pasos** con un sidebar de progreso y un "Save Draft" persistente en cada paso:

| Paso | Nombre | Contenido |
|------|--------|-----------|
| 1 | **Basics** | Nombre del torneo, **cantidad de jornadas** (stepper `-`/`+`), monto de inscripción (USD), **máximo de jugadores** (opcional), descripción, imagen de portada |
| 2 | **Scoring** | Puntos por resultado correcto (W/D/L), puntos bonus por marcador exacto, puntos por goles totales de jornada |
| 3 | **Prizes** | Cantidad de lugares premiados, porcentaje por lugar, validación de que el total = 100% |
| 4 | **Matches** | Selección de partidos para la primera jornada desde el catálogo de API-Football (búsqueda por competición) |
| 5 | **Review** | Resumen completo del torneo antes de publicar. Botón "Publish Live". |

El wizard tiene **5 pasos tanto en desktop como en mobile**. La versión mobile del paso 5 (Review) no tiene mockup dedicado — se implementa como versión responsiva del desktop.

> **Nota sobre los mockups mobile**: `create_scoring_mobile` y `create_prizes_mobile` muestran "2/4" y "3/4" respectivamente. Eso es un error en los mockups — el total correcto es 5. Los mockups tienen una nota al respecto.

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

El Banter Rail es el feed de anuncios del BanterBot — **solo lectura**:

- **Posición desktop**: panel vertical glassmórfico en el lado derecho de las pantallas principales.
- **Posición mobile**: sección inline al final del contenido principal (scroll vertical).
- **Contenido**: anuncios automáticos del BanterBot sobre eventos del torneo — goles anotados, cambios de posición en el ranking, predicciones que se cumplieron, deadlines próximos. Son mensajes de broadcasting, no conversación.
- **Comportamiento**: se actualiza en tiempo real vía SignalR. Los jugadores **no pueden escribir** en el Banter Rail.
- **Pantallas donde aparece**: Dashboard, Tournament Overview, Matchday Predictions, Organizer Console, Create Tournament wizard.
- **Límite de mensajes**: 280 caracteres por mensaje.

---

## Arena Chat

El Arena Chat es el componente de interacción social en tiempo real. **Distinto al Banter Rail.**

- **Participantes**: todos los jugadores del torneo + el BanterBot como participante activo.
- **Posición desktop**: panel lateral derecho interactivo con campo de texto. Reemplaza al Banter Rail en las pantallas donde el contexto es social (leaderboard, join tournament, cierre de torneo).
- **Posición mobile**: botón flotante (FAB) que al tocarse expande el chat en un drawer o modal de pantalla completa.
- **Comportamiento del BanterBot en el chat**:
  - Comenta resultados positivos y negativos de los jugadores con humor y picardía.
  - Provoca a los que tuvieron mala jornada ("¿En serio no viste ese gol viniendo?").
  - Celebra jugadas brillantes o predicciones exactas ("Exacto 3-1... ¿sabías algo que no nos contaste?").
  - Se sorprende de rachas de suerte inusual o predicciones perfectas encadenadas.
  - Nunca usa groserías. Tono: compañero de tribuna, no árbitro.
  - Los mensajes del BanterBot en el chat son generados por Claude API, contextualizados con los resultados reales de la jornada del jugador al que comenta.
- **Pantallas donde aparece**: Leaderboard completo, Join Tournament, Cierre de torneo, Post-jornada summary.
- **Implementación**: SignalR hub dedicado para el chat por torneo. Los mensajes del BanterBot se inyectan vía el mismo hub.

---

## Decisiones de Diseño — Resueltas

| # | Decisión | Resolución |
|---|----------|------------|
| 1 | Discord social login | **OUT OF SCOPE**. Solo ASP.NET Core Identity. Los mockups de Login y Register muestran Discord pero esa opción no se implementa. Los mockups tienen nota al respecto. |
| 2 | Wallet en el perfil | **OUT OF SCOPE**. No hay procesamiento de pagos. El ítem "Wallet" se ignora — mockup con nota. En el diseño final se elimina o reemplaza por "Prize History". |
| 3 | Global Career Ranking | **POST-MVP**. El dashboard MVP muestra únicamente la posición del jugador dentro de cada torneo activo. Los mockups tienen nota al respecto. |
| 4 | Jornada count en wizard | **RESUELTO**. Stepper `-`/`+` en el Basics step. Ver spec en "Wizard de Creación de Torneo". |
| 5 | Tournament Privacy | **SIEMPRE PRIVADO en MVP**. Los torneos solo son accesibles via invite link — no hay directorio público. El toggle "Privacy: Public" visible en `create_basics_mobile` es una idea para post-MVP: permitir torneos descubribles públicamente. Los mockups tienen nota al respecto. |
| 6 | Win Rate en dashboard y perfil | **POST-MVP**. Los mockups mobile muestran un stat de "Win Rate" (% de predicciones correctas históricas). No se implementa en MVP — requiere historial cross-torneo. Los mockups tienen nota al respecto. |
| 7 | Review step en mobile | **INCLUIDO**. El wizard mobile también tiene 5 pasos. El paso 5 (Review) no tiene mockup dedicado — se implementa como versión responsiva del desktop. |
| 8 | Arena Chat (peer-to-peer) | **IN SCOPE**. Los jugadores pueden chatear entre sí en tiempo real vía SignalR. El BanterBot participa activamente. Distinto al Banter Rail (solo lectura). Ver sección "Arena Chat". |
| 9 | Moneda "BANTER" en mockups | **IGNORAR**. Los mockups usan nombres ficticios. La implementación usa USD para todos los montos. |
| 10 | Paso "TEAMS" en wizard review | **ERROR DE MOCKUP**. El sidebar de `create_review_publish_web` muestra "3. TEAMS". El paso correcto es "4. MATCHES". Nota en el mockup. |
| 11 | Join Tournament con registro inline | **IN SCOPE**. Usuario sin cuenta puede registrarse directamente desde la pantalla de invitación. Al registrarse, queda automáticamente ligado al torneo. No hay Google/Discord OAuth — formulario propio. |
| 12 | Max Players por torneo | **IN SCOPE**. Campo opcional en el Basics step del wizard. Sin límite = torneo abierto. |
| 13 | Componentes esports en mockups (brackets, eliminate.) | **OUT OF SCOPE**. Solo quiniela tradicional en MVP. Los mockups de `playoff_management` son decorativos / referencia visual. No se implementa ningún sistema de brackets ni duelos. |

---

## Mockups Existentes

### Desktop (`mockups/`)

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
| `create_review_publish_web` | Wizard paso 5: Review & Publish |
| `forgot_password_web` | Recuperación de contraseña |
| `full_leaderboard_web` | Leaderboard completo del torneo |
| `join_tournament_web` | Unirse a torneo via invite link |
| `playoff_management_web` | Organizer: gestión de partidos fase final |
| `post_matchday_summary_web` | Resumen post-jornada (jugador) |
| `tournament_closure_prizes_web` | Cierre de torneo y distribución de premios |
| `tournament_history_web` | Historial de torneos completados |

### Mobile (`mockups/mobile_mockups/`)

| Mockup | Pantalla |
|--------|----------|
| `login_mobile` | Login |
| `register_mobile` | Registro |
| `dashboard_mobile` | Dashboard principal |
| `tournament_overview_mobile` | Vista general del torneo |
| `matchday_predictions_mobile` | Predicciones de jornada (jugador) |
| `organizer_console_mobile` | Consola del organizador |
| `create_basics_mobile` | Wizard paso 1: Datos básicos |
| `create_scoring_mobile` | Wizard paso 2: Configuración de puntos |
| `create_prizes_mobile` | Wizard paso 3: Distribución de premios |
| `create_matches_mobile` | Wizard paso 4: Selección de partidos |
| `user_profile_mobile` | Perfil + configuración del bot |
| `create_review_mobile` | Wizard paso 5: Review & Publish |
| `forgot_password_mobile` | Recuperación de contraseña |
| `full_leaderboard_mobile` | Leaderboard completo del torneo |
| `join_tournament_mobile` | Unirse a torneo via invite link |
| `playoff_management_mobile` | Organizer: gestión de partidos fase final |
| `post_matchday_summary_mobile` | Resumen post-jornada (jugador) |
| `tournament_closure_prizes_mobile` | Cierre de torneo y distribución de premios |
| `tournament_history_mobile` | Historial de torneos completados |

## Mockups Pendientes de Creación

| # | Pantalla | Versiones | Descripción |
|---|----------|-----------|-------------|
| 1 | **Create Tournament Basics — actualización** | desktop + mobile | Agregar campo "Jornadas" (stepper) y "Max Players". Eliminar Discord de Login/Register. |

> Los mockups pendientes #2-#10 del ciclo anterior ya tienen cobertura con los nuevos mockups entregados.

---

---

## Rollback Plan

- El repositorio legado (`quinielas-legacy/`) se mantiene intacto como referencia.
- PostgreSQL schema se versiona con EF Core Migrations — reversible.
- El bot de Telegram y el banter engine son módulos aislados — desactivarlos no rompe el core.
- API-Football tiene fallback: el organizador puede ingresar resultados manualmente si la API falla.
