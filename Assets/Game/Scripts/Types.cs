// Shared data types for the port of 霧鎖千佛山 (index.html -> Unity).
// All positions / angles stored in these classes are in the ORIGINAL three.js coordinate space
// (x east, y up, +z south; player forward = (-sin yaw, -cos yaw)). Only Obj / MB / Scatter convert to Unity space.
using System;
using System.Collections.Generic;
using UnityEngine;

public class Pad { public float x, z, hx, hz, m, h; public Func<float, float, float> bump; public HallDef def; public bool kofun, gate, niche; public DeadDef dead; }
public class DeadDef { public string id, model; public float x, z, H; }
public class FloorRect { public float minx, maxx, minz, maxz; public Func<float, float, float> fn; }
public class ColCircle { public float x, z, r; public bool occ; public int s; public bool off; }
public class ColRect { public float minx, maxx, minz, maxz; public bool occ; public int s; public bool off; }

/// <summary>Temple definition (DEF.* in buildWorld). 0 means "not set" for ov/pr/backDoor/rise/doorW/doorH/wallH; lift is nullable.</summary>
public class HallDef { public string id, name = ""; public float x, z, w, d, rot, wallH, doorW, doorH, rise, ov, pr, backDoor; public float? lift; public float[][] ipil; }

public class EyesInfo { public float[][] eyes; public float[][][] tears; public float r; public float faceYaw; }
public class ModelPart { public Mesh geo; public Texture2D map, normalMap; public bool unlit; public Dictionary<string, Material> mats = new Dictionary<string, Material>(); }
/// <summary>MODELS[name]: parts + manifest data (d = [width, depth, height] of the unit-height model, t = triangle count, eyes may be null).</summary>
public class ModelInfo { public string name; public List<ModelPart> parts = new List<ModelPart>(); public float[] d; public int t; public EyesInfo eyes; }
/// <summary>PH["file.node"] (Poly Haven pieces). The node matrix is already baked into the mesh, so there is no .matrix.</summary>
public class PHPart { public Mesh geo; }

/// <summary>Weathering parameters (the JS `w` object passed to stdMat / weatherize).</summary>
public class W
{
    public float moss = 0.4f, gray = 0f, amb = 1f, t2 = 0f;
    public float[] tint = { 1, 1, 1 }, tint2 = { 1, 1, 1 }, t2y = { 0, 1 };
    public W clone() { var c = (W)MemberwiseClone(); c.tint = (float[])tint.Clone(); c.tint2 = (float[])tint2.Clone(); c.t2y = (float[])t2y.Clone(); return c; }
}

/// <summary>Options for placeStatue / placeInst / buildModel / modelMat (the JS `o` literal). Use object initialisers: new SO{h=3.1f, dy=0.9f, col=0}.</summary>
public class SO
{
    public float s, h, w;                 // scale aliases: s || h || w || 1
    public float? y;                      // explicit base height (else groundH)
    public float sink, pitch, yaw, roll, dy;
    public bool track; public float turn; // turn==0 -> default 0.7
    public string msg; public float msgR; // msgR==0 -> default
    public bool hot, indoor;
    public bool eyes = true;              // eyes:false disables eye meshes
    public bool tears, strands; public Material tearMat; public float tearW, eyeScale; // 0 -> defaults
    public float? col;                    // collision radius factor; col==0 -> no collider; null -> default
    public float k;                       // cull factor; 0 -> default
    public float? moss; public float gray; public float[] tint; public string tintKey;
    public W wx;                          // extra weather overrides (JS o.w when it is an object)
    public bool cast = true;
    public float cell, far;               // placeInst only
    public SO clone() { return (SO)MemberwiseClone(); }
}

/// <summary>Lamp options (addLamp / H.lamp). null -> default.</summary>
public class LO { public int? color; public float? size, power, range; public bool? indoor; }

public class Lamp { public float x, y, z; public bool lit; public Obj sp; public Material spMat; public float power = 1, range = 17, fl; public Color color; public bool indoor; public float baseSize = 3.6f; public Light light; }

/// <summary>Anything with glowing eyes: statues, chasers, vision statues.</summary>
public class EyeHolder { public Obj g; public List<Obj> eyes, sprs; public float eyeY; public bool hot; }

public class Statue : EyeHolder
{
    public string name, msg;
    public float x, z, y, s, yaw, turn, msgR, height, faceYaw, curYaw, cool;
    public int spin;
    public bool track, msgDone, giant, indoor, dead;
    public bool hot0, hot0Set;
    public Obj tears; public ColCircle col;
}

public class Chaser : EyeHolder
{
    public string kind, zone;
    public float x, z, hx, hz, hyaw, moved, stuck, sideT, h, r, faceYaw, headY;
    public int side = 1;
    public Maze maze; public Interior hall;
    public bool dormant, dormant0, dormant0Set, seen, vis = true;
}

/// <summary>Statues used by the hallucination system (VIS.pool / VIS.heads).</summary>
public class VisSt : EyeHolder { public string name; public ModelInfo M; public bool stand, on; public float faceYaw, wr, x, y, z, h = 1; }

/// <summary>INTERIORS entry. Hall (Halls.cs) and Kofun derive from this. `isHall` replaces the JS check `h.W` (true only for real halls).</summary>
public class Interior { public string id, name = ""; public float x, z, minx, maxx, minz, maxz, floorY; public bool isHall; }
public class Kofun : Interior { public Func<float, float> ceilAt; public float entX; public Chaser asc; }

public class Lantern { public float x, z, y; public bool lit; public Lamp L; public Obj core; }
public class Pickup { public string type; public float x, y, z; public Obj g; public bool taken; public Action onTake; public Interior hall; public int id; }
public class Temple { public Interior H; public bool key; public string label; }
public class PathObj { public List<float[]> pts; public float[] bb; }
public class Landmark { public float x, z; public string name; }
public class HeadTop { public float x, z, y, r; }
public class Stele { public float x, z; public string text; public bool read; }

public class Player
{
    public float x, z, y, yaw, pitch, vx, vz;
    public float stamina = 100, battery = 100, sanity = 100;
    public bool flash, safe = true, exhausted;
    public float bob, bobAmp, stepAcc;
    public int deaths, nights, keys, charms;
    public float elapsed;
    public string zone = "forest";
    public Interior inside; public Lantern lastLantern;
    public float danger, nearGiant = 1e9f, eye = 1.62f, grace;
}

public class Weather { public string name = "mist"; public float rain, fog = 0.03f, timer = 50; }
public class Lightning { public float t = 9, flash, next = 8, thunderIn = -1, dist; }
public class WXDef { public float rain, fog; public string label; }
public class Joy { public float x, y; public int id = -1; public float ox, oy; }
