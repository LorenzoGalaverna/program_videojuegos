# Análisis del Proyecto — Dusty FPS
**Fecha:** 2026-05-06  
**Integrantes:** Lorenzo Galaverna — Santiago Carranza  
**Motor:** Unity 6000.3.11f1 — HDRP  

---

## Bugs Críticos

### 1. Conflicto entre Recoil y MouseLook (cámara rota)
**Archivo:** `Assets/Scripts/Weapons/WeaponManager.cs:312` y `Assets/Scripts/Player/MouseLook.cs:36`

Ambos scripts escriben sobre `cameraTransform.localRotation` en cada frame. El que ejecuta último "gana" y anula al otro. Cuando el recoil decae a (0,0), WeaponManager fuerza la cámara a `Euler(0,0,0)` (mirando recto al frente), pisando el ángulo guardado por MouseLook.

**Impacto:** La cámara no responde correctamente al mouse después de disparar, o el recoil visual no funciona.

**Solución aplicada:** WeaponManager ya no escribe `localRotation`. Expone su recoil como propiedad (`RecoilX`). MouseLook lo suma a su propio ángulo acumulado.

---

### 2. Hit marker solo aparece con el cuchillo
**Archivo:** `Assets/Scripts/Weapons/Weapon.cs:163`

`lastHitTime` solo se actualiza dentro del bloque `if (weaponType == Knife)`. `WeaponManager.OnGUI` lee ese campo para mostrar el "X" blanco. Con pistola, rifle o sniper, nunca se actualiza → el jugador no recibe feedback visual de que impactó.

**Impacto:** Feedback de combate roto en 3 de 4 armas.

**Solución aplicada:** `lastHitTime = Time.time` movido fuera del bloque del cuchillo, al bloque de `if (targetHealth != null)`.

---

### 3. El jugador puede moverse y disparar mientras está muerto
**Archivo:** `Assets/Scripts/Setup/SceneSetup.cs:292`

El listener de `onDeath` solo suma el kill al score y programa el respawn. No desactiva `PlayerMovement` ni `WeaponManager` durante los 3 segundos.

**Impacto:** El jugador puede seguir disparando al bot durante la animación de muerte.

**Solución aplicada:** El listener desactiva los componentes al morir y los reactiva en `RespawnPlayer`.

---

### 4. Mensaje de victoria/derrota duplicado
**Archivo:** `Assets/Scripts/Game/GameManager.cs:71`

`AddPlayerKill()` dispara `"YOU WIN!"` y luego llama `EndGame()`. `EndGame()` revisa el score de nuevo y dispara `"VICTORY!"`. El jugador ve dos mensajes superpuestos.

**Solución aplicada:** Eliminados los mensajes `"YOU WIN!"` y `"YOU LOSE!"` de `AddPlayerKill`/`AddEnemyKill`. Solo `EndGame()` emite el mensaje final.

---

## Bugs Moderados

### 5. Crosshair puede ser invisible (lineTex sin inicializar)
**Archivo:** `Assets/Scripts/Game/GameHUD.cs:242`

`new Texture2D(1, 1)` crea la textura pero nunca llama `SetPixel(0,0,Color.white)` + `Apply()`. La textura tiene píxeles transparentes por defecto. `GUI.DrawTexture` multiplica el color de la textura por `GUI.color`, así que si la textura es transparente, el resultado es invisible.

**Solución aplicada:** Se agrega `SetPixel + Apply` al crear `lineTex`.

---

### 6. GUIStyle creado cada frame durante la recarga
**Archivo:** `Assets/Scripts/Game/GameHUD.cs:223`

`DrawReloadIndicator()` llama `new GUIStyle(GUI.skin.label)` dentro de `OnGUI`, que corre cada frame. Genera basura continua durante toda la animación de recarga.

**Solución aplicada:** El estilo de recarga se crea y cachea en `InitStyles()`.

---

### 7. Recoil no se resetea al cambiar de arma
**Archivo:** `Assets/Scripts/Weapons/WeaponManager.cs:315`

`SwitchWeapon()` no limpia `currentRecoil`. Si cambiás de arma con recoil pendiente, el arma nueva hereda el kick visual.

**Solución aplicada:** `currentRecoil = Vector2.zero` agregado en `SwitchWeapon()`.

---

### 8. El jugador puede respawnear después de que el juego terminó
**Archivo:** `Assets/Scripts/Setup/SceneSetup.cs:303`

`Invoke("RespawnPlayer", 3f)` se programa incluso si el juego termina en esos 3 segundos.

**Solución aplicada:** `RespawnPlayer()` verifica `GameManager.Instance.GameActive` antes de ejecutar.

---

### 9. `GetComponentInParent` llamado en cada disparo automático
**Archivo:** `Assets/Scripts/Weapons/Weapon.cs:118`

Con el rifle en modo automático, `GetComponentInParent<PlayerMovement>()` corre cada frame que se mantiene apretado el botón. Es una operación costosa.

**Solución aplicada:** Se cachea en `Start()`.

---

## Features Faltantes (vs. diseño en Entregables1y2.md)

### 10. Agacharse debería ser toggle, no hold
**Diseño:** *"Si el jugador presiona Ctrl, entonces **alterna** entre estado agachado"*  
**Código:** `Input.GetKey(LeftControl)` (mantener apretado)

**Solución aplicada:** Cambiado a `Input.GetKeyDown` que togglea un bool.

---

### 11. Modo ráfaga del bot no implementado
**Diseño:** *"Si el bot dispara 4 veces seguidas, pausa de fireRate × 4 segundos"*  
`EnemyBot` solo tiene un timer de cadencia simple sin contador de disparos.

**Solución aplicada:** Contador de tiros + pausa implementados.

---

### 12. Crosshair dinámico no implementado
**Archivo:** `Assets/Scripts/Game/GameHUD.cs:134`

El comentario `// Make crosshair bigger based on current weapon spread` estaba sin código.

**Solución aplicada:** El gap del crosshair ahora escala con el spread actual del arma.

---

### 13. No hay pantalla de fin de partida
Cuando termina el juego, el mensaje desaparece a los 3 segundos y el jugador puede seguir moviendose. No hay pantalla de resultado ni botón para volver al menú.

**Solución aplicada:** Al terminar el juego se deshabilitan los controles y aparece una pantalla de resultado en `GameHUD`.

---

### 14. Stats del bot no coinciden con el diseño
| Propiedad | Diseño | Código anterior |
|---|---|---|
| Detection range | 40u | 30f |
| Accuracy | 0.65 | 0.7 |
| Reaction time | 0.4s | 0.3f |

**Solución aplicada:** Valores actualizados en `EnemyBot`.

---

## Mejoras Menores

### 15. Dead code: `bulletHolePrefab` y `bloodEffectPrefab` en WeaponManager
`SpawnImpactEffect()` usa prefabs nunca asignados. `Weapon.cs` ya maneja los bullet holes via `BulletEffects`. Código duplicado que nunca corre.

**Decisión:** Mantenido por compatibilidad con prefabs futuros, pero documentado.

---

## Estado después de los fixes

| # | Problema | Estado |
|---|---|---|
| 1 | Recoil/MouseLook conflict | ✅ Corregido |
| 2 | Hitmarker solo en cuchillo | ✅ Corregido |
| 3 | Jugador se mueve muerto | ✅ Corregido |
| 4 | Mensaje doble fin de partida | ✅ Corregido |
| 5 | lineTex invisible | ✅ Corregido |
| 6 | GUIStyle GC cada frame | ✅ Corregido |
| 7 | Recoil no resetea al cambiar arma | ✅ Corregido |
| 8 | Respawn después del juego | ✅ Corregido |
| 9 | GetComponentInParent sin cachear | ✅ Corregido |
| 10 | Crouch toggle | ✅ Implementado |
| 11 | Bot burst fire | ✅ Implementado |
| 12 | Crosshair dinámico | ✅ Implementado |
| 13 | Pantalla fin de partida | ✅ Implementado |
| 14 | Stats del bot | ✅ Actualizados |
