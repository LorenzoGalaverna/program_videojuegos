Entregables 1 y 2 — Diseño Inicial y Diseño Detallado
Materia: Taller de Videojuegos 
Universidad Católica de Córdoba 
Fechas de entrega: 28 de Abril de 2026 (E1) — 5 de Mayo de 2026 (E2) 
Integrantes: Lorenzo Galaverna — Santiago Carranza

1. Nombre del juego
Dusty
2. Objetivo principal del juego
Eliminar al jugador rival en un enfrentamiento 1 contra 1 dentro de un mapa cerrado de combate. Gana quien alcance primero la cantidad objetivo de eliminaciones (10 kills) o quien tenga más eliminaciones cuando se agote el tiempo de la ronda (3 minutos).
3. Público objetivo
Edad: 14 a 30 años.
Perfil: jugadores de PC con interés en shooters competitivos en primera persona (FPS), familiarizados con títulos como Counter-Strike, Valorant o Call of Duty.
Experiencia: desde casual hasta intermedio. No requiere conocimientos previos de shooters tácticos complejos, pero sí reflejos básicos y manejo de mouse y teclado.
4. Plataforma
Computadora (PC) — Windows y Mac. Desarrollado en Unity 6.3 con HDRP (High Definition Render Pipeline) para gráficos de alta calidad.
5. Géneros
Género de jugabilidad: Shooter en primera persona (FPS) — Arena Deathmatch.
Género narrativo: Acción / Combate competitivo. Sin narrativa profunda; el contexto es un duelo deportivo entre dos contendientes en una arena cerrada.
6. Estructura del juego
Inicio
Los dos jugadores aparecen (spawnean) en extremos opuestos del mapa, en sus respectivas zonas marcadas (azul y roja). Cada uno empieza con armamento completo (pistola, rifle de asalto y rifle de francotirador), 100 puntos de vida, y munición llena. Aparece un mensaje “GAME START!” y arranca el contador de 3 minutos.
Core Loop
Spawn → Explorar el mapa → Detectar al rival → Combatir →
   ├── Ganar el duelo → +1 al score → Respawn del rival
   └── Perder el duelo → +1 al rival → Respawn propio (3s)
   → Repetir hasta llegar a 10 kills o que se acabe el tiempo
El loop combina exploración táctica (rotaciones por el mapa, uso de cobertura), toma de decisiones (cuándo empujar, cuándo aguantar posición, qué arma usar según la distancia) y ejecución mecánica (apuntado, control de retroceso, movimiento).
Resultado
Victoria: llegar a 10 eliminaciones antes que el rival, o tener más eliminaciones cuando termine el tiempo (3 minutos).
Derrota: que el rival alcance las 10 eliminaciones primero, o terminar el tiempo con menos eliminaciones que él.
Empate: ambos terminan con la misma cantidad de eliminaciones al agotarse el tiempo.
7. Mecánica del juego
Elementos
Jugador: personaje en primera persona con vida (100 HP), cámara, y un arma equipada.
Armas: 3 disponibles, cada una con stats distintos.
Pistola: 30 daño, recarga rápida, 12 balas por cargador.
Rifle de asalto (AK-47): 25 daño, automático, 30 balas por cargador.
Rifle de francotirador (AWP): 80 daño, un disparo a la vez, 5 balas por cargador, alta precisión con zoom.
Mapa: arena cerrada estilo Dust 2 simplificado, con corredores, paredes, cajas de cobertura y una construcción central.
HUD: crosshair dinámico, vida, munición, score, timer y nombre del arma activa.
Bot enemigo (modo offline): IA que patrulla el mapa, detecta al jugador en su campo de visión y lo enfrenta.
Reglas principales
Si el jugador recibe daño, su vida baja. Si llega a 0 muere y respawnea en 3 segundos.
Un disparo a la cabeza (headshot) hace 2.5x el daño normal.
Cada arma tiene retroceso (recoil) y dispersión (spread) que aumentan al disparar continuamente y se recuperan cuando el jugador deja de disparar.
Solo se puede ver al rival si está dentro del campo de visión y no hay obstáculos en el medio.
La ronda termina cuando un jugador alcanza 10 kills o se agotan los 3 minutos.
Controles
Acción
Tecla
Mover
W A S D
Correr
Shift
Agacharse
Ctrl
Saltar
Space
Mirar
Mouse
Disparar
Click izquierdo
Apuntar (ADS)
Click derecho
Recargar
R
Cambiar arma
1 / 2 / 3 o rueda del mouse

8. Dinámica del juego — Taxonomía de Bartle
El juego apunta principalmente a dos tipos de jugadores según la taxonomía de Bartle:
Killer (principal): el núcleo del juego es la confrontación directa entre jugadores. La satisfacción central viene de derrotar al rival, dominarlo y demostrar habilidad mecánica. Casi todas las decisiones del jugador giran alrededor de cómo eliminar al otro de forma más eficiente.
Achiever (secundario): el sistema de score y los headshots premian la mejora continua. El jugador busca mejorar su K/D, alcanzar 10 kills más rápido, lograr disparos a la cabeza y dominar el control del retroceso de cada arma.
En menor medida también participa el perfil Explorer durante las primeras partidas, mientras el jugador aprende el mapa, las posiciones de cobertura, los ángulos y las rotaciones óptimas. Esta etapa se diluye una vez que el jugador domina el escenario.
9. Estética — Emoción principal
La emoción central que se busca producir es tensión competitiva: la sensación de que cada esquina puede ser el lugar donde aparece el rival, y que cada disparo puede ser el último. Esto se logra a través de:
Mapas cerrados con visibilidad parcial: el jugador nunca tiene control total de la información, lo que genera incertidumbre y vigilancia constante.
Tiempo de “kill” muy bajo: los duelos se resuelven en menos de un segundo si la puntería es buena. Esto premia los reflejos y castiga las distracciones, manteniendo al jugador en estado de alerta.
Recompensa inmediata y feedback claro: sonido, hit-marker, contador de score que sube. La derrota también es inmediata y clara, lo que produce el ciclo emocional clásico del FPS competitivo: tensión → resolución → revancha.
En segundo plano, el juego ofrece la emoción de dominio (mastery): la sensación de progresión personal al manejar mejor el retroceso, encontrar mejores ángulos o lograr headshots consistentes.

High Concept (resumen en una oración)
Un shooter 1 contra 1 en primera persona donde dos jugadores se enfrentan en una arena cerrada con tres armas distintas, ganando quien llegue primero a 10 eliminaciones.

Entregable 2 — Diseño Detallado
10. Reglas semánticas (sin ambigüedad)
Las reglas se describen en formato condición → consecuencia para que su comportamiento sea unívoco e implementable directamente en código.
Reglas de movimiento del jugador
Si el jugador presiona W / A / S / D, entonces se desplaza en la dirección correspondiente a velocidad de caminata (4.5 unidades/segundo).
Si el jugador presiona Shift mientras se mueve y no está agachado, entonces la velocidad pasa a ser de carrera (7 unidades/segundo).
Si el jugador presiona Ctrl, entonces alterna entre estado agachado (altura del CharacterController = 1, velocidad = 2.5 u/s) y mejor precision.
Si el jugador presiona Space y está apoyado en el suelo y no está agachado, entonces se aplica una fuerza vertical positiva que produce un salto.
Si el jugador no está apoyado en el suelo, entonces se le aplica una aceleración vertical negativa (gravedad = -15 u/s²).
Reglas de cámara
Si el jugador mueve el mouse, entonces la cámara rota en el eje X (vertical, limitado a ±89°) y el cuerpo del jugador rota en el eje Y (horizontal, sin límite).
Reglas de armas
Si el jugador presiona Click Izquierdo y el arma actual no está recargando y el cargador tiene al menos 1 bala y ha pasado el tiempo entre disparos, entonces se dispara un raycast desde la cámara, se descuenta 1 bala del cargador y se aumenta la dispersión actual del arma.
Si el raycast del disparo impacta un collider con la etiqueta Head, entonces el daño aplicado al PlayerHealth correspondiente se multiplica por 2.5.
Si el raycast del disparo impacta un collider que pertenece a un objeto con PlayerHealth, entonces se le aplica el daño base del arma (modificado si fue headshot).
Si el cargador del arma llega a 0 munición, entonces se inicia automáticamente la recarga.
Si el jugador presiona R y el cargador no está lleno y la reserva tiene al menos 1 bala, entonces se inicia la recarga durante el tiempo definido por el arma (1.5s a 3s según el tipo).
Si termina el tiempo de recarga, entonces se transfieren balas de la reserva al cargador hasta llenarlo o agotar la reserva.
Si el jugador presiona Click Derecho, entonces el FOV de la cámara baja (zoom in) y el arma se desplaza al centro de la pantalla, simulando apuntar con mira.
Si el jugador presiona 1, 2 o 3 o usa la rueda del mouse, entonces se desactiva el arma actual y se activa la nueva.
Si un arma dispara, entonces la cámara recibe un retroceso vertical (y horizontal aleatorio) acumulativo, limitado a un máximo de 8°.
Si el jugador deja de disparar, entonces el retroceso acumulado vuelve suavemente a 0 a una velocidad definida por el arma.
Reglas de daño y muerte
Si la vida del jugador llega a 0, entonces se dispara el evento onDeath, el jugador queda marcado como muerto y no puede recibir más daño.
Si el jugador local muere, entonces se le suma 1 al score del enemigo y comienza un timer de 3 segundos.
Si el bot enemigo muere, entonces se le suma 1 al score del jugador y comienza un timer de 3 segundos.
Si el timer de respawn de un personaje llega a 0, entonces su vida se restaura a 100, su posición se cambia a un spawn point aleatorio de su equipo y vuelve a estar activo.
Reglas del bot enemigo
Si el bot está en estado Patrol y llega a su punto objetivo, entonces espera entre 1 y 3 segundos y elige un nuevo punto aleatorio dentro de su radio de patrulla (12 unidades).
Si el bot detecta al jugador dentro de su rango (40 u), su campo de visión (120°) y con línea de vista directa, entonces pasa a estado Attack después de su tiempo de reacción (0.4s).
Si el bot pierde la línea de vista del jugador, entonces pasa a estado Chase y se mueve hacia la última posición conocida.
Si el bot está en estado Attack y han pasado los segundos de su fireRate, entonces dispara un raycast hacia la posición del jugador con una desviación inversamente proporcional a su precisión (0.65).
Si el bot dispara 4 veces seguidas, entonces hace una pausa de fireRate × 4 segundos antes del próximo disparo (simula una ráfaga).
Reglas de la partida
Si el score de un jugador alcanza 10 eliminaciones, entonces se finaliza la partida y se muestra “VICTORY!” o “DEFEAT!” según corresponda.
Si el timer de la ronda llega a 0, entonces se finaliza la partida y se compara el score: el mayor gana, si son iguales es empate.
Si la partida está finalizada, entonces ningún score se sigue actualizando aunque haya kills.

11. Características generales del juego
Características principales
Modo de juego offline: 1 jugador humano contra 1 bot con IA simple basada en estados (Patrol / Chase / Attack / Dead).
Modo de juego LAN (1v1 en red local): 1 jugador humano vs 1 jugador humano dentro de la misma red local (Wi-Fi o cable). Uno de los dos hostea la partida y el otro se conecta por IP.
Mapa cerrado estilo Dust 2 simplificado, con corredores, paredes y cajas de cobertura.
Sistema de armas modular basado en ScriptableObjects (WeaponData), permitiendo agregar nuevas armas sin modificar lógica.
HUD diegético con crosshair dinámico, score, timer, vida, munición y mensajes de partida.
Sistema de spawn points por equipo (azul / rojo) y respawn automático tras 3 segundos.
Sistemas a grandes rasgos
A continuación se identifican los sistemas principales del juego, su responsabilidad y los componentes (scripts) que los implementan.
Sistema
Responsabilidad
Scripts principales
Jugador (Player System)
Controla el movimiento, la cámara, la vida y las acciones del jugador local.
PlayerMovement, MouseLook, PlayerHealth
Sistema de Armas (Weapons System)
Gestiona los tipos de arma, sus stats, el disparo, la recarga, el retroceso y el cambio entre armas.
WeaponManager, Weapon, WeaponData
Sistema de Combate (Combat System)
Ejecuta los raycasts de los disparos, calcula el daño (incluido headshot) y aplica el daño a los PlayerHealth impactados.
Weapon (lógica de raycast) y PlayerHealth (recepción de daño)
Inteligencia Artificial (AI System)
Controla al bot enemigo: detección, patrullaje, persecución y disparo.
EnemyBot
Game Manager (Match System)
Lleva el score, el timer, las condiciones de victoria/derrota y los respawns.
GameManager
HUD / Interfaz (UI System)
Muestra la vida, munición, score, timer, crosshair, hit-marker y mensajes de partida.
GameHUD
Setup / Bootstrap (Initialization)
Inicializa la escena en runtime: crea el mapa, instancia al jugador, al bot, al GameManager y conecta sus referencias.
SceneSetup, FPSBootstrap
Networking LAN (Multiplayer System) (planeado)
Sincronizar el estado entre dos clientes en la misma red local: posiciones, disparos, vida y score. Modelo cliente-servidor con uno de los dos jugadores como host
A definir (Mirror / Netcode for GameObjects)

Características técnicas
Motor: Unity 6.3 con HDRP (High Definition Render Pipeline).
Lenguaje: C# (.NET / Unity).
Input: legacy Input Manager (con compatibilidad para el nuevo Input System).
Plataforma: PC — Windows y Mac.
Resolución objetivo: 1920x1080.
FPS objetivo: 120 FPS.
Persistencia: por ahora ninguna; se podría agregar guardado de mejor score o configuración (sensibilidad, FOV) en PlayerPrefs en el Entregable 5.

12. Diagrama Entidad-Relación simplificado
El siguiente diagrama muestra cómo se comunican los sistemas entre sí. Cada flecha indica el flujo de información o llamadas. La regla general es que los sistemas de lógica no conocen al HUD: este se suscribe a sus eventos.
              

Lectura del diagrama (relaciones clave)
FPSBootstrap → SceneSetup: el bootstrap es el único componente que se coloca a mano en la escena; instancia el SceneSetup que arma todo lo demás.
SceneSetup → todo lo demás: crea el mapa, el jugador (con sus subsistemas), el bot y el GameManager, y conecta sus referencias.
Player (contiene a) → PlayerMovement, MouseLook, PlayerHealth, WeaponManager: el jugador es un GameObject contenedor que agrupa los componentes de su comportamiento.
WeaponManager → Weapon[] → WeaponData: el manager controla un array de armas; cada arma lee sus stats de un WeaponData (ScriptableObject reutilizable y editable sin tocar código).
Weapon (raycast) → PlayerHealth: cuando un disparo impacta un collider con PlayerHealth, le llama directamente TakeDamage(...). El arma no conoce el HUD ni el GameManager.
PlayerHealth.onDeath → GameManager: cuando el jugador muere se notifica al GameManager para que sume kill al rival y dispare el respawn. Esto desacopla la lógica de combate de la lógica de partida.
GameHUD se suscribe a los eventos UnityEvent de PlayerHealth, WeaponManager y GameManager. Es solo lectura: no modifica el estado, lo refleja. Esto permite cambiar el HUD sin tocar la lógica del juego.
Por qué esta separación
El Combat System (raycast + daño) no sabe quién es el jugador o el bot, sólo conoce PlayerHealth. Esto permite usar las mismas armas para el jugador y para el bot sin código duplicado.
El GameManager no sabe cómo se calcula el daño, sólo recibe notificaciones de muertes. Esto permite cambiar las reglas de la partida (round-based, deathmatch, time-attack) sin modificar el sistema de armas.
El HUD no escribe sobre la lógica, sólo escucha eventos. Esto evita “spaghetti” y permite tener distintos HUDs (por ejemplo uno para single-player y otro para LAN) reutilizando los mismos sistemas.


