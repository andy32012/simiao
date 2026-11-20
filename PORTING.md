# 霧鎖千佛山 → Unity port: shared contract

Source of truth: `scratchpad/game/index.html` (three.js r128, ~1935 lines). Unity 6000.0.77f1, URP, legacy Input Manager, uGUI.
Project: `C:\Users\USER\Desktop\QFS-Unity`. Scripts in `Assets/Game/Scripts`, shaders in `Assets/Game/Shaders`, editor code in `Assets/Game/Editor`.

## Golden rules
1. **Port line by line, keep the JS identifiers.** All gameplay code lives in `public static partial class G` (no namespace), one file per module.
   A JS global `LANTERNS` is `G.LANTERNS`, a JS function `showMsg` is `G.showMsg`. Other modules rely on the JS names — do not rename, do not "improve".
   C# keyword clashes in *locals* get a trailing underscore (`out_`, `base_`, `in_`). Everything `public static`.
2. **Game logic stays in three.js coordinates** (x east, y up, +z south, north = −z, forward = (−sin yaw, −cos yaw)). Never negate z in gameplay code.
   Only `Obj`, `CamW`, `PLight`, `MB.geo()`, `G.meshFromArrays`, `Scatter` convert to Unity space. Never touch `Transform` directly; use `Obj`.
3. `float` everywhere. `Math.random()` → `rnd()`, `rand()` stays `rand()`, `R(a,b)`, `RR(a,b)`, `pick`, `pickR`. `Math.round` → `round()` (G helper), `Math.floor` → `floor()` returns int.
   JS arrays `[x,z]` → `float[]` (`v2(x,z)`, `v3(x,y,z)` helpers). `resolve(p,r)` → `resolve(ref x, ref z, r)`.
4. Colours: JS hex / `new THREE.Color` are LINEAR values. Use `C(0xffa448)`, `C("#8f9892")`, plain `Color` math (`Color.Lerp`, `*`). Never call `.linear/.gamma` yourself.
5. No scene / prefab authoring. Everything is created from code at runtime. Do not run Unity (only the lead does). Do not edit files you do not own.
6. Optional JS options → the option classes in `Types.cs` (`SO`, `LO`, `W`) or C# optional parameters. `flags.x` → `F("x")` / `setF("x")` / `clrF("x")` (Game.cs).
   `setTimeout(fn, ms)` → `later(sec, fn)` (Game.cs).
7. When JS code is quality dependent use the HIGH path (`LOW=false`, `LITE=false`, `DENS=1`); the statics exist in Render.cs if you want to keep the expression.

## Files and owners
| File | Owner | JS lines |
|---|---|---|
| `Types.cs`, `Core.cs`, `Render.cs`, `Boot.cs`, Editor setup | lead | 260–385, 387–462, 491–534, 1700–1705 |
| `Assets.cs` + `tools/convert_assets.py` + `Editor/QFImporter.cs` + `ModelDB.cs` | assets fork | 464–489 |
| `Shaders/*.shader` | shader fork | 432–460, 1120–1136, 1175–1185 |
| `Things.cs`, `TextTex.cs`, `Halls.cs` | things fork | 536–608, 636–768 |
| `World.cs` | world fork | 770–1174 |
| `Audio.cs` (`public static class A`) | audio fork | 1187–1253 |
| `Voices.cs`, `Vision.cs` | vision fork | 1255–1565 |
| `Game.cs` | game fork | 1567–1604 (state, reset, messages), 1639–1846 minus HUD, 1901–1921 |
| `UI.cs` (`public static class UI`) | ui fork | HTML/CSS 1–250, 1606–1637 (input), 1848–1899 (HUD, map, credits) |

## Render API (Render.cs / Core.cs — already written, read them)
- `Obj` (three Object3D): `setPos/setRot(rx,ry,rz YXZ)/rotY/rotX/rotZ/posY/setScale/mulScale/visible/add(child)/mat/setMatAll/setShadows`, fields `px,py,pz,rx,ry,rz`.
  `mkGroup()`, `mkMesh(mesh, mat, cast, recv)`, `mkSprite(glowMat)`.
- Geometry: `MB` (`quad/box/cyl/tri`, `.pos/.nor/.uv` lists, `.geo(flipV, smoothN)`), `boxGeo(sx,sy,sz,uvUnit)`, `planeGeo(w,h,u0,u1)`, `sphereGeo`, `cylGeo`, `torusGeo`, `latheGeo`,
  `meshFromArrays(pos, nor|null, uv, tris|null, colors)` for big displaced grids (three-space arrays, three winding).
  `geo(flipV:true)` whenever UVs are image-space (v down) or the texture was `flipY=false` in JS (fern, grass atlases).
- Materials (all colours linear): `stdMat(map, normalMap, W, doubleSided, roughness, metalness)` (= MeshStandardMaterial + weatherize),
  `litMat(color, roughness, metalness, map, emissive, emissiveIntensity, doubleSided)`, `foliageMat(map, alphaTest, color, swayAmp, swayByY, gain, roughness)` (= alphaTest material + swayMat),
  `basicMat(color, fog, map, transparent, opacity, doubleSided, depthWrite, additive)` (= MeshBasicMaterial), `glowMat(color, opacity, fog, map, additive)` (= SpriteMaterial additive glow, for `mkSprite`).
  Mutators: `setOpacity(mat,a)`, `getOpacity`, `setColor(mat,c)`, `setRGB(mat,r,g,b)`, `getColor`.
- Instancing: `SC.add(key, new ScDef{geo,mat,cell,cast,recv,r,yc,k,far}, mtx(...))`, `SC.build()`. `mtx(x,y,z,ry,s,rx,rz,sy)` returns a three-space `Matrix4x4`; PH/model matrices are identity (baked), so drop `.multiply(g.matrix)`.
- Culling: `cullAdd(obj,x,y,z,r,k,far,fr)`, `cullPass()`.
- Globals: `camera` (`CamW`: `px,py,pz,rx,ry,rz`, `setPos`, `setRot`, `apply()`, `fov`, `aspect`, `project(x,y,z)` → NDC with z=0 inside near/far else 2, `dir()`),
  `torch`, `lampA`, `lampB` (`PLight`: `setPos`, `color`, `setRGB`, `distance`, `intensity`), `hemi` (`color`, `groundColor`, `intensity`),
  `fogC` (Color, = scene.fog.color), `fogDensity` (= scene.fog.density), `rain` (`RainFx`: `N`, `count`, `opacity`, `visible`, `color`, `step(dt)`),
  `moon` (`MoonFx`: `opacity`, `color`, `setPos`), `uTime`, `uWet`, `glowTex`, `FONT`, `T["floor_d"]`, `MODELS["arhat"]`, `PH["fir.trunk_a"]`, `CREDITS`.
- JS `renderer.shadowMap.*`, `renderer.compile`, `frustumCulled`, `matrixAutoUpdate`, `toneMapped`, `anisotropy` → drop.

## Cross-module API (each owner implements; everyone else may call)
**Things.cs**: `LAMPS`, `addLamp(x,y,z,LO)`, `setLamp(L,on)`, `STATUES`, eye materials `eyeMatBlack/eyeMatNight/eyeMatHot` (basicMat) and `eyeSprNight/eyeSprHot` (glowMat), `tearMat`, `paleTearMat`,
`modelMat(name,part,SO)`, `buildModel(name,SO)` → `Obj`, `addEyes(EyeHolder st, ModelInfo M, SO o)`, `setHot(EyeHolder,bool)`, `addTears(Statue,ModelInfo,SO)`, `placeStatue(name,x,z,SO)` → `Statue`, `placeInst(name,x,z,SO)`,
`STELES`, `makeStele(x,z,yaw,text)`, world state lists `LANTERNS, CHASERS, PICKUPS, TEMPLES, LANDMARKS, HEADTOPS, DEADB, HALLS, INTERIORS, paths`, singletons `m1, m2, KOFUN, GATEOBJ, FOURF, DAIBUTSU, WELL, BLEEDER, PHANTOM, NICHEPOS, exitGlow, ghost, groundMat`,
`zoneOfZ(z)`, `ZR` (`Dictionary<string,float[]>`), `pathDist`, `addPath`, `wiggle`, `pointAlong`, `nearestOn`, `inPad`, `inMazeBand`, `findSpot(xr,zr,r,FO)` → `float[]`|null, `faceTo`, `makeLantern`, `setLantern`, `CK`, `makeChaser(kind,x,z,CO)`, `makePickup(type,x,y,z,onTake,hall)`.
`class GateObj { Obj g; Obj[] doors; Obj[,] locks /*[door,k]*/; ColRect col; float open; bool opening; float y0; }`, `class GhostObj { Obj root; bool active; float cool, life, flashT; }`,
`class PhantomObj { Obj root, arm; Material mat; bool active; float cool,x,z,life,callT,warnT,fade,gone,dx,dz; int hops, calls; }`.
**TextTex.cs**: `plaqueTex(text, vertical)`, `steleTex(text)`, `ofudaTex()` → `Texture2D` (rasterise CJK text with `G.FONT` into a texture).
**Halls.cs**: `class Hall : Interior` with `def,g,w,d,rot,baseY,P,wallH,ceil,lamps,ridgeRise,ridgeRl,ridgeSwap,bud,sleep`, methods `W(lx,lz)` → `float[]{x,z}`, `rectW(lx0,lx1,lz0,lz1)` → `float[4]`, `yaw(ly)`, `statue(name,lx,lz,SO)`, `lamp(lx,ly,lz,LO)`, `box(lx,lz,sx,sy,sz,matKey,col)`, `ofuda(n)`;
`hipRoof(mb,ex,ez,y0,rise,lift,tile)` → `(float rl, bool swap)`, `HM` / `hallMats()` → `Dictionary<string,Material>`, `buildHall(HallDef)` → `Hall`.
**World.cs**: `buildWorld()` (ends with `SC.build(); visInit();`).
**Audio.cs** `public static class A`: `ok`, `init()`, `resume()`, `suspend()`, every JS method with the same name/args (`step, bell, knock, thunder, stinger, scream, whisper, heart, ignite, chime, breath, hum, stepsBehind, rumble, clip(name, vol, pan, lp, delay, rate)` → duration, `loadClips()`),
continuous layer levels as settable properties with smoothing: `A.rainG, A.rainLP, A.rainLowG, A.windG, A.leafG, A.droneG, A.droneDis, A.chantG, A.grindG, A.grindPan, A.chantClipG` via `A.set(ref/handle, value, tc)` (owner defines the handle type `A.Param`).
**Voices.cs**: `PNAME`, `DUR`, `LINE`, `HL`, `voicePos(x,z)` → struct `{pan,vol,lp}`, `say(Vector2? src, string[] parts, float vol=1, bool female=false, float delay=0)` → float (src = (x, z)), `talk()`, `nearestGiant`, `behindPoint(d)` → `Vector2(x,z)`, `buddhaSrc()` → `Vector2`, `flashGhost`, `phantomHide`, `updatePhantom`, `updateHalluc(dt)`, `resetHalluc()`.
**Vision.cs**: `VIS` (static class or object with the JS fields: `hold, ep, face, roof, dark, red, chant, cool, sway...`), `visInit()`, `visStop()`, `resetVision()`, `updateVision(dt)`, `visSan`, `HFOV()`. `VIS.ep == null` / `VIS.face == null` are tested by other modules.
**Game.cs**: `player`, `state` (string: loading/title/playing/paused/map/dying/dead/won), `hour, day, night, finalEvent, wasNight, shake, stingerCD, indoorF, fogRange, wet, lastZone, lastInside`, `weather`, `lightning`, `WX`,
`F/setF/clrF`, `later(sec, Action)`, `flashOn()`, `interiorAt(x,z)`, `resetGame()`, `showMsg(text, dur=4.2f, cls="")`, `updateMsg`, `msgCur/msgQ` (UI reads `msgText`, `msgCls`, `msgOn`), `updateInv()`, `lookBy`, `toggleFlash`, `pause`, `resume`, `startGame`, `die`, `sendHome`, `respawn`, `win`, `useCharm`,
`updateEnv`, `updatePlayer`, `inView`, `inTorch`, `isSeen`, `updateChasers`, `updateStatues`, `updateGhost`, `updatePickups`, `updateGate`, `updateEvents`, `updateDying`, `frame(dt)`, `boot()` (JS boot minus asset loading: buildWorld, resetGame, state="title").
**UI.cs** `public static class UI`: `init()` (build the whole uGUI canvas), `update(dt)` (poll keyboard/mouse/touch, call `G.lookBy/toggleFlash/pause/openMap…`, refresh HUD = JS updateHUD + drawCompass), `show(id, on)` for `title, pauseOv, mapOv, credOv, dead, win, hud, touch`,
`setFx(id, opacity)` for `vignette, danger, grain, flashfx, blink, redfx, blackfx`, `setDeadSub(text)`, `setWinStats(time, nights, deaths)`, `openMap()/closeMap()/drawMap()`, input state `UI.key(code)` (JS `keys.KeyW` → `UI.key("KeyW")`), `UI.joy` (`Joy`), `UI.touchRun`, `UI.usingTouch`, `UI.releaseKeys()`, `UI.lockCursor(bool)`, `UI.setFilter(saturation, contrast)`, `UI.setLoad(fraction, text, ready)`, `UI.nameValue`.
