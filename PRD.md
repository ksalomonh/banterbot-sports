# BanterBot Sports — Product Requirements Document

## 1. Product Overview

**BanterBot Sports** es una plataforma de quinielas de fútbol con motor de inteligencia artificial, nacida de una tradición entre amigos que comenzó en la **Eurocopa 2016** con una hoja de Excel y evolucionó a su primera versión digital en el **Mundial Rusia 2018**. Hoy es un negocio estructurado con modelo de distribución, gestión de organizadores y experiencia conversacional potenciada por IA.

### El negocio

BanterBot Sports opera como plataforma B2B2C: provee la infraestructura a **clubes** o **personas particulares** (los organizadores) para que gestionen sus propias quinielas, mientras retiene un porcentaje de la bolsa como tarifa de plataforma. Los organizadores, a su vez, obtienen su propio porcentaje. El equipo de BanterBot Sports también puede operar como organizador y crear sus propias quinielas directamente.

Las quinielas se construyen sobre **partidos de torneos profesionales e internacionales** — cualquier competición con cobertura en API-Football (Champions League, Copa del Mundo, Euros, ligas nacionales, etc.). El lanzamiento comercial apunta a la **Copa del Mundo 2026** (USA, México y Canadá). Los organizadores no crean torneos propios: eligen partidos de competiciones reales y arman sus quinielas sobre ellos.

### Cómo funciona

1. **Organizadores** pueden incorporarse a la plataforma de dos formas: siendo dados de alta por el equipo de BanterBot Sports, o registrando su propia cuenta de forma autónoma a través de la app web/mobile o por Telegram.
2. El organizador crea una quiniela y comparte un **enlace de invitación** con sus jugadores.
3. Los jugadores se unen, ingresan sus predicciones y realizan su pago.
4. Solo participan en la distribución del premio quienes hayan **confirmado su pago** antes del inicio del primer partido — confirmación manual por parte del organizador en el sistema.
5. Al finalizar la quiniela, la bolsa se distribuye según la configuración de porcentajes.

### Experiencia del jugador

Las predicciones pueden ingresarse desde múltiples canales, siempre de forma **privada**:
- **App web / mobile** (diseño responsivo)
- **Chat de la quiniela** — por mensaje de texto o audio (en privado, no visible para otros jugadores)
- **Telegram** — en conversación privada con el bot

Las predicciones permanecen ocultas para el resto de los jugadores hasta que el primer partido de la quiniela comience. En ese momento las predicciones se cierran y **todas se vuelven visibles** para cualquier participante.

Cada quiniela cuenta con un **chat exclusivo** para sus participantes donde pueden conversar por texto y audio. La IA del chat adopta una **personalidad juguetona y burlona**: comenta resultados, lanza bromas dirigidas a jugadores específicos o al grupo, y reacciona en tiempo real cada vez que los puntos de la quiniela se actualizan.

### Modelo de ingresos y configuración de porcentajes

Tanto BanterBot Sports como el organizador obtienen un porcentaje de la bolsa de cada quiniela. La configuración opera en dos niveles:

| Nivel | Actor | Alcance |
|-------|-------|---------|
| Global | BanterBot Sports | Define su porcentaje de plataforma y el rango permitido (mínimo/máximo) para el organizador — aplica por defecto a todas las quinielas |
| Por quiniela | BanterBot Sports | Puede sobrescribir el porcentaje para una quiniela específica |
| Global | Organizador | Define su porcentaje dentro del rango permitido por BanterBot Sports — aplica a todas sus quinielas |
| Por quiniela | Organizador | Puede ajustar su porcentaje individualmente sin superar los límites definidos por BanterBot Sports |

Cada actor tiene su propio **panel de control** para gestionar esta configuración.

> **Target de lanzamiento**: Copa del Mundo 2026 — USA, México y Canadá.

---

## 2. User Personas

### 2.1 Administrador — BanterBot Sports

**Perfil**: Gestor del negocio con visión 360°. Conoce las reglas del juego de todas las partes — la plataforma, los organizadores y los jugadores — y actúa como árbitro de última instancia. No es un perfil técnico puro ni comercial puro: es un operador de negocio que entiende los intereses de cada actor y toma decisiones que los balancen.

**Acceso**: Nivel máximo. Tiene visibilidad y control sobre todos los apartados de la plataforma — configuración global, gestión de socios, torneos de cualquier organizador y métricas generales. Puede consultar las predicciones de todas las quinielas de todos los organizadores, pero **solo en modo lectura** — no las modifica.

**Motivaciones**:
- Que las reglas sean justas para todas las partes: plataforma, organizadores y jugadores
- Que los jugadores disfruten la experiencia y vuelvan
- Que BanterBot Sports opere con integridad y transparencia

**Frustraciones**:
- Conflictos entre organizadores y jugadores sin trazabilidad
- Configuraciones de porcentajes fuera de rango que afecten la sostenibilidad del negocio
- Falta de visibilidad sobre quinielas activas o pagos pendientes

---

### 2.2 Organizador — Club (Persona Moral)

**Perfil**: Institución deportiva con base de afiliados establecida. No busca construir una comunidad desde cero — ya la tiene. Su interés en BanterBot Sports es agregar una capa de entretenimiento y retención a sus actividades existentes, generando al mismo tiempo un ingreso adicional sobre la participación de sus propios miembros. Arma quinielas sobre **partidos de torneos profesionales e internacionales** (Copa del Mundo, Euros, Champions League, ligas nacionales, etc.) — no sobre los torneos internos del club. Dado el volumen de afiliados, puede gestionar **múltiples quinielas simultáneas** para un mismo torneo.

**Cómo llega a la plataforma**: Puede ser dado de alta por BanterBot Sports o registrar su cuenta de forma autónoma a través de la app o Telegram.

**Qué hace en la plataforma**:
- Crea quinielas basadas en partidos de torneos profesionales e internacionales disponibles en API-Football
- Envía invitaciones a sus afiliados como prospectos de jugadores
- Los prospectos que acepten la invitación y no tienen cuenta generan una nueva; los que ya tienen cuenta inician sesión directamente
- Configura cada quiniela: porcentajes de bolsa, cantidad de ganadores y el porcentaje del premio que corresponde a cada posición ganadora
- Confirma manualmente los pagos de los jugadores antes del inicio del primer partido

**Motivaciones**:
- Generar un beneficio económico adicional a través de la participación de sus afiliados
- Fortalecer el vínculo con sus miembros ofreciéndoles entretenimiento complementario a sus torneos
- Retener afiliados con propuestas de valor más allá de la actividad deportiva

**Frustraciones**:
- Gestionar pagos y confirmaciones de forma manual o fuera de la plataforma
- No tener visibilidad clara sobre qué afiliados ya tienen cuenta y cuáles no
- Perder afiliados desinteresados por falta de incentivos adicionales

---

### 2.3 Organizador — Particular (Persona Física)

**Perfil**: Individuo que organiza quinielas para su círculo cercano — amigos, familia o compañeros de trabajo. Opera a menor escala que un club: típicamente **1 a 2 quinielas por torneo**, con un grupo reducido y conocido de participantes. Sin embargo, tiene acceso a exactamente las mismas capacidades de configuración que un club: porcentajes, ganadores, invitaciones y gestión de pagos. La diferencia es de escala y contexto, no de funcionalidad.

**Cómo llega a la plataforma**: Se registra de forma autónoma a través de la app o Telegram, o puede ser dado de alta por BanterBot Sports.

**Motivaciones**:
- Darle estructura y emoción a la tradición de apostar entre amigos
- Obtener un beneficio económico moderado por organizar el grupo
- Simplificar la gestión de pagos y resultados que antes hacía manualmente

**Frustraciones**:
- Herramientas demasiado complejas para grupos pequeños
- Tener que perseguir a amigos para que paguen o ingresen sus predicciones
- Perder el hilo de los resultados y puntos sin un sistema centralizado

---

### 2.4 Jugador

**Perfil**: Participante de una quiniela, invitado por un organizador. Puede ser afiliado de un club o parte del círculo cercano de un particular. Su relación con la plataforma comienza con una invitación y su nivel de compromiso depende de la experiencia que encuentre.

**Cómo llega a la plataforma**: Recibe un enlace de invitación del organizador. Si no tiene cuenta, la crea en el momento; si ya tiene una, inicia sesión directamente.

**Qué hace en la plataforma**:
- Ingresa sus predicciones de forma privada (por app, chat o Telegram) antes del inicio del primer partido
- Realiza su pago — su participación en la bolsa queda confirmada solo cuando el organizador lo registra en el sistema
- Consulta las predicciones de todos los jugadores una vez que el primer partido comienza
- Interactúa en el chat exclusivo de la quiniela (texto y audio) con los demás participantes y con la IA
- Sigue el marcador en tiempo real y ve cómo evolucionan sus puntos

**Motivaciones**:
- La emoción de competir y ganar un premio
- El entretenimiento social: bromear, presumir y sufrir junto al grupo
- Seguir los partidos con un incentivo adicional más allá del resultado deportivo

**Frustraciones**:
- No saber si su predicción fue registrada correctamente
- No poder ver el estado de la quiniela en tiempo real
- Una IA que aburre en lugar de divertir

---

## 4. Technical Architecture Overview

### 4.1 Stack Tecnológico

| Capa | Tecnología | Razón |
|------|-----------|-------|
| Backend | .NET 10 LTS + ASP.NET Core MVC | LTS garantiza soporte hasta 2026+, MVC probado en el sistema legado |
| ORM | EF Core 10 | Migrations, LINQ, integración nativa con .NET |
| Base de datos | PostgreSQL | Open source, robusto, soporte excelente en contenedores |
| Tiempo real | SignalR | Push de mensajes de chat y actualizaciones de resultados/ranking al browser sin polling del cliente |
| IA — Banter y parsing | Claude API (Anthropic) | Generación de banter personalizado y extracción de predicciones desde texto libre |
| IA — Transcripción | Whisper API (OpenAI) | Conversión de mensajes de audio a texto para predicciones por voz |
| Bot de mensajería | Telegram Bot API | Canal principal de interacción para registro de predicciones y notificaciones |
| Resultados en tiempo real | API-Football | Fuente de partidos, calendarios y resultados de torneos profesionales internacionales |
| Frontend | Razor Views + Tailwind CSS | Responsivo, sin framework JS adicional — mantiene el stack simple |

---

### 4.2 Capas de la Aplicación

La aplicación sigue una arquitectura en capas con separación estricta de responsabilidades:

| Proyecto | Responsabilidad |
|----------|----------------|
| `BanterBotSports.Web` | Controladores MVC, Razor Views, SignalR Hubs, configuración de middleware y DI |
| `BanterBotSports.BL` | Lógica de negocio: cálculo de puntos, distribución de premios, gestión de quinielas |
| `BanterBotSports.DAL` | EF Core DbContext, repositorios, migraciones, acceso a PostgreSQL |
| `BanterBotSports.Entities` | Entidades de dominio (en español), ViewModels, DTOs compartidos entre capas |
| `BanterBotSports.BanterAI` | Integración con Claude API: generación de banter y extracción de predicciones desde texto/audio |
| `BanterBotSports.Integrations` | API-Football (partidos y resultados), Telegram Bot, Whisper (transcripción de audio) |

---

### 4.3 Canales de Entrada

Los jugadores y organizadores interactúan con el sistema a través de tres canales:

- **App web / mobile**: interfaz responsiva accesible desde cualquier navegador. Cubre todas las funciones: predicciones, chat, ranking, gestión de quinielas y paneles de control.
- **Telegram**: canal conversacional para registro de predicciones (texto libre y audio) y notificaciones del bot. La IA responde con tono amigable y confirma acciones.
- **Chat de la quiniela** (dentro de la app): canal de comunicación en tiempo real exclusivo por quiniela, con participación activa del BanterBot.

---

### 4.4 Integraciones Externas

**API-Football**
Proveedor de datos de partidos y resultados de torneos profesionales internacionales. El sistema realiza polling periódico (cada ~5 minutos) durante partidos activos para obtener resultados actualizados. Cuando un resultado cambia, el backend lo procesa, actualiza la base de datos y usa SignalR para notificar a todos los clientes conectados en tiempo real. API-Football provee los datos; SignalR es el mecanismo de entrega al browser.

**Claude API**
Dos usos: (1) generación de mensajes de banter personalizados por jugador al actualizarse resultados, y (2) extracción de predicciones desde texto libre enviado por Telegram, con confidence score — si el score no supera el umbral, el bot pide al jugador que reformule.

**Whisper API**
Transcripción de mensajes de audio enviados por Telegram. El audio se convierte a texto y luego pasa por el mismo flujo de extracción de predicciones que el texto libre.

**Telegram Bot API**
Canal de interacción asíncrona. Recibe mensajes de jugadores, los encola para procesamiento y responde con confirmaciones. También envía mensajes del BanterBot al chat de la quiniela.

---

### 4.5 Tiempo Real

El sistema usa **SignalR** como mecanismo de push hacia los clientes conectados. Hay dos flujos principales:

- **Chat de la quiniela**: mensajes entre jugadores y respuestas del BanterBot se entregan en tiempo real a todos los participantes conectados.
- **Resultados y ranking**: cuando el polling de API-Football detecta un resultado actualizado, el backend procesa los puntos y usa SignalR para refrescar el ranking en el browser de todos los jugadores conectados sin necesidad de recarga de página.

En ambos casos, API-Football es la fuente de verdad de los datos; SignalR es el canal de entrega al cliente.

---

### 4.6 Modelo de Datos (Conceptual)

Entidades principales y sus relaciones de negocio:

```
BanterBotSports
    └── Organizador (Club | Particular)
            └── Quiniela
                    ├── Configuración (puntos, premios, monto)
                    ├── Partido[] (de API-Football)
                    ├── JugadorEnrolado[]
                    │       ├── PagoConfirmado: bool
                    │       └── Predicción[] (privada hasta inicio del 1er partido)
                    └── Chat
                            └── Mensaje[] (texto | audio | BanterBot)
```

---

### 4.7 Despliegue

**Recomendación: Coolify sobre VPS Hostinger**

[Coolify](https://coolify.io) es una plataforma de despliegue self-hosted, open source y gratuita. Se instala en el VPS existente y provee una experiencia similar a Heroku/Railway sin costo adicional: gestión de contenedores Docker, SSL automático (Let's Encrypt), variables de entorno, logs y redeploys desde Git.

**Stack de contenedores:**

| Contenedor | Imagen |
|-----------|--------|
| App | `banterbot-sports` (imagen propia desde Dockerfile) |
| Base de datos | `postgres:16-alpine` |
| Reverse proxy | Caddy o Nginx (manejado por Coolify) |

**Flujo de despliegue:**
1. Push a rama `main` en GitHub
2. Coolify detecta el cambio, reconstruye la imagen y hace rolling deploy
3. Sin downtime — el contenedor anterior se mantiene hasta que el nuevo responde healthy

**Alternativa DIY** (si se prefiere sin Coolify): Docker Compose + Nginx como reverse proxy + Certbot para SSL, todo gestionado manualmente en el VPS. Más control, más configuración manual.

> El VPS de Hostinger es suficiente para el lanzamiento en Copa del Mundo 2026. Si el crecimiento lo requiere, la misma arquitectura en contenedores es portable a cualquier cloud (DigitalOcean, Fly.io, Railway) sin cambios en el código.

---

## 5. Integrations & Dependencies

### 5.1 API-Football

**Propósito**: fuente de datos de partidos, calendarios y resultados de torneos profesionales internacionales. Es la única fuente de verdad para los partidos que componen una quiniela.

**Autenticación**: API Key en header `x-apisports-key`.

**Modelo de uso**: polling periódico desde un hosted service en background (`ResultSyncService`). Durante partidos activos, el sistema consulta resultados cada ~5 minutos. Fuera de partidos activos, el polling se reduce o se detiene.

**Usos concretos**:
- Búsqueda de competiciones y partidos para armar quinielas
- Obtención de resultados en tiempo real durante la jornada
- Carga progresiva de partidos de fase final cuando los equipos clasificados están definidos

**Limitaciones y consideraciones**:
- El tier gratuito tiene restricciones de rate y cobertura insuficientes para producción — **se requiere tier de pago** para el lanzamiento en Copa del Mundo 2026
- Si la API no responde, el sistema permite ingreso manual de resultados por parte del organizador como fallback
- En null mode (sin API key configurada), el sistema arranca normalmente pero desactiva todas las funciones dependientes de API-Football

**Costo**: variable según plan. A definir antes del lanzamiento.

---

### 5.2 Claude API (Anthropic)

**Propósito**: dos funciones independientes — generación de mensajes de banter personalizados y extracción de predicciones desde texto libre o transcripciones de audio.

**Autenticación**: API Key en header `x-api-key`.

**Modelo de uso**: llamadas on-demand por evento (resultado actualizado → banter; mensaje de jugador recibido → parsing de predicción).

**Usos concretos**:
- **Banter Engine**: genera mensajes de máximo 280 caracteres por jugador al actualizarse un resultado, con tono burlón y sarcástico. Inputs: nombre del jugador, sus predicciones vs resultados reales, posición en el ranking.
- **Prediction Parser**: extrae predicciones desde texto libre ("Argentina 2-0 Brasil") con un confidence score. Si el score no supera el umbral (0.75), el bot pide al jugador que reformule. Si supera el umbral, confirma y guarda.

**Limitaciones**:
- Banter está limitado a 280 caracteres por mensaje para mantener agilidad en el chat
- El parsing falla gracefully — si Claude no puede extraer predicciones con suficiente confianza, no guarda nada y notifica al jugador

**Costo**: pay-per-token según uso.

---

### 5.3 Whisper API (OpenAI)

**Propósito**: transcripción de mensajes de audio enviados por Telegram a texto, para que puedan procesarse por el mismo flujo de extracción de predicciones que el texto libre.

**Autenticación**: API Key de OpenAI (misma key que otras integraciones OpenAI si las hubiera).

**Modelo de uso**: llamada on-demand cuando se recibe un mensaje de audio en Telegram. El audio (formato OGG de Telegram) se envía a Whisper, se obtiene la transcripción y se pasa al Claude Prediction Parser.

**Usos concretos**:
- Transcripción de predicciones por voz: "Argentina dos cero Brasil, Francia uno a uno Alemania"
- El texto transcripto sigue el mismo flujo de confidence score y confirmación que el texto escrito

**Limitaciones**:
- La calidad de transcripción depende de la calidad del audio y del idioma — funciona bien en español
- Si la transcripción falla, el bot notifica al jugador y le pide que reenvíe el audio o escriba sus predicciones

**Costo**: pay-per-minute de audio transcripto.

---

### 5.4 Telegram Bot API

**Propósito**: canal de interacción asíncrona para registro de predicciones, confirmaciones del BanterBot y notificaciones de la quiniela.

**Autenticación**: Bot Token generado por BotFather — se configura como variable de entorno `Telegram:BotToken`.

**Modelo de uso**: webhook en producción (Telegram envía updates al endpoint de la app) y long polling en desarrollo local. En null mode (sin token configurado), el sistema arranca normalmente sin funcionalidad de Telegram.

**Usos concretos**:
- Registro de predicciones por texto libre o audio (privado, por conversación directa con el bot)
- Confirmación de acciones con tono amigable y divertido
- Participación del BanterBot en el chat de la quiniela
- Registro de cuenta de organizador o jugador a través del bot

**Limitaciones**:
- Telegram tiene rate limits de envío de mensajes por chat — relevante si una quiniela tiene muchos participantes y se generan muchos mensajes de banter simultáneos
- La vinculación de cuenta Telegram con cuenta de la app requiere un flujo de autenticación (token de 15 minutos generado en la web)

**Costo**: gratuito.

---

### 5.5 Resumen de Dependencias

| Integración | Auth | Modelo | Tier requerido | Fallback |
|-------------|------|--------|---------------|---------|
| API-Football | API Key | Polling | **Pago** | Ingreso manual de resultados |
| Claude API | API Key | On-demand | Pago (pay-per-token) | Sin banter / sin parsing de voz |
| Whisper API | API Key (OpenAI) | On-demand | Pago (pay-per-minute) | El jugador escribe en lugar de hablar |
| Telegram Bot API | Bot Token | Webhook / Long polling | Gratuito | Sin canal Telegram |

---

## 6. Security & Permissions Model

### 6.1 Roles del Sistema

| Rol | Descripción |
|-----|-------------|
| `Admin` | Equipo de BanterBot Sports — acceso total de gestión |
| `OrgClub` | Organizador persona moral (club) |
| `OrgParticular` | Organizador persona física — puede jugar en su propia quiniela |
| `Jugador` | Participante de una quiniela |

---

### 6.2 Matriz de Permisos

| Acción | Admin | OrgClub | OrgParticular | Jugador |
|--------|:-----:|:-------:|:-------------:|:-------:|
| CRUD organizadores | ✅ | ❌ | ❌ | ❌ |
| CRUD jugadores | ✅ | ❌ | ❌ | ❌ |
| Ver predicciones de cualquier quiniela (lectura) | ✅ | ❌ | ❌ | ❌ |
| Configurar % global de plataforma | ✅ | ❌ | ❌ | ❌ |
| Configurar rango % permitido al organizador | ✅ | ❌ | ❌ | ❌ |
| Definir monto mínimo de inscripción global | ✅ | ❌ | ❌ | ❌ |
| Crear quinielas | ✅ | ✅ | ✅ | ❌ |
| Configurar quiniela (puntos, premios, monto) | ✅ | ✅ | ✅ | ❌ |
| Confirmar pagos de jugadores | ❌ | ✅ | ✅ | ❌ |
| Clonar jugadores de quiniela anterior | ❌ | ✅ | ✅ | ❌ |
| Enviar invitaciones a jugadores | ❌ | ✅ | ✅ | ❌ |
| Ingresar resultados manualmente (fallback) | ❌ | ✅ | ✅ | ❌ |
| Jugar en su propia quiniela | ❌ | ❌ | ✅ | ✅ |
| Ingresar predicciones propias | ❌ | ❌ | ✅ | ✅ |
| Modificar predicciones propias (antes del cierre) | ❌ | ❌ | ✅ | ✅ |
| Modificar predicciones de otros jugadores | ❌ | ❌ | ❌ | ❌ |
| Ver predicciones propias (antes del cierre) | ❌ | ❌ | ✅ | ✅ |
| Ver predicciones de todos (post cierre) | ✅ | ✅ | ✅ | ✅ |
| Interactuar en el chat de la quiniela | ❌ | ✅ | ✅ | ✅ |

**Regla crítica**: ningún rol — incluyendo el organizador particular que juega en su propia quiniela — puede modificar predicciones ajenas ni las propias una vez iniciado el primer partido.

---

### 6.3 Reglas de Negocio con Impacto en Seguridad

- **Cierre de predicciones**: el sistema bloquea automáticamente toda modificación de predicciones al inicio del primer partido de la quiniela. No existe override manual para este bloqueo.
- **Participación del OrgParticular**: cuando un organizador particular juega en su propia quiniela, sus predicciones siguen exactamente las mismas reglas que las de cualquier jugador — sin privilegios adicionales.
- **Visibilidad de predicciones**: antes del cierre, cada jugador solo puede ver sus propias predicciones. El Admin puede verlas todas en modo lectura. Después del cierre, todos los participantes ven todas.
- **Confirmación de pago**: solo el organizador puede confirmar pagos — el jugador no puede marcarse a sí mismo como pagado.
- **Enlace de invitación**: expira a los 7 días. Un enlace vencido no permite registro ni acceso a la quiniela.

---

### 6.4 Autenticación

- **Credencial**: número de teléfono + contraseña. El número de teléfono es el identificador de login. El email se solicita únicamente para recuperación de contraseña — no se usa como credencial de acceso.
- **Flujo de registro**: el usuario ingresa número de teléfono, email y contraseña. La cuenta queda activa inmediatamente — no se requiere validación del número.
- **Auto-registro por Telegram**: un organizador o jugador puede iniciar el registro directamente desde el bot — el flujo es el mismo, el bot guía al usuario paso a paso.
- **Invite link con registro inline**: un usuario sin cuenta que recibe un enlace de invitación puede registrarse directamente en esa pantalla siguiendo el mismo flujo de validación por Telegram. Al completar, queda automáticamente enrolado en la quiniela.
- **Sesión**: cookie de ASP.NET Core Identity una vez validada la cuenta.

---

### 6.5 Seguridad Técnica

- **API Keys y secrets**: todas las claves externas (API-Football, Claude, Whisper, Telegram) se almacenan como variables de entorno — nunca en el código ni en archivos commiteados al repositorio.
- **HTTPS obligatorio**: todo el tráfico en producción va por HTTPS. Gestionado por Coolify + Let's Encrypt automático.
- **Páginas de error**: ninguna excepción ni stack trace se expone al browser en ningún entorno — rutas de error personalizadas en todos los casos.
- **Autorización por rol**: cada endpoint y acción está protegido por políticas de autorización de ASP.NET Core — no hay funciones accesibles sin el rol correspondiente.
- **Datos sensibles**: los archivos `.env` y `appsettings.*.json` con valores reales están excluidos del repositorio vía `.gitignore`.

---

## 7. Release Cycles

Los ciclos siguen el mismo modelo SDD incremental que ya llevamos — cambios pequeños, verificados y archivados antes de pasar al siguiente. Hay dos releases target antes del inicio de la Copa del Mundo 2026.

---

### Release 1 — MVP (20 de abril de 2026)

**Objetivo**: el juego funciona de punta a punta. Un organizador puede crear una quiniela real, los jugadores se unen y predicen, los resultados se actualizan solos y el chat hace el partido divertido.

| Módulo | Alcance |
|--------|---------|
| **Autenticación** | Registro con número de teléfono + contraseña. Validación del número vía enlace enviado por Telegram. La cuenta Telegram queda vinculada en el mismo paso. |
| **Quinielas** | Crear quiniela con nombre, monto de inscripción y configuración de puntos y premios. Selección de partidos individuales o jornada completa desde API-Football. Enlace de invitación para jugadores. |
| **Jugadores** | Registro inline desde enlace de invitación. Confirmación de pago por el organizador. |
| **Predicciones** | Ingreso por app web y por Telegram (texto libre). Cierre automático al inicio del primer partido. Visibilidad pública post-cierre. |
| **Resultados y puntuación** | Sincronización automática vía API-Football (polling cada ~5 min). Cálculo de puntos por resultado correcto y marcador exacto. Ranking en tiempo real vía SignalR. |
| **Chat + BanterBot** | Chat exclusivo por quiniela (texto). BanterBot anuncia inicio de jornada con link a predicciones, comenta cada resultado con humor y responde cuando es mencionado directamente. |
| **Deployment** | App en producción sobre VPS Hostinger con Coolify. HTTPS automático. Secret management por variables de entorno. |

**Fechas clave**:
- `10 de abril` — inicio de pruebas con usuarios reales
- `20 de abril` — release a producción

---

### Release 2 — Pre-Mundial (Mayo 2026)

**Objetivo**: experiencia completa antes del arranque de la Copa del Mundo 2026. Agrega los canales de audio, herramientas de gestión y el tercer concepto de puntuación.

| Módulo | Alcance |
|--------|---------|
| **Predicciones por audio** | Mensajes de voz en Telegram transcritos por Whisper y parseados por Claude. Mismo flujo de confirmación que texto. |
| **Clonación de jugadores** | El organizador puede reutilizar la lista de jugadores de una quiniela anterior. Solo requiere confirmación de pago. |
| **Panel Admin** | CRUD de organizadores y jugadores. Configuración global de porcentaje de plataforma, rango permitido al organizador y monto mínimo de inscripción. |
| **Panel Organizador** | Configuración global de porcentaje por quiniela. Sobrescritura por quiniela individual dentro de los límites de BanterBot Sports. |
| **Goles de la jornada** | Tercer concepto de puntuación: pronóstico de suma total de goles de la jornada. Configurable por quiniela, todo o nada. |

---

### Fuera de Alcance (Post-Mundial o indefinido)

- Gestión de pagos digitales — el dinero se maneja fuera de la plataforma
- App móvil nativa — la web responsiva cubre mobile
- Autenticación social (Google, Facebook, Discord)
- Otros canales de mensajería (WhatsApp, Discord)
- Ranking global cross-quiniela
- Sistema de logros / trofeos

---

## 8. Open Questions

### Resueltas

| # | Pregunta | Resolución |
|---|----------|-----------|
| OQ-1 | ¿Qué plan de API-Football contratar? | **Plan Pro ($19/mes, 7,500 req/día)**. Estimado de pico en Copa del Mundo: ~500 req/día. Pro tiene 15x de headroom. Escalar a Ultra ($29/mes) solo si se superan los 5,000 req/día sostenidos. |
| OQ-2 | ¿Validación del número de teléfono por Telegram? | **Eliminada**. Se asume que el número ingresado existe. No se valida vía Telegram. |
| OQ-3 | ¿Secret management en producción? | **Variables de entorno en Coolify**. Sin vault externo por ahora. |
| OQ-4 | ¿Límite de quinielas simultáneas por organizador? | **Sin límite**. Un organizador puede gestionar múltiples quinielas activas para el mismo torneo. |
| OQ-5 | ¿Qué pasa si un jugador no paga antes del primer partido? | El jugador es dado de baja de la quiniela, sus predicciones se borran y es retirado del chat. |
| OQ-6 | ¿El chat persiste después de que termina la quiniela? | No persiste. Se elimina por completo **60 días después** de finalizada la quiniela. |
| OQ-7 | ¿El modo de banter es configurable? | No es configurable. El modo es siempre **picante**. |
| OQ-8 | ¿BanterBot Sports opera con qué rol al crear sus propias quinielas? | Usa el rol **OrgBanterBot** — tratado como otro organizador más dentro de la plataforma. |
| OQ-9 | ¿Los porcentajes se calculan sobre bolsa total o neta? | Sobre la **bolsa total**. Ejemplo: bolsa de $1,000 → BanterBot 10% = $100, organizador 15% = $150, bolsa de premios = $750. |
| OQ-10 | ¿Recuperación de contraseña? | El registro también solicita **email** para este propósito. La recuperación de contraseña ya está implementada y funciona por email. El identificador de login es el número de teléfono; el email es solo para recovery. |

---

## 3. Core Features

### 3.1 Gestión de Quinielas

El organizador crea una quiniela con nombre personalizado y la configura con partidos de torneos profesionales disponibles en API-Football. Tiene dos modos de selección de partidos:

- **Selección libre**: elige partidos individuales de distintos torneos y fechas
- **Jornada completa**: selecciona todos los partidos de una jornada específica de un torneo

**Clonación de jugadores**: si ya existió una quiniela previa, el organizador puede clonar la lista de jugadores enrolados para la nueva quiniela. Los jugadores clonados solo requieren confirmación de pago para participar — no necesitan aceptar una nueva invitación.

**Invitación**: el organizador comparte un enlace de invitación. Los prospectos sin cuenta crean una al aceptar; los que ya tienen cuenta inician sesión directamente y quedan enrolados.

**Monto de inscripción**: el organizador define el monto de entrada por jugador para cada quiniela, respetando el mínimo global establecido por BanterBot Sports en su panel de control.

**Confirmación de pago**: el organizador registra manualmente el pago de cada jugador. Solo participan en la distribución del premio quienes estén confirmados antes del inicio del primer partido.

> **Fuera de alcance (por ahora)**: la plataforma no procesa pagos digitales. El dinero se gestiona en efectivo o directamente con BanterBot Sports.

---

### 3.2 Sistema de Puntuación

Tres conceptos de puntuación, configurables por el organizador **por quiniela** al momento de crearla:

| Concepto | Puntos por defecto | Descripción |
|---|---|---|
| Resultado correcto | 1 pt | Acertar ganador o empate sin importar el marcador |
| Marcador exacto | 1 pt | Acertar los goles exactos de ambos equipos |
| Goles de la jornada | 3 pts | Pronosticar la suma total de goles de todos los partidos de la jornada |

**Goles de la jornada**: el jugador ingresa un número de goles totales esperados. Si la suma de goles oficiales de todos los partidos coincide exactamente con el pronóstico, se otorgan los puntos completos. No hay puntos parciales — es todo o nada. El jugador puede optar por no pronosticar (no suma ni descuenta).

El cálculo de puntos es **automático** al actualizarse cada resultado. El ranking se actualiza en tiempo real vía SignalR.

---

### 3.3 Distribución de Premios

**Ganadores**: el organizador define la cantidad de posiciones ganadoras y el porcentaje de la bolsa que corresponde a cada una. La suma de los porcentajes de ganadores + porcentaje del organizador + porcentaje de BanterBot Sports debe ser igual al 100% de la bolsa.

**Regla de empates**:
- Si varios jugadores empatan en una misma posición ganadora, el premio de esa posición y todas las posiciones siguientes se divide en partes iguales entre los empatados.
- Ejemplo: si tres jugadores empatan en primer lugar, los tres se dividen la bolsa completa de premios (incluidos 2.° y 3.° lugar). Si un jugador gana el 1.° y dos empatan en 2.°, el ganador recibe el premio de 1.° y los dos empatados se dividen el de 2.° y 3.°.

> **Fuera de alcance (por ahora)**: BanterBot Sports no gestiona la entrega del premio. El organizador es responsable de entregar el monto correspondiente a los ganadores.

---

### 3.4 Predicciones

Los jugadores ingresan sus predicciones de forma **privada** por cualquiera de estos canales:

- **App web / mobile**: formulario por quiniela con un input por partido (goles local y visitante) más un campo opcional para goles totales de la jornada
- **Chat de la quiniela**: mensaje de texto o audio en privado — no visible para otros jugadores
- **Telegram**: conversación privada con el bot — texto libre o mensaje de voz

Las predicciones se cierran automáticamente al inicio del primer partido de la quiniela. A partir de ese momento **ningún jugador puede corregir ni agregar predicciones**, y todas las predicciones de todos los participantes se vuelven visibles para cualquier miembro de la quiniela.

---

### 3.5 IA — BanterBot

La IA tiene dos modos de interacción según el canal:

**En Telegram** (asistente de registro):
- Registra predicciones enviadas por texto libre o audio (Whisper transcribe, Claude parsea)
- Confirma las acciones del jugador con un tono divertido y amigable
- Flujo: mensaje → extracción con confidence score → confirmación → el jugador confirma o corrige → guardado

**En el chat de la quiniela** (personalidad burlona):
- Anuncia el inicio de la jornada
- Comenta cada actualización de resultado con una broma o comentario sarcástico
- Responde preguntas **solo si es mencionada directamente** (tagged)
- Da resultados de partidos cuando se le pregunta, siempre con un comentario sarcástico o humorístico
- Máximo 280 caracteres por mensaje

---

### 3.6 Chat de la Quiniela

Cada quiniela tiene un chat exclusivo para sus participantes con las siguientes capacidades:

- Mensajes de texto y audio entre jugadores
- Participación activa de la IA BanterBot (ver 3.5)
- **Notificaciones en el chat**:
  - Anuncio de inicio de jornada + link donde los jugadores pueden consultar las predicciones de todos los participantes
  - Actualización de cada resultado de partido

No se envían notificaciones por otros canales (push, email, Telegram) para eventos de quiniela.

---

### 3.7 Panel de Control — BanterBot Sports

Configuración global que aplica como default a todas las quinielas:

- Porcentaje de plataforma que retiene BanterBot Sports
- Rango permitido (mínimo y máximo) del porcentaje que puede cobrar el organizador
- Monto mínimo de inscripción por quiniela
- **CRUD de organizadores**: alta, edición, baja de cuentas de organizadores
- **CRUD de jugadores**: alta, edición, baja de cuentas de jugadores en caso de necesidad operativa

> **Fuera de alcance (por ahora)**: CRUD de usuarios internos de BanterBot Sports.

---

### 3.8 Panel de Control — Organizador

Configuración aplicable a sus quinielas:

- Porcentaje global que cobrará el organizador (dentro del rango definido por BanterBot Sports)
- Sobrescritura por quiniela individual (sin superar los límites globales)
- Cantidad de ganadores por quiniela y porcentaje de bolsa por posición
- Monto de inscripción por quiniela (respetando el mínimo de BanterBot Sports)
- Configuración de puntos por quiniela (valores por defecto editables)
- Confirmación de pagos de jugadores
