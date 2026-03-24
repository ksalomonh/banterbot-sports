# PRD: BanterBot Sports

## Contexto e Intent

BanterBot Sports es la evolución de un sistema de quinielas de fútbol usado por un grupo de amigos desde la **Eurocopa 2016** (Excel) y el **Mundial Rusia 2018** (app .NET Core 2.0). El objetivo es modernizar y extender esa app a **.NET 10 LTS** con todas las reglas reales del juego ya validadas en producción, incorporando una IA que interactúa con los jugadores con banter personalizado al cierre de cada jornada.

El punto de partida es el repositorio legado: [Adriansillo/Quinielas](https://github.com/Adriansillo/Quinielas) (.NET Core 2.0 + PostgreSQL).

---

## Estructura de la Quiniela

La quiniela es un **torneo de largo aliento**, similar a una liga de fútbol. No es una quiniela de un solo partido ni de una sola jornada. Funciona así:

- El **organizador** crea el torneo, define la cantidad de jornadas, cuántos partidos tiene cada una y qué partidos la componen.
- Los **jugadores** participan durante toda la duración del torneo.
- Hay una **tabla general** que acumula puntos de todas las jornadas.
- Al final del torneo, los lugares del podio reciben su porcentaje del prize pool.

---

## Sistema de Puntos (configurable por el organizador)

| Concepto | Puntos por defecto | Descripción |
|---|---|---|
| Resultado correcto | 1 pt | Acertar ganador o empate (sin importar el marcador exacto) |
| Marcador exacto | 1 pt | Acertar los goles exactos de ambos equipos |
| Goles de la jornada | 3 pts | El jugador pronostica la suma total de goles de los partidos de la jornada que definió. Si coincide con la realidad, gana los puntos |

> Los valores de puntos por cada concepto son **configurables por el organizador**. Ejemplo: puede otorgar 2 pts por resultado correcto en lugar de 1.

**Nota sobre goles de jornada**: el jugador puede ingresar una suma personalizada o dejar que la app sume automáticamente sus predicciones de goles de los partidos. Si la suma de goles oficiales de esos partidos coincide con su pronóstico, gana los puntos.

---

## Premio y Distribución (configurable)

- Cada jugador aporta un **monto fijo de inscripción**.
- El organizador configura **cuántos lugares reciben premio** y **qué porcentaje** corresponde a cada uno.
- Ejemplo Rusia 2018: 3 ganadores → 1° 70%, 2° 20%, 3° 10%.
- En caso de **empate entre posiciones**, el premio de esos lugares se divide en partes iguales.

---

## Deadlines y Bloqueo de Predicciones

- Los jugadores tienen hasta el **inicio del primer partido de la jornada** para ingresar sus predicciones.
- Una vez iniciado el primer partido, los campos de predicción se **bloquean para los jugadores**.
- **Solo el organizador** puede ingresar o modificar marcadores una vez bloqueada la jornada.
- El organizador **puede ser también un jugador**.

---

## Scope

### In Scope (MVP)
- Registro/login de usuarios (migrar el sistema de auth existente)
- Creación de torneos con configuración completa: nombre, jornadas, partidos por jornada, monto de inscripción, puntos configurables, distribución de premios configurable
- Invitación de participantes vía link
- Ingreso de predicciones por partido: marcador equipo 1 y equipo 2
- Pronóstico de goles totales por jornada (con opción de autocompletar con suma de predicciones)
- Bloqueo automático de predicciones al inicio del primer partido de la jornada
- Ingreso de resultados oficiales por el organizador
- Cálculo automático de puntos (resultado, marcador exacto, goles de jornada)
- Tabla general acumulada del torneo
- Tabla de posiciones por jornada
- Gestión del prize pool (quién pagó, cuánto, ganadores)
- El organizador puede participar como jugador
- IA Banter Engine: mensajes personalizados por jugador al cerrar cada jornada
- Historial de torneos

### Out of Scope (MVP)
- Integración de pagos reales (el dinero se gestiona fuera de la app)
- App móvil nativa (web responsiva primero)
- Chat entre jugadores
- Integración automática con API de fútbol para resultados (el organizador los ingresa manualmente en v1)
- Autenticación social (Google, Facebook)

---

## Stack

| Capa | Tecnología | Justificación |
|---|---|---|
| Framework | .NET 10 LTS | Migración desde .NET Core 2.0. LTS activo a 2026. |
| Web | ASP.NET Core MVC / Razor Pages | Misma arquitectura del legado, migración incremental |
| ORM | Entity Framework Core 10 | Ya usado en legado con EF Core 2.0 |
| Base de datos | PostgreSQL | Ya usado en legado (Npgsql). Gratuito y open source. |
| Auth | ASP.NET Core Identity | Ya integrado en legado |
| Real-time | SignalR | Para actualizaciones de tabla en vivo |
| IA Banter | Claude API (claude-haiku-4-5) | Mensajes personalizados, control de tono, bajo costo |
| Tiempo real resultados | Manual (v1) → API-Football (v2) | Organizador ingresa resultados en MVP |

---

## Arquitectura del Proyecto (hereda del legado)

```
BanterBotSports/
├── BanterBotSports.Web/         # ASP.NET Core — controllers, views, wwwroot
├── BanterBotSports.BL/          # Business Logic Layer
├── BanterBotSports.DAL/         # Data Access Layer + EF Core + Migrations
├── BanterBotSports.Entities/    # Domain entities y ViewModels
├── BanterBotSports.BanterAI/    # Claude API integration — Banter Engine
└── BanterBotSports.sln
```

---

## Entidades Principales

- `Torneo` — nombre, configuración de puntos, configuración de premios, monto inscripción, organizador
- `Jornada` — número, fecha límite de predicciones, estado (abierta / cerrada)
- `Partido` — equipo1, equipo2, fecha/hora kick-off, goles oficiales
- `Participante` — relación usuario ↔ torneo, rol (organizador / jugador / ambos), estado de pago
- `PrediccionPartido` — goles predichos equipo1 y equipo2, puntos obtenidos
- `PrediccionJornada` — goles totales pronosticados para la jornada, puntos obtenidos

---

## Risks

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Ruptura de breaking changes .NET Core 2.0 → .NET 10 | Alta | Migración incremental. Ejecutar tests de smoke en cada upgrade de versión mayor. |
| Lógica de empates en premios compleja | Media | Cubrir con tests unitarios exhaustivos antes de cualquier release. |
| Banter ofensivo o inapropiado | Media | System prompt con guardrails + validación de output antes de mostrar. |
| Bloqueo de predicciones con zonas horarias diferentes | Baja | Almacenar kick-off en UTC, mostrar en timezone del usuario. |
| Regulación legal de pools de dinero | Media | La app no procesa pagos. Gestión de dinero fuera del sistema. |

---

## Success Criteria

- [ ] Migración completa a .NET 10 sin pérdida de funcionalidad existente
- [ ] Un organizador puede crear un torneo completo con jornadas y partidos en < 5 minutos
- [ ] Los jugadores pueden ingresar predicciones de una jornada en < 3 minutos
- [ ] El bloqueo de predicciones ocurre automáticamente al iniciar el primer partido de la jornada
- [ ] Los puntos se calculan correctamente para los 3 conceptos (resultado, marcador, goles jornada)
- [ ] La tabla general refleja puntos acumulados de todas las jornadas en tiempo real
- [ ] Cada jugador recibe al menos 1 mensaje de banter personalizado al cerrar cada jornada
- [ ] La distribución de premios con empates se calcula correctamente según las reglas configuradas

---

## Rollback Plan

- El repositorio legado (`quinielas-legacy/`) se mantiene intacto como referencia.
- PostgreSQL schema se versiona con EF Core Migrations — cualquier migración es reversible.
- El banter engine es un módulo aislado — desactivarlo no rompe el resto de la app.
