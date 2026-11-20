// World.cs — port of buildWorld() (index.html lines 770-1174).
// Everything is in three.js coordinates. The ORDER of rand()/R() calls follows the JS exactly so a seed always gives the same mountain.
using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class G
{
    public static void buildWorld() { WB.run(); }

    static class WB
    {
        const float KX = 108f, KZ = -54f, KS = 1.3f;                   // 石室
        const float NCx = -136f, NCz = -60f, NCL = 46f, NCH = 15f;     // 千佛崖 (JS NC)
        static readonly int[] SG = { -1, 1 };
        static readonly string[] DEFK = { "t1", "t2", "hut", "t3", "t4", "t6" };

        static float ent1X, exit1X, ent2X, exit2X;
        static List<int[]> sol1, sol2;
        static Dictionary<string, HallDef> DEF;
        static DeadDef[] DB;
        static PathObj mainA, mainB1, mainB2, mainC;
        static float[] pre3, post3, kEnt, t1d, t2d, t4d, t6d, zrF, zrB, zrC;
        static Dictionary<string, Hall> Hh;
        static Material[] cliffM; static PHPart[] cliffG;

        // ---------- small helpers ----------
        static Texture2D Tx(string n) { Texture2D t; T.TryGetValue(n, out t); return t; }
        static PHPart Ph(string n) { PHPart p; PH.TryGetValue(n, out p); return p; }
        static ScDef D(Mesh geo, Material mat, float cell = 40, bool cast = false, bool recv = false, float r = 3, float yc = 0, float k = 1, float far = 0)
        { return new ScDef { geo = geo, mat = mat, cell = cell, cast = cast, recv = recv, r = r, yc = yc, k = k, far = far }; }
        static bool nearLantern(float x, float z, float d) { for (int i = 0; i < LANTERNS.Count; i++) { var L = LANTERNS[i]; if (hypot(L.x - x, L.z - z) < d) return true; } return false; }
        /// <summary>JS `o.yaw||R(0,TAU)` inside makeChaser: a yaw of exactly 0 counts as "not given".</summary>
        static float? yawOrNull(float v) { return v == 0f ? (float?)null : v; }
        /// <summary>JS: g.traverse(o=>{ if(o.isMesh) o.material.side = THREE.DoubleSide; })</summary>
        static void doubleSide(Obj g)
        {
            var cache = new Dictionary<Material, Material>();
            foreach (var r in g.go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m0 = r.sharedMaterial; if (m0 == null) continue; Material c;
                if (!cache.TryGetValue(m0, out c)) { c = new Material(m0); c.SetFloat("_Cull", 0); cache[m0] = c; }
                r.sharedMaterial = c;
            }
        }
        static float[] door(HallDef d, float extra = 0f)
        {
            float lz = d.d / 2 + 1.3f + 1.6f + (extra != 0f ? extra : 0.6f);
            return v2(d.x + lz * sin(d.rot), d.z + lz * cos(d.rot));
        }

        public static void run()
        {
            PADS.Clear(); FLOORS.Clear(); clearCollision();
            defs();
            pathsBuild();
            halls();
            maze1(); maze2();
            borders();
            lanterns();
            setPieces();
            deadBuddhas();
            nicheCliff();
            guardiansAndSteles();
            mazeDeadEnds();
            rows();
            chasers();
            trees();
            undergrowth();
            ground();
            gate();
            ghostAndPhantom();
            SC.build();
            visInit();
        }

        // ================= temple definitions & pads =================
        static void defs()
        {
            NOX = R(0, 400); NOZ = R(0, 400);
            m1 = new Maze(M1C); m2 = new Maze(M2C);
            ent1X = m1.x0 + (m1.entC + 0.5f) * m1.cell; exit1X = m1.x0 + (m1.exitC + 0.5f) * m1.cell;
            ent2X = m2.x0 + (m2.entC + 0.5f) * m2.cell; exit2X = m2.x0 + (m2.exitC + 0.5f) * m2.cell;
            sol1 = m1.path(m1.entC, 0, m1.exitC, m1.rows - 1); sol2 = m2.path(m2.entC, 0, m2.exitC, m2.rows - 1);
            DEF = new Dictionary<string, HallDef>();
            DEF["t1"] = new HallDef { id = "t1", name = "地藏堂", x = -48, z = 266, w = 9, d = 7, rot = HP, wallH = 3.4f, doorW = 6.2f, doorH = 2.8f, rise = 3.2f };
            DEF["t2"] = new HallDef { id = "t2", name = "本堂", x = 88, z = 214, w = 15, d = 12, rot = -HP, wallH = 4.8f, doorW = 3.2f, doorH = 3.2f, rise = 5.4f };
            DEF["hut"] = new HallDef { id = "hut", name = "", x = -100, z = 198, w = 4.6f, d = 4.7f, rot = HP, wallH = 2.95f, doorW = 1.7f, doorH = 2.1f, rise = 2.2f, ov = 0.9f, lift = 0.2f, pr = 0.12f };
            DEF["t3"] = new HallDef
            {
                id = "t3", name = "大佛殿", x = 0, z = -18, w = 28, d = 24, rot = 0, wallH = 13, doorW = 5.4f, doorH = 6.6f, backDoor = 2.6f, pr = 0.5f, rise = 9, ov = 2.6f, lift = 1.4f,
                ipil = new[] { new[] { -9.6f, -8f }, new[] { 9.6f, -8f }, new[] { -9.6f, -2.5f }, new[] { 9.6f, -2.5f }, new[] { -9.6f, 3f }, new[] { 9.6f, 3f }, new[] { -9.6f, 8.5f }, new[] { 9.6f, 8.5f } }
            };
            DEF["t4"] = new HallDef { id = "t4", name = "千手殿", x = -100, z = 24, w = 13, d = 17, rot = HP, wallH = 6.4f, doorW = 2.9f, doorH = 3.3f, rise = 5.6f };
            DEF["t6"] = new HallDef { id = "t6", name = "閻魔殿", x = -80, z = -234, w = 15, d = 13, rot = HP, wallH = 5.8f, doorW = 3.1f, doorH = 3.3f, rise = 5.6f };

            foreach (var key in DEFK)
            {
                var d = DEF[key]; bool sw = abs(sin(d.rot)) > 0.5f; float hx = (sw ? d.d : d.w) / 2 + 4.5f, hz = (sw ? d.w : d.d) / 2 + 4.5f;
                PADS.Add(new Pad { x = d.x, z = d.z, hx = hx, hz = hz, m = 9, h = 0, def = d });
            }
            {
                float eX = KX - 8.3f * KS;
                PADS.Add(new Pad { x = KX, z = KZ, hx = 13.5f, hz = 8, m = 8, h = 0, kofun = true, bump = (bx, bz) => 4.9f * smooth(eX - 0.4f, eX + 3.6f, bx) * smooth(2.0f, 3.8f, abs(bz - KZ)) });
            }
            var gatePad = new Pad { x = 0, z = -314, hx = 9, hz = 13, m = 8, h = 0, gate = true }; PADS.Add(gatePad);
            foreach (var p in PADS) p.h = baseH(p.x, p.z);
            gatePad.bump = (bx, bz) => smooth(-299, -321, bz) * 2.6f;
            DB = new[] { new DeadDef { id = "a", model = "head_b", x = 126, z = 276, H = 25 }, new DeadDef { id = "b", model = "head_a", x = 126, z = -212, H = 22 } };
            foreach (var d0 in DB)
            {
                var d = d0;
                PADS.Add(new Pad { x = d.x - 6, z = d.z, hx = 9, hz = 10, m = 26, h = baseH(d.x - 6, d.z), dead = d, bump = (bx, bz) => d.H * 0.95f * smooth(d.x - 1, d.x + 9, bx) * smooth(27, 9, abs(bz - d.z)) });
            }
            PADS.Add(new Pad { x = NCx + 8, z = NCz, hx = 8, hz = NCL / 2 + 2, m = 20, h = baseH(NCx + 8, NCz), niche = true, bump = (bx, bz) => smooth(NCx - 4, NCx - 7.5f, bx) * (NCH + 1.5f) });
        }

        // ================= paths =================
        static PathObj branch(PathObj main, HallDef d, float extra = 0f) { var dp = door(d, extra); var a = nearestOn(main.pts, dp[0], dp[1]); return addPath(wiggle(a, dp, 8, 3)); }
        static PathObj spur(PathObj main, float tx, float tz) { return addPath(wiggle(nearestOn(main.pts, tx, tz), v2(tx, tz), 9, 4)); }
        static void pathsBuild()
        {
            mainA = addPath(wiggle(v2(START.x, START.y + 4), v2(ent1X, m1.zS + 1), 22, 13));
            pre3 = door(DEF["t3"], 1.5f); post3 = v2(0, DEF["t3"].z - DEF["t3"].d / 2 - 4.2f);
            mainB1 = addPath(wiggle(v2(exit1X, m1.zN - 1), pre3, 14, 8));
            mainB2 = addPath(wiggle(post3, v2(ent2X, m2.zS + 1), 14, 8));
            var mc1 = wiggle(v2(exit2X, m2.zN - 1), v2(0, -258), 12, 7); mc1.Add(v2(0, -300)); mc1.Add(v2(0, -323)); mainC = addPath(mc1);
            branch(mainA, DEF["t1"]); branch(mainA, DEF["t2"]); branch(mainA, DEF["hut"], 0.2f); branch(mainB1, DEF["t4"]); branch(mainC, DEF["t6"]);
            kEnt = v2(KX - 10.6f * KS - 2.5f, KZ);
            {
                var cat = new List<float[]>(mainB1.pts); cat.AddRange(mainB2.pts);
                addPath(wiggle(nearestOn(cat, kEnt[0], kEnt[1]), kEnt, 10, 5));
            }
            spur(mainA, DB[0].x - 16, DB[0].z); spur(mainC, DB[1].x - 16, DB[1].z); spur(mainB2, NCx + 15, NCz);
            addPath(new List<float[]> { v2(-13, pre3[1] + 1), v2(13, pre3[1] + 1) });
            addPath(new List<float[]> { v2(14.6f, pre3[1]), v2(16.5f, -18), v2(14.6f, post3[1]) });
            addPath(new List<float[]> { v2(-14.6f, pre3[1]), v2(-16.5f, -18), v2(-14.6f, post3[1]) });
        }

        // ================= halls =================
        static void onitileOn(Hall H)
        {
            foreach (int sg in SG)
            {
                float lx = H.ridgeSwap ? 0 : sg * (H.ridgeRl + 0.25f), lz = H.ridgeSwap ? sg * (H.ridgeRl + 0.25f) : 0; var p = H.W(lx, lz);
                placeStatue("onitile", p[0], p[1], new SO { w = min(1.7f, H.w * 0.12f + 0.5f), y = H.baseY + H.ridgeRise + 0.15f, yaw = H.rot + (H.ridgeSwap ? (sg > 0 ? 0 : PI) : sg * HP), col = 0, moss = 0.4f, k = 1.3f });
            }
        }
        static Obj table(Hall H, float lx, float lz, float sx, float sz, float h = 0.8f) { return H.box(lx, lz, sx, h, sz, "pilIn"); }

        static void halls()
        {
            Hh = new Dictionary<string, Hall>(); foreach (var key in DEFK) Hh[key] = buildHall(DEF[key]);
            foreach (var key in new[] { "t1", "t2", "t3", "t4", "t6" }) onitileOn(Hh[key]);
            hallT1(); hallT2(); hallHut(); hallT3(); hallT4(); hallT6(); kofun();
        }

        // T1 地藏堂
        static void hallT1()
        {
            var H = Hh["t1"]; H.statue("jizocluster", 0, -1.25f, new SO { w = 3.5f, col = 0.36f });
            foreach (int sg in SG) foreach (float lz in new[] { -1.6f, 0.4f, 2.2f }) H.statue("jizostele", sg * 3.75f, lz, new SO { h = 1.15f, yaw = -sg * HP, col = 0.3f });
            H.lamp(-2.3f, 1.0f, 1.4f); H.lamp(2.3f, 1.0f, 1.4f); table(H, -2.3f, 1.4f, 0.4f, 0.4f, 0.9f); table(H, 2.3f, 1.4f, 0.4f, 0.4f, 0.9f);
            TEMPLES.Add(new Temple { H = H, key = false });
        }

        // T2 本堂
        static void hallT2()
        {
            var H = Hh["t2"]; table(H, 0, -4.3f, 5.4f, 2.4f, 0.9f);
            var bud = H.statue("bluebuddha", 0, -4.4f, new SO { h = 3.1f, dy = 0.9f, col = 0, track = false }); H.bud = bud;
            var spots = new List<float[]>(); foreach (int sg in SG) foreach (float lz in new[] { -3.2f, -0.6f, 2f, 4.4f }) spots.Add(new[] { sg * 5.7f, lz, -sg * HP });
            H.sleep = new List<Chaser>();
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i]; var p = H.W(s[0], s[1]);
                if (i == 0 || i == 5 || i == 2) H.sleep.Add(makeChaser("monk", p[0], p[1], new CO { hall = H, zone = "t2", dormant = true, yaw = yawOrNull(H.rot + s[2]) }));
                else H.statue("monk", s[0], s[1], new SO { h = 1.25f, yaw = s[2], col = 0.42f });
            }
            table(H, 0, -2.1f, 1.7f, 0.8f, 0.8f); H.lamp(-1.7f, 1.25f, -2.5f); H.lamp(1.7f, 1.25f, -2.5f); table(H, -1.7f, -2.5f, 0.3f, 0.3f, 1.0f); table(H, 1.7f, -2.5f, 0.3f, 0.3f, 1.0f); H.ofuda(46);
            var kp = H.W(0, -2.1f);
            makePickup("key", kp[0], H.floorY + 1.3f, kp[1], () =>
            {
                foreach (var L in H.lamps) setLamp(L, false);
                foreach (var c in H.sleep) { c.dormant = false; setHot(c, true); }
                setHot(bud, true); A.stinger(); shake = 0.6f;
                showMsg("燭火全滅了。兩側坐著的石僧，少了三尊。", 5, "warn");
            }, H);
            var kp2 = H.W(0, H.d / 2 + 6.5f); placeStatue("komainu", kp2[0], kp2[1], new SO { w = 4.6f, yaw = H.rot, col = 0, moss = 0.5f });
            foreach (int sg in SG) { var q = H.W(sg * 1.75f, H.d / 2 + 6.5f); addCircle(q[0], q[1], 0.75f); }
            TEMPLES.Add(new Temple { H = H, key = true });
        }

        // 廢屋
        static void hallHut()
        {
            var H = Hh["hut"];
            var g = buildModel("altarroom", new SO { indoor = true, moss = 0, cast = false }); g.setPos(H.x, H.floorY - 0.13f, H.z); g.rotY = H.rot + PI; cullAdd(g, H.x, H.floorY, H.z, 5, 1);
            doubleSide(g);
            var r = H.rectW(-1.9f, 1.25f, -2.0f, -1.15f); addRect(r[0], r[1], r[2], r[3], false);
            H.statue("monk", 1.15f, 0.9f, new SO { h = 0.95f, yaw = -2.2f, col = 0.42f });
            var cp = H.W(-0.9f, 0.7f); makePickup("charm", cp[0], H.floorY + 1.05f, cp[1], null, H); TEMPLES.Add(new Temple { H = H, key = false, label = "廢屋" });
        }

        // T3 大佛殿
        static void hallT3()
        {
            var H = Hh["t3"]; table(H, 0, -3.6f, 13, 10.2f, 0.8f);
            DAIBUTSU = H.statue("daibutsu", 0, -3.6f, new SO { h = 10.2f, dy = 0.8f, col = 0, tears = true, moss = 0.2f, k = 1.6f });
            var dr = H.rectW(-6.4f, 6.4f, -8.6f, 1.4f); addRect(dr[0], dr[1], dr[2], dr[3], true);
            table(H, 0, 3.4f, 2.2f, 0.9f, 0.9f);
            H.lamp(-2.6f, 1.5f, 3.6f, new LO { size = 2, power = 0.9f, range = 15 }); H.lamp(2.6f, 1.5f, 3.6f, new LO { size = 2, power = 0.9f, range = 15 });
            H.lamp(-6.2f, 1.5f, 7, new LO { size = 2 }); H.lamp(6.2f, 1.5f, 7, new LO { size = 2 });
            foreach (var p in new[] { new[] { -2.6f, 3.6f }, new[] { 2.6f, 3.6f }, new[] { -6.2f, 7f }, new[] { 6.2f, 7f } }) table(H, p[0], p[1], 0.35f, 0.35f, 1.2f);
            H.sleep = new List<Chaser>();
            foreach (var s in new[] { new[] { -12f, -10f }, new[] { 12f, -10f }, new[] { -12.2f, 9.4f }, new[] { 12.2f, 9.4f } })
            {
                var p = H.W(s[0], s[1]);
                H.sleep.Add(makeChaser("arhat", p[0], p[1], new CO { hall = H, zone = "t3", dormant = true, yaw = yawOrNull(faceTo(p[0], p[1], H.x, H.z)) }));
            }
            foreach (int sg in SG)
            {
                H.statue("monk", sg * 12.4f, -3, new SO { h = 1.3f, yaw = -sg * HP, col = 0.42f });
                H.statue("monk", sg * 12.4f, 3, new SO { h = 1.3f, yaw = -sg * HP, col = 0.42f });
            }
            H.ofuda(40);
            var kp = H.W(0, 3.4f);
            makePickup("key", kp[0], H.floorY + 1.45f, kp[1], () =>
            {
                foreach (var L in H.lamps) setLamp(L, false);
                foreach (var c in H.sleep) { c.dormant = false; setHot(c, true); }
                setHot(DAIBUTSU, true); if (DAIBUTSU.tears != null) DAIBUTSU.tears.visible = true;
                A.bell(0, 1); A.rumble(); shake = 0.9f; showMsg("大佛睜開了眼睛。", 4, "warn"); showMsg("祂在流淚——黑紅色的。四個角落的石僧，動了。", 5, "warn");
            }, H);
            foreach (int sg in SG)
            {
                var p = H.W(sg * 6.2f, H.d / 2 + 5.2f);
                placeStatue(sg < 0 ? "nio_a" : "nio_b", p[0], p[1], new SO
                {
                    h = 5.2f, yaw = H.rot + sg * -0.25f, track = true, turn = 0.9f, moss = 0.45f, gray = 0.6f, tint = new[] { 0.5f, 0.5f, 0.47f }, tintKey = "d",
                    msg = sg < 0 ? "兩尊仁王守著大佛殿。你一轉頭，祂們就換了方向。" : null, msgR = 22
                });
            }
            TEMPLES.Add(new Temple { H = H, key = true });
        }

        // T4 千手殿
        static void hallT4()
        {
            var H = Hh["t4"]; table(H, 0, -6.4f, 4.4f, 1.8f, 0.7f); H.statue("avalokita", 0, -6.5f, new SO { h = 5.0f, dy = 0.7f, col = 0, moss = 0.05f });
            foreach (int sg in SG) foreach (float lz in new[] { -4.5f, -1.2f, 2.1f, 5.4f })
                {
                    var p = H.W(sg * (H.w / 2 - 0.34f), lz);
                    placeStatue("hannya", p[0], p[1], new SO { h = 0.85f, y = H.floorY + 2.3f, yaw = H.rot - sg * HP, col = 0, indoor = true, hot = true, moss = 0, eyeScale = 0.8f, k = 1 });
                }
            foreach (var s in new[] { new[] { -4f, 3.6f }, new[] { 4.2f, 2.4f }, new[] { -4.4f, -3f }, new[] { 4f, -3.6f } })
            {
                var p = H.W(s[0], s[1]); makeChaser("arhat", p[0], p[1], new CO { hall = H, zone = "t4", yaw = yawOrNull(R(0, TAU)) });
            }
            table(H, 0, -4.7f, 1.2f, 0.7f, 0.8f); H.lamp(-1.4f, 1.2f, -4.9f, new LO { color = 0xff7040 }); H.lamp(1.4f, 1.2f, -4.9f, new LO { color = 0xff7040 }); H.ofuda(30);
            var cp = H.W(0, -4.7f); makePickup("charm", cp[0], H.floorY + 1.25f, cp[1], null, H); TEMPLES.Add(new Temple { H = H, key = false });
        }

        // T6 閻魔殿
        static void hallT6()
        {
            var H = Hh["t6"]; table(H, 0, -3.7f, 4.6f, 3.4f, 1.1f);
            FOURF = H.statue("fourfaces", 0, -3.7f, new SO { h = 3.3f, dy = 1.1f, col = 0, moss = 0.1f, gray = 0.3f, tint = new[] { 0.62f, 0.6f, 0.56f }, tintKey = "f", eyeScale = 1.1f });
            if (FOURF != null) { FOURF.spin = 0; FOURF.cool = 0; }
            foreach (int sg in SG)
            {
                H.statue("sekijin", sg * 4.9f, -5, new SO { h = 2.7f, yaw = -sg * 0.4f, col = 0.3f });
                H.statue("fudostele", sg * 6.6f, -0.4f, new SO { h = 2.3f, yaw = -sg * HP, col = 0.3f });
            }
            H.statue("armless", -5, 3.8f, new SO { h = 2.7f, yaw = HP * 1.2f, col = 0.38f });
            H.sleep = new List<Chaser>();
            foreach (var s in new[] { new[] { 5.6f, 4.6f }, new[] { 2.2f, 5.2f }, new[] { 6.2f, 1.8f } })
            {
                var p = H.W(s[0], s[1]);
                H.sleep.Add(makeChaser("arhat", p[0], p[1], new CO { hall = H, zone = "t6", dormant = true, yaw = yawOrNull(faceTo(p[0], p[1], H.x, H.z)) }));
            }
            table(H, 0, -1.2f, 1.5f, 0.7f, 0.8f); H.lamp(-1.6f, 1.25f, -1.3f, new LO { color = 0xff4a28 }); H.lamp(1.6f, 1.25f, -1.3f, new LO { color = 0xff4a28 }); H.ofuda(70);
            var kp = H.W(0, -1.2f);
            makePickup("key", kp[0], H.floorY + 1.3f, kp[1], () =>
            {
                foreach (var L in H.lamps) setLamp(L, false);
                foreach (var c in H.sleep) { c.dormant = false; setHot(c, true); }
                setHot(FOURF, true); A.stinger(); A.rumble(); shake = 0.7f;
                showMsg("四張臉同時笑了。", 4, "warn");
            }, H);
            TEMPLES.Add(new Temple { H = H, key = true });
        }

        // 石室
        static void kofun()
        {
            var pad = PADS.Find(p => p.kofun); float fy = pad.h + 0.12f;
            var g = buildModel("kofun", new SO { indoor = true, moss = 0.25f, cast = false }); doubleSide(g);
            g.setScale(KS); g.setPos(KX, pad.h - 0.1f, KZ); g.rotY = PI; cullAdd(g, KX, pad.h, KZ, 16, 1.2f);
            float[][] SL =
            {
                new[] { -8.13f, -1.69f, 1.62f, 3.09f }, new[] { -6.1f, -1.67f, 1.73f, 3.04f }, new[] { -4.07f, -1.72f, 1.89f, 3.01f }, new[] { -2.03f, -2.06f, 1.95f, 2.85f }, new[] { 0f, -1.35f, 1.59f, 2.52f },
                new[] { 2.03f, -0.89f, 1.57f, 2.34f }, new[] { 4.07f, -1.37f, 1.24f, 2.26f }, new[] { 6.1f, -1.18f, 0.91f, 2.34f }, new[] { 8.13f, -0.97f, 0.95f, 1.1f }
            };
            foreach (var s in SL)
            {
                float x0 = KX - (s[0] + 1.02f) * KS, x1 = KX - (s[0] - 1.02f) * KS;
                addRect(x0, x1, KZ + s[2] * KS - 0.35f, KZ + s[2] * KS + 2.5f, true); addRect(x0, x1, KZ + s[1] * KS - 2.5f, KZ + s[1] * KS + 0.35f, true);
            }
            addRect(KX + 7.85f * KS, KX + 7.85f * KS + 2, KZ - 4, KZ + 4, true);
            Func<float, float> ceilAt = wx =>
            {
                float bx = (KX - wx) / KS; int i = 0; while (i < SL.Length - 2 && bx > SL[i + 1][0]) i++;
                float[] a = SL[i], b = SL[i + 1]; float t = clamp((bx - a[0]) / (b[0] - a[0]), 0, 1); return (lerp(a[3], b[3], t) - 0.15f) * KS;
            };
            KOFUN = new Kofun { id = "kofun", name = "石室", x = KX, z = KZ, minx = KX - 8.3f * KS, maxx = KX + 8.2f * KS, minz = KZ - 2.9f, maxz = KZ + 2.9f, floorY = fy, ceilAt = ceilAt, entX = KX - 8.3f * KS };
            INTERIORS.Add(KOFUN);
            FLOORS.Add(new FloorRect { minx = KX - 8.6f * KS, maxx = KX + 8.4f * KS, minz = KZ - 3, maxz = KZ + 3, fn = (fx, fzz) => fy });
            float ax = KX + 6.5f * KS; var asc = makeChaser("ascetic", ax, KZ + 0.1f, new CO { hall = KOFUN, zone = "kofun", dormant = true, yaw = -HP }); asc.g.posY = fy; KOFUN.asc = asc;
            makePickup("charm", KX + 4.4f * KS, fy + 0.95f, KZ, () =>
            {
                asc.dormant = false; A.whisper(0, 0.6f); showMsg("……祂剛才，是低著頭的嗎？", 4.5f, "warn"); showMsg("盯著祂，倒著退出去。", 4, "warn");
            }, KOFUN);
            TEMPLES.Add(new Temple { H = KOFUN, key = false, label = "石室" });
        }

        // ================= mazes =================
        static void maze1()
        {
            var segs = m1.segments(); float Tn = m1.T, H = m1.H, L = m1.cell + Tn;
            Mesh hg = boxGeo(L, H, Tn, 3.2f), vg = boxGeo(Tn, H, L, 3.2f);
            var wmat = stdMat(Tx("wall_d"), Tx("wall_n"), new W { moss = 0.56f, tint = new[] { 0.82f, 0.82f, 0.8f } });
            ScDef dh = D(hg, wmat, 40, true, true, 8, 3), dv = D(vg, wmat, 40, true, true, 8, 3);
            foreach (var s in segs)
            {
                float sy = R(0.9f, 1.14f);
                float gy = min(terrainH(s.x - (s.h ? L / 2 : 0), s.z - (s.h ? 0 : L / 2)), terrainH(s.x + (s.h ? L / 2 : 0), s.z + (s.h ? 0 : L / 2)));
                SC.add(s.h ? "m1h" : "m1v", s.h ? dh : dv, mtx(s.x, gy + H * sy / 2 - 1.3f, s.z, 0, 1, 0, 0, sy));
                if (s.h) addRect(s.x - L / 2, s.x + L / 2, s.z - Tn / 2, s.z + Tn / 2, true); else addRect(s.x - Tn / 2, s.x + Tn / 2, s.z - L / 2, s.z + L / 2, true);
            }
        }

        /// <summary>The canvas-drawn bamboo culm texture (64x256, sRGB).</summary>
        static Texture2D bambooTex()
        {
            const int TW = 64, TH = 256; var px = new Color[TW * TH];   // canvas layout: row 0 = top
            Color c0 = C(0x4d5a26), c1 = C(0x7d8a3c), c2 = C(0x465222);
            for (int y = 0; y < TH; y++) for (int x = 0; x < TW; x++)
                {
                    float t = (x + 0.5f) / TW; px[y * TW + x] = t < 0.5f ? Color.Lerp(c0, c1, t / 0.5f) : Color.Lerp(c1, c2, (t - 0.5f) / 0.5f);
                }
            Action<int, int, int, int, Color, float> fill = (x0, y0, w, h, c, a) =>
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(TH, y0 + h); y++) for (int x = Mathf.Max(0, x0); x < Mathf.Min(TW, x0 + w); x++)
                    {
                        var o = px[y * TW + x]; px[y * TW + x] = new Color(lerp(o.r, c.r, a), lerp(o.g, c.g, a), lerp(o.b, c.b, a), 1);
                    }
            };
            for (int y = 0; y < TH; y += 32) { fill(0, y, TW, 3, new Color(30 / 255f, 34 / 255f, 14 / 255f), 0.75f); fill(0, y + 3, TW, 2, new Color(200 / 255f, 210 / 255f, 150 / 255f), 0.35f); }
            for (int k = 0; k < 60; k++) fill(floor(rnd() * 64), floor(rnd() * 256), 1, floor(20 + rnd() * 60), new Color(40 / 255f, 40 / 255f, 20 / 255f), 0.15f);
            var tex = new Texture2D(TW, TH, TextureFormat.RGBA32, true, false); var flipped = new Color[TW * TH];
            for (int y = 0; y < TH; y++) Array.Copy(px, y * TW, flipped, (TH - 1 - y) * TW, TW);
            tex.SetPixels(flipped); tex.wrapMode = TextureWrapMode.Repeat; tex.anisoLevel = 4; tex.Apply(true); return tex;
        }

        static void maze2()
        {
            var segs = m2.segments(); float Tn = m2.T, L = m2.cell + Tn;
            var ct = bambooTex();
            // CylinderGeometry(0.85, 1, 1, 7, 1, true) translated +0.5 in y; u 0..1 around, v 0..8 (texture repeat (1,8))
            var cb = new MB();
            for (int i = 0; i < 7; i++)
            {
                float a0 = i / 7f * TAU, a1 = (i + 1) / 7f * TAU;
                cb.quad(v3(cos(a1), 0, sin(a1)), v3(cos(a0), 0, sin(a0)), v3(cos(a0) * 0.85f, 1, sin(a0) * 0.85f), v3(cos(a1) * 0.85f, 1, sin(a1) * 0.85f), (i + 1) / 7f, 0, i / 7f, 8);
            }
            var cgeo = cb.geo(false, true);
            var cmat = stdMat(ct, null, new W { moss = 0.12f, gray = 0.25f, tint = new[] { 0.6f, 0.62f, 0.55f } }, false, 0.6f);
            var lb = new MB();
            for (int k = 0; k < 3; k++) { float a = k * PI / 3; float sx = cos(a) * 1.7f, sz = sin(a) * 1.7f; lb.quad(v3(-sx, 0, -sz), v3(sx, 0, sz), v3(sx, 3.4f, sz), v3(-sx, 3.4f, -sz), 0, 0, 1, 1); }
            for (int i = 0; i < lb.nor.Count; i += 3) { lb.nor[i] = 0; lb.nor[i + 1] = 1; lb.nor[i + 2] = 0; }
            var lgeo = lb.geo();
            var lmat = foliageMat(Tx("bamboo"), 0.4f, C(0x6f7d5c), 0.16f, false, 1f, 0.85f);
            ScDef dBam = D(cgeo, cmat, 40, true, false, 6, 5), dLeaf = D(lgeo, lmat, 40, false, false, 6, 8);
            foreach (var s in segs)
            {
                int n = round(L / 0.58f);
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n - 0.5f; float along = t * L + R(-0.15f, 0.15f), across = R(-0.62f, 0.62f); float x = s.h ? s.x + along : s.x + across, z = s.h ? s.z + across : s.z + along;
                    float rr = R(0.055f, 0.105f), hh = R(8.5f, 13.5f);
                    SC.add("bam", dBam, mtx(x, terrainH(x, z) - 0.3f, z, R(0, TAU), rr, R(-0.06f, 0.06f), R(-0.06f, 0.06f), hh));
                }
                for (int k = 0; k < 5; k++)
                {
                    float t = R(-0.48f, 0.48f) * L; float x = s.h ? s.x + t : s.x + R(-0.5f, 0.5f), z = s.h ? s.z + R(-0.5f, 0.5f) : s.z + t;
                    SC.add("baml", dLeaf, mtx(x, terrainH(x, z) + R(5.5f, 10.5f), z, R(0, TAU), R(0.8f, 1.4f)));
                }
                if (s.h) addRect(s.x - L / 2, s.x + L / 2, s.z - Tn / 2, s.z + Tn / 2, true); else addRect(s.x - Tn / 2, s.x + Tn / 2, s.z - L / 2, s.z + L / 2, true);
            }
        }

        // ================= side blockers, world borders, cliffs =================
        static void cliff(float x, float z, float yaw)
        {
            int k = rand() < 0.5f ? 0 : 1; if (cliffG[k] == null) return; float s = k != 0 ? R(4.2f, 5.6f) : R(2.6f, 3.4f);
            SC.add("cliff" + k, D(cliffG[k].geo, cliffM[k], 60, false, true, 12, 4, 1.2f), mtx(x, terrainH(x, z) - R(1, 3), z, yaw + R(-0.3f, 0.3f), s, R(-0.1f, 0.1f), R(-0.1f, 0.1f)));
        }
        static void borders()
        {
            addRect(-XB - 20, -M1C.hw, M1C.zN - 1, M1C.zS + 1, false); addRect(M1C.hw, XB + 20, M1C.zN - 1, M1C.zS + 1, false);
            addRect(-XB - 20, -M2C.hw, M2C.zN - 1, M2C.zS + 1, false); addRect(M2C.hw, XB + 20, M2C.zN - 1, M2C.zS + 1, false);
            addRect(9.8f, XB + 20, GATE.y - 0.6f, GATE.y + 0.6f, false); addRect(-XB - 20, -9.8f, GATE.y - 0.6f, GATE.y + 0.6f, false);
            addRect(-XB - 30, -XB, ZN - 30, ZS + 30, false); addRect(XB, XB + 30, ZN - 30, ZS + 30, false); addRect(-XB - 30, XB + 30, ZS, ZS + 30, false); addRect(-XB - 30, XB + 30, ZN - 30, ZN, false);

            cliffM = new[]
            {
                stdMat(Tx("cliffa_d"), Tx("cliffa_n"), new W { moss = 0.5f, tint = new[] { 0.6f, 0.6f, 0.58f }, gray = 0.4f }),
                stdMat(Tx("cliffb_d"), Tx("cliffb_n"), new W { moss = 0.5f, tint = new[] { 0.6f, 0.6f, 0.58f }, gray = 0.4f })
            };
            cliffG = new[] { Ph("rock_face_01.rock_face_01"), Ph("rock_face_02.rock_face_02") };
            cliffFn = cliff;
            var CG = Ph("cliffc.cliff");
            if (CG != null)
            {
                var cmat = stdMat(Tx("cliffc_d"), null, new W { moss = 0.5f, gray = 0.25f, tint = new[] { 0.95f, 0.93f, 0.9f } });
                var dBig = D(CG.geo, cmat, 170, false, true, 100, 9, 1.5f);
                // a real scanned cliff face, 92 m long, set end to end around the valley; its face is local +Z
                Action<float, float, float, float> big = (bcx, bcz, byaw, bs) =>
                {
                    float dx = cos(byaw), dz = -sin(byaw), Lb = 46 * bs; float y = 1e9f;
                    for (float t = -1; t <= 1.001f; t += 0.25f) y = min(y, terrainH(clamp(bcx + dx * Lb * t, -XB, XB), clamp(bcz + dz * Lb * t, ZN, ZS)));
                    SC.add("cliffc", dBig, mtx(bcx, y - 2.2f, bcz, byaw, bs, 0, 0, 2.15f));
                };
                float s = 1.7f, L = 92 * s * 0.93f; int i = 0;
                for (float z = ZS + 10 - L / 2; z > ZN - L / 2 - 10; z -= L, i++) { big(-XB - 3.5f - 2 * s, z + (i % 2 != 0 ? 4 : -3), HP, s); big(XB + 3.5f + 2 * s, z + (i % 2 != 0 ? -5 : 2), -HP, s); }
                big(-76, ZS + 6 + 2 * s, PI, s); big(78, ZS + 6 + 2 * s, PI, s); big(-(9 + 46 * 1.56f), ZN - 4 - 2 * 1.56f, 0, 1.56f); big(9 + 46 * 1.56f, ZN - 4 - 2 * 1.56f, 0, 1.56f);
            }
            else
            {
                for (float z = ZN - 4; z <= ZS + 4; z += 11) { cliff(-XB - R(3, 7), z, HP); cliff(XB + R(3, 7), z, -HP); }
                for (float x = -XB; x <= XB; x += 11) { cliff(x, ZS + R(4, 8), PI); if (abs(x) > 9) cliff(x, ZN - R(3, 6), 0); }
            }
            for (float x = 13; x <= XB; x += 10) { cliff(x, GATE.y - 3, 0); cliff(-x, GATE.y - 3, 0); }
            foreach (var mz in new[] { M1C, M2C })
                for (float z = mz.zN; z <= mz.zS; z += 9) { cliff(-mz.hw - R(4, 7), z, HP); cliff(mz.hw + R(4, 7), z, -HP); cliff(-mz.hw - R(14, 22), z, HP); cliff(mz.hw + R(14, 22), z, -HP); }
        }

        // ================= lanterns (save points) =================
        static float[] lp(Maze m, List<int[]> sol, float f) { var c = sol[floor((sol.Count - 1) * f)]; return m.center(c[0], c[1]); }
        static float[] off(List<float[]> P, float f, float d) { var q = pointAlong(P, f); return v2(q[0] - q[3] * d, q[1] + q[2] * d); }
        static void lanterns()
        {
            t1d = door(DEF["t1"], 3); t2d = door(DEF["t2"], 3); t4d = door(DEF["t4"], 3); t6d = door(DEF["t6"], 3);
            var LD = new List<float[]>
            {
                v2(3.4f, START.y - 1.5f), off(mainA.pts, 0.34f, 3.4f), v2(t1d[0], t1d[1] + 3.4f), off(mainA.pts, 0.68f, 3.4f), v2(t2d[0], t2d[1] - 3.6f), v2(ent1X + 4, m1.zS + 9), lp(m1, sol1, 0.34f), lp(m1, sol1, 0.68f), v2(exit1X + 4, m1.zN - 8),
                v2(pre3[0] + 9, pre3[1] + 6), v2(t4d[0], t4d[1] + 3.4f), v2(kEnt[0] - 4, kEnt[1] + 3.5f), v2(ent2X + 4, m2.zS + 8), lp(m2, sol2, 0.5f), v2(exit2X + 4, m2.zN - 8), v2(t6d[0], t6d[1] + 3.5f), off(mainC.pts, 0.52f, 3.2f), v2(4.2f, -257),
                v2(DB[0].x - 19, DB[0].z - 5), v2(DB[1].x - 19, DB[1].z - 5), v2(NCx + 17, NCz + 9)
            };
            foreach (var d in LD) makeLantern(d[0], d[1]);
            setLantern(LANTERNS[0], true);
        }

        // ================= set pieces =================
        /// <summary>JS giant(name,h,xr,zr,o). pathGap/pathMax/face/track were fields of the JS `o`; `o` holds the placeStatue overrides.</summary>
        static Statue giant(string name, float h, float[] xr, float[] zr, SO o = null, float? pathGap = null, float pathMax = 0f, float[] face = null, bool track = true)
        {
            if (o == null) o = new SO(); ModelInfo M; if (!MODELS.TryGetValue(name, out M)) { Debug.LogWarning("no model " + name); return null; }
            float r = max(M.d[0], M.d[1]) * 0.42f * h;
            var sp = findSpot(xr, zr, r, new FO { pathGap = pathGap ?? 5f, pathMax = pathMax, tries = 400 }); if (sp == null) return null;
            o.h = h; o.yaw = face != null ? faceTo(sp[0], sp[1], face[0], face[1]) + R(-0.3f, 0.3f) : R(-0.4f, 0.4f);
            if (o.sink == 0) o.sink = 0.012f; o.track = track; if (o.turn == 0) o.turn = 0.5f;
            return placeStatue(name, sp[0], sp[1], o);
        }
        /// <summary>JS clus(name,w,xr,zr,o). pathGap/pathMax/pi/eyes were fields of the JS `o`.</summary>
        static Statue clus(string name, float w, float[] xr, float[] zr, SO o = null, float? pathGap = null, float pathMax = 0f, int pi = 0, bool eyes = false)
        {
            if (o == null) o = new SO(); ModelInfo M; if (!MODELS.TryGetValue(name, out M)) { Debug.LogWarning("no model " + name); return null; }
            float r = max(M.d[0], M.d[1]) * 0.5f * w;
            var sp = findSpot(xr, zr, r, new FO { pathGap = pathGap ?? 2.5f, pathMax = pathMax != 0 ? pathMax : 30f, tries = 400 }); if (sp == null) return null;
            var q = nearestOn(paths[pi].pts, sp[0], sp[1]);
            o.w = w; o.yaw = faceTo(sp[0], sp[1], q[0], q[1]); if (o.sink == 0) o.sink = 0.02f; o.eyes = eyes; if (!o.col.HasValue) o.col = max(M.d[0], M.d[1]) * 0.42f;
            return placeStatue(name, sp[0], sp[1], o);
        }

        static void setPieces()
        {
            zrF = ZR["forest"]; zrB = ZR["field"]; zrC = ZR["final"];
            float[] fz = zrF, bz = zrB, cz = zrC;
            // forest
            giant("mossbuddha", 7.5f, v2(-70, 70), v2(230, 300), new SO { moss = 0.6f, msg = "霧裡坐著一尊石佛，長滿了青苔。祂的臉……剛才是朝那邊的嗎？", msgR = 20 }, null, 26, v2(START.x, START.y));
            giant("gandhara", 5.5f, v2(-120, 120), v2(fz[0] + 8, fz[1] - 30), new SO { moss = 0.55f });
            giant("ironbuddha", 6.5f, v2(-120, 120), v2(fz[0] + 8, fz[1] - 30), new SO { moss = 0.5f, gray = 0.3f });
            giant("head_b", 4.2f, v2(-110, 110), v2(fz[0] + 8, fz[1] - 20), new SO { sink = 0.16f, roll = 0.22f, pitch = -0.12f, moss = 0.6f, msg = "半埋在土裡的佛頭。眼睛是閉著的——應該是閉著的。", msgR = 13 });
            giant("torso", 4.6f, v2(-110, 110), v2(fz[0] + 8, fz[1] - 20), new SO { moss = 0.55f }, null, 0, null, false);
            clus("mound", 7.4f, v2(-60, 60), v2(236, 300), new SO { msg = "石佛堆成了一座小山。每一尊的臉都磨平了。", msgR = 13 });
            clus("group", 6.6f, v2(-80, 80), v2(fz[0] + 10, 236));
            clus("graves", 11, v2(-100, 100), v2(fz[0] + 10, fz[1] - 30), null, null, 40);
            clus("hokora", 4.8f, v2(-60, 60), v2(fz[0] + 20, fz[1] - 30), new SO { msg = "路邊的小祠。格子門後面，有東西在看你。", msgR = 9 }, null, 14);
            clus("hut", 10.5f, v2(-130, 130), v2(fz[0] + 10, fz[1] - 20), null, 10, 60);
            clus("yukimi", 2.6f, v2(-80, 80), v2(fz[0] + 10, fz[1] - 20), null, null, 12);
            {
                var sp = findSpot(v2(-110, 110), v2(fz[0] + 14, fz[1] - 40), 3, new FO { pathGap = 9, pathMax = 40 });
                if (sp != null) WELL = placeStatue("well", sp[0], sp[1], new SO { h = 2.6f, yaw = R(0, TAU), moss = 0.5f, col = 0.42f, eyes = false, msg = "一口古井。井底有東西，在一下一下地敲。", msgR = 8 });
            }
            foreach (float f in new[] { 0.07f, 0.5f, 0.93f })
            {
                var q = pointAlong(mainA.pts, f); float yaw = atan2(q[2], q[3]);
                placeStatue("torii", q[0], q[1], new SO { w = 6.6f, yaw = yaw, col = 0, moss = 0.5f, sink = 0.01f });
                foreach (int sg in SG) addCircle(q[0] + cos(yaw) * sg * 2.55f, q[1] - sin(yaw) * sg * 2.55f, 0.4f, false);
            }
            // field of giants
            float[] bx = v2(-125, 125), bzr = v2(bz[0] + 8, bz[1] - 8);
            giant("greenbuddha", 12, bx, bzr, new SO { moss = 0.55f });
            giant("amitabha", 19, bx, bzr, new SO { moss = 0.5f, gray = 0.2f });
            BLEEDER = giant("mossbuddha", 15, v2(-60, 60), v2(14, bz[1] - 10), new SO { tears = true, moss = 0.5f, msg = "那尊佛在流淚……是黑紅色的。", msgR = 34 }, null, 34, v2(exit1X, m1.zN));
            if (BLEEDER != null && BLEEDER.tears != null) BLEEDER.tears.visible = true;
            var cosmic = giant("cosmic", 17, bx, bzr, new SO { moss = 0.5f, msg = "一尊沒有頭的巨佛。", msgR = 30 }, null, 0, null, false);
            if (cosmic != null)
            {
                float a = cosmic.yaw + R(-0.5f, 0.5f); float hx = cosmic.x + sin(a) * 11, hz = cosmic.z + cos(a) * 11;
                if (canPlace(hx, hz, 2.6f) && !inPad(hx, hz, 3))
                    placeStatue("head_a", hx, hz, new SO { h = 4.6f, yaw = a + 2.4f, sink = 0.1f, roll = 0.5f, pitch = -0.2f, moss = 0.5f, track = true, turn = 0.3f, msg = "祂的頭滾在這裡。臉朝著你。", msgR = 14 });
            }
            giant("ironbuddha", 14, bx, bzr, new SO { moss = 0.5f, gray = 0.25f });
            giant("naga", 12, bx, bzr, new SO { moss = 0.5f });
            giant("elevenhead", 16, bx, bzr, new SO { moss = 0.5f, gray = 0.6f, msg = "十一張臉。每一張都朝不同的方向——現在全朝著你。", msgR = 28 });
            giant("gandhara", 10, bx, bzr, new SO { moss = 0.55f });
            giant("bluebuddha", 9, bx, bzr, new SO { moss = 0.5f });
            giant("head_b", 6.5f, v2(-120, 120), bzr, new SO { sink = 0.2f, roll = -0.3f, moss = 0.55f });
            clus("sixjizo", 6.6f, v2(-110, 110), bzr, null, null, 40, 1);
            clus("shrinescan", 7.2f, v2(-110, 110), bzr, null, 5, 40, 1);
            clus("relief", 9, v2(-110, 110), bzr, null, null, 40, 1);
            clus("jizohall", 3.3f, v2(-90, 90), bzr, null, null, 16, 1);
            for (int i = 0; i < 3; i++) clus("jizoped", 2.3f, v2(-110, 110), bzr, null, null, 22, 1, true);
            clus("stairs", 5, v2(-110, 110), bzr, null, 8, 50, 1);
            // back mountain
            float[] czr = v2(cz[0] + 50, cz[1] - 8);
            giant("armless", 10, bx, czr, new SO { moss = 0.5f, gray = 0.3f });
            giant("greenbuddha", 9, bx, czr, new SO { moss = 0.6f });
            giant("cosmic", 11, bx, czr, new SO { moss = 0.55f }, null, 0, null, false);
            giant("head_a", 5.5f, v2(-110, 110), czr, new SO { sink = 0.14f, roll = 0.35f, moss = 0.55f });
            clus("graves", 10, v2(-110, 110), czr, null, null, 40, 3);
            clus("group", 6, v2(-90, 90), czr, null, null, 26, 3);
            placeStatue("belltower", 18, -286, new SO { h = 7.4f, yaw = -0.5f, moss = 0.45f, col = 0.24f, eyes = false, msg = "鐘樓。沒有人，鐘卻在響。", msgR = 14 });
            foreach (int sg in SG)
                placeStatue(sg < 0 ? "nio_a" : "nio_b", sg * 7.6f, -314.5f, new SO { h = 4.8f, yaw = sg * -0.3f, track = true, turn = 1.0f, moss = 0.45f, gray = 0.6f, tint = new[] { 0.5f, 0.5f, 0.47f }, tintKey = "d" });
        }

        // ================= 死佛：giant heads grown into the mountainside =================
        static void deadBuddhas()
        {
            foreach (var d in DB)
            {
                var pad = PADS.Find(p => p.dead == d); ModelInfo M; MODELS.TryGetValue(d.model, out M);
                var st = placeStatue(d.model, d.x, d.z, new SO
                {
                    h = d.H, y = pad.h, sink = 0.07f, yaw = -HP, track = false, tears = true, strands = true, tearMat = paleTearMat, eyeScale = 0.55f, moss = 0.5f, gray = 0.85f, tint = new[] { 0.36f, 0.33f, 0.43f }, tintKey = "dead" + d.id,
                    wx = new W { tint2 = new[] { 1.5f, 0.95f, 0.92f }, t2 = 1, t2y = new[] { pad.h + d.H * 0.1f, pad.h + d.H * 0.36f } }, col = 0.4f, k = 1.8f,
                    msg = "山壁裡嵌著一顆巨大的佛頭。臉是黑的，眼睛下面有兩道白痕。", msgR = 46
                });
                if (st != null) { if (st.tears != null) st.tears.visible = true; st.dead = true; DEADB.Add(st); }
                LANDMARKS.Add(new Landmark { x = d.x - 5, z = d.z, name = "死佛崖" });
                for (int k = 0; k < 12; k++) { int side = k % 2 != 0 ? 1 : -1; cliffFn(d.x + R(-3, 9), d.z + side * R(7.8f, 14), -HP + R(-0.6f, 0.6f)); }
                if (M != null) HEADTOPS.Add(new HeadTop { x = d.x + 1.5f, z = d.z, y = pad.h + d.H * 0.93f - 0.4f, r = M.d[0] * d.H * 0.34f });
                float spx = d.x - 17, spz = d.z + 6.5f; resolve(ref spx, ref spz, 1); makeStele(spx, spz, -HP, "死佛 不可拜");
            }
        }

        // ================= 千佛崖：a cliff face cut full of niches =================
        static SO SM() { return new SO { moss = 0.3f, gray = 0.9f, tint = new[] { 1.2f, 0.86f, 0.76f }, tintKey = "crag", col = 0, track = false, k = 1.7f }; }
        static void nicheCliff()
        {
            var pad = PADS.Find(p => p.niche); float gy = pad.h, L = NCL, Hn = NCH;
            var niches = new List<float[]>(); var small = new List<float[]>();   // niche = {u0,u1,v0,v1,dep}
            Action<float, float, float, float, float> add = (a0, a1, b0, b1, dep) => niches.Add(new[] { a0, a1, b0, b1, dep });
            add(-5.6f, 5.6f, 0.5f, 13.5f, 3.0f); add(8.6f, 15.2f, 0.6f, 9.8f, 2.5f);
            for (int r = 0; r < 3; r++) for (int c = 0; c < 5; c++) { float u0 = -21.2f + c * 2.75f, v0 = 1.0f + r * 3.9f; add(u0, u0 + 2.1f, v0, v0 + 3.2f, 1.15f); small.Add(v2(u0 + 1.05f, v0)); }
            for (int r = 0; r < 3; r++) for (int c = 0; c < 2; c++) { float u0 = 16.6f + c * 2.75f, v0 = 1.0f + r * 3.9f; add(u0, u0 + 2.1f, v0, v0 + 3.2f, 1.15f); small.Add(v2(u0 + 1.05f, v0)); }
            for (int c = 0; c < 2; c++) { float u0 = 9.1f + c * 3.2f; add(u0, u0 + 2.3f, 10.7f, 13.8f, 1.05f); small.Add(v2(u0 + 1.15f, 10.7f)); }
            float stp = 0.42f, W2 = L + 12, HT = Hn + 7; int nu = round(W2 / stp), nv = round(HT / stp);
            Func<float, float, float> depthAt = (du_, dv_) =>
            {
                float dd = 0;
                foreach (var n in niches)
                {
                    float w = (n[1] - n[0]) / 2, cu = (n[0] + n[1]) / 2, du = abs(du_ - cu); if (du > w || dv_ < n[2] - 0.1f || dv_ > n[3]) continue; float ts = n[3] - w * 0.75f;
                    float ins = dv_ <= ts ? w - du : w - hypot(du, (dv_ - ts) / 0.75f); ins = min(ins, dv_ - n[2] + 0.12f); if (ins > 0) dd = max(dd, n[4] * smooth(0, 0.32f, ins));
                }
                return dd;
            };
            int nx1 = nu + 1, ny1 = nv + 1; var pos = new Vector3[nx1 * ny1]; var uv = new Vector2[nx1 * ny1];
            for (int iy = 0; iy < ny1; iy++) for (int ix = 0; ix < nx1; ix++)
                {
                    float u = -W2 / 2 + ix * W2 / nu, v = iy * HT / nv; float dep = v < Hn ? depthAt(u, v) : 0;
                    float rough = vnoise(u * 0.33f + 3, v * 0.33f + 7) * 0.5f + vnoise(u * 1.1f, v * 1.1f + 2) * 0.14f; float wing = max(0, abs(u) - L / 2 + 1) / 6;
                    float y = v, z = rough * (dep > 0.2f ? 0.25f : 1) - dep - wing * wing * 7 - pow(min(v, Hn) / Hn, 3) * 1.5f;
                    if (v > Hn) { float t = v - Hn; y = Hn + t * 0.12f + vnoise(u * 0.3f, t) * 0.3f; z -= t * 1.05f; }
                    pos[iy * nx1 + ix] = new Vector3(u, y, z); uv[iy * nx1 + ix] = new Vector2(u / 7.5f, v / 7.5f);
                }
            var tris = new int[nu * nv * 6]; int ti = 0;
            for (int iy = 0; iy < nv; iy++) for (int ix = 0; ix < nu; ix++)
                {
                    int a = iy * nx1 + ix, b = a + 1, c = a + nx1 + 1, d = a + nx1;   // +Z facing (three space)
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c; tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                }
            var wall = mkMesh(meshFromArrays(pos, null, uv, tris), stdMat(Tx("crag_d"), Tx("crag_n"), new W { moss = 0.44f, gray = 0.2f, tint = new[] { 0.95f, 0.84f, 0.8f } }), true, true, "nicheWall");
            wall.setPos(NCx, gy - 0.6f, NCz); wall.rotY = HP; cullAdd(wall, NCx, gy + 7, NCz, L / 2 + 10, 1.6f);
            addRect(NCx - 14, NCx + 0.4f, NCz - L / 2 - 7, NCz + L / 2 + 7, true);
            Func<float, float, float[]> PW = (pu, plz) => v2(NCx + plz, NCz - pu); var p = PW(0, -1.5f);
            {
                var o = SM(); o.h = 11.4f; o.y = gy - 0.05f; o.yaw = HP; o.tint = new[] { 0.62f, 0.48f, 0.44f }; o.tintKey = "crag2"; o.msg = "一整面山壁，鑿滿了佛龕。正中間那尊，比廟還高。"; o.msgR = 34;
                placeStatue("amitabha", p[0], p[1], o);
            }
            foreach (int sg in SG) { p = PW(sg * 3.7f, -1.75f); var o = SM(); o.h = 8.6f; o.y = gy - 0.05f; o.yaw = HP + sg * 0.14f; placeStatue("elevenhead", p[0], p[1], o); }
            { p = PW(11.9f, -1.25f); var o = SM(); o.h = 7.8f; o.y = gy + 0.02f; o.yaw = HP; placeStatue("gandhara", p[0], p[1], o); }
            for (int i = 0; i < small.Count; i++)
            {
                var s = small[i]; p = PW(s[0], -0.6f); string nm = i % 7 == 3 ? "niche" : (i % 5 == 1 ? "fudostele" : "jizostele");
                var o = SM(); o.h = nm == "niche" ? 2.3f : 2.65f; o.y = gy - 0.6f + s[1] + 0.04f; o.yaw = HP + R(-0.06f, 0.06f); o.cell = 60;
                placeInst(nm, p[0], p[1], o);
            }
            LANDMARKS.Add(new Landmark { x = NCx + 7, z = NCz, name = "千佛崖" }); NICHEPOS = new Vector2(NCx, NCz);
            float spx = NCx + 15, spz = NCz + L / 2 - 3; resolve(ref spx, ref spz, 1); makeStele(spx, spz, HP, "千佛崖 莫數");
        }

        // ================= maze entrance guardians & steles =================
        static void sign(float[] p, PathObj main, string t)
        {
            var a = nearestOn(main.pts, p[0], p[1]); float dx = p[0] - a[0], dz = p[1] - a[1]; float l = hypot(dx, dz); if (l == 0) l = 1; dx /= l; dz /= l; float k = min(6, l * 0.4f);
            float ppx = a[0] + dx * k - dz * 2.6f, ppz = a[1] + dz * k + dx * 2.6f; resolve(ref ppx, ref ppz, 1); resolve(ref ppx, ref ppz, 1);
            makeStele(ppx, ppz, atan2(a[0] - ppx, a[1] - ppz), t);
        }
        static void guardiansAndSteles()
        {
            foreach (int sg in SG)
                placeStatue("sekijin", ent1X + sg * 6.4f, m1.zS + 3.4f, new SO { h = 5.2f, yaw = sg * -0.2f, track = true, turn = 0.9f, moss = 0.5f, msg = sg < 0 ? "兩尊石人守著迷宮入口。" : null, msgR = 16 });
            var SP = new[] { mainA.pts, mainA.pts, mainB1.pts, mainC.pts }; var SF = new[] { 0.045f, 0.6f, 0.4f, 0.7f }; var ST = new[] { "入此山者 莫回頭", "佛在看你", "不要數祂們", "到了山門 也不要停" };
            for (int i = 0; i < 4; i++)
            {
                var q = pointAlong(SP[i], SF[i]); float ppx = q[0] + q[3] * 3.4f, ppz = q[1] - q[2] * 3.4f; resolve(ref ppx, ref ppz, 1);
                makeStele(ppx, ppz, atan2(q[0] - ppx, q[1] - ppz), ST[i]);
            }
            makeStele(ent1X - 3.6f, m1.zS + 6.5f, 0, "閉眼的佛 是醒著的");
            sign(t1d, mainA, "地藏堂 由此入"); sign(t2d, mainA, "本堂 藏鑰"); sign(t4d, mainB1, "千手殿 有符"); sign(t6d, mainC, "閻魔殿 藏鑰"); sign(kEnt, mainB1, "石室 莫入");
        }

        // ================= maze dead ends: statues, niches, steles =================
        class DE { public int c, r, d; public float cx, cz; }
        static List<DE> deadEnds(Maze m)
        {
            var o = new List<DE>();
            for (int r = 0; r < m.rows; r++) for (int c = 0; c < m.cols; c++)
                {
                    if (m.degree(c, r) != 1) continue; int d = -1; for (int k = 0; k < 4; k++) if (m.open(c, r, k)) d = k; if (d < 0) continue;
                    var ct = m.center(c, r); float cx = ct[0], cz = ct[1]; if (nearLantern(cx, cz, m.cell * 1.6f)) continue; o.Add(new DE { c = c, r = r, d = d, cx = cx, cz = cz });
                }
            for (int i = o.Count - 1; i > 0; i--) { int j = floor(rand() * (i + 1)); var t = o[i]; o[i] = o[j]; o[j] = t; }
            return o;
        }
        static void mazeDeadEnds()
        {
            string[] MAZE_TEXT = { "你已經來過這裡", "燈滅之時 祂們便會走動", "出口 在鐘聲之下", "不要數 祂們有幾尊", "回頭看看" };
            {
                var de = deadEnds(m1);
                string[] kn = { "gandhara", "ironbuddha", "niche", "stele", "fudostele", "bluebuddha", "niche", "naga", "stele" }; float[] kh = { 3.3f, 3.4f, 2.8f, 3.2f, 2.9f, 3.2f, 2.6f, 3.4f, 3f };
                int bi = 0, ti = 0;
                foreach (var e in de)
                {
                    int dx = DC[e.d], dz = -DR[e.d]; float yaw = atan2(dx, dz);
                    if (bi < kn.Length)
                    {
                        string nm = kn[bi]; float h = kh[bi]; ModelInfo M; float md = MODELS.TryGetValue(nm, out M) ? M.d[1] : 0.5f;
                        float back = m1.cell / 2 - m1.T / 2 - md * h * 0.5f - 0.25f;
                        placeStatue(nm, e.cx - dx * back, e.cz - dz * back, new SO { h = h, yaw = yaw, moss = 0.55f, track = false, col = 0.42f, turn = 1 }); bi++;
                    }
                    else if (ti < MAZE_TEXT.Length) { float back = m1.cell / 2 - 1.3f; makeStele(e.cx - dx * back, e.cz - dz * back, yaw, MAZE_TEXT[ti]); ti++; }
                    else break;
                }
                // demon tiles on walls
                int n = 0;
                for (int t = 0; t < 300 && n < 16; t++)
                {
                    int c = floor(rand() * m1.cols), r = floor(rand() * m1.rows), d = floor(rand() * 4); if (m1.open(c, r, d)) continue; if ((r == 0 && d == 2) || (r == m1.rows - 1 && d == 0)) continue;
                    var ct = m1.center(c, r); int dx = DC[d], dz = -DR[d]; float x = ct[0] + dx * (m1.cell / 2 - m1.T / 2 - 0.12f), z = ct[1] + dz * (m1.cell / 2 - m1.T / 2 - 0.12f);
                    placeStatue("onitile", x, z, new SO { w = R(1.3f, 1.9f), y = terrainH(x, z) + R(1.6f, 2.6f), yaw = atan2(-dx, -dz), col = 0, moss = 0.5f, k = 0.9f }); n++;
                }
            }
            {
                var de = deadEnds(m2); string[] T2 = { "竹聲 是祂們在走", "最後一段路 不要停" }; int bi = 0, ti = 0;
                string[] kn = { "jizoped", "torso", "sekijin", "jizoped" }; float[] kh = { 2.4f, 3.4f, 3f, 2.2f };
                foreach (var e in de)
                {
                    int dx = DC[e.d], dz = -DR[e.d]; float yaw = atan2(dx, dz);
                    if (bi < 4) { placeStatue(kn[bi], e.cx - dx * (m2.cell / 2 - 2.2f), e.cz - dz * (m2.cell / 2 - 2.2f), new SO { h = kh[bi], yaw = yaw, moss = 0.5f, col = 0.4f }); bi++; }
                    else if (ti < T2.Length) { makeStele(e.cx - dx * (m2.cell / 2 - 1.6f), e.cz - dz * (m2.cell / 2 - 1.6f), yaw, T2[ti]); ti++; }
                    else break;
                }
            }
        }

        // ================= roadside jizo steles (instanced) & final corridor rows =================
        static void rowAlong(List<float[]> P, float f0, float f1, float step, int[] sides)
        {
            float L = 0; for (int i = 1; i < P.Count; i++) L += hypot(P[i][0] - P[i - 1][0], P[i][1] - P[i - 1][1]); int n = Mathf.Max(1, floor((f1 - f0) * L / step));
            for (int i = 0; i <= n; i++)
            {
                var q = pointAlong(P, f0 + (f1 - f0) * i / n);
                foreach (int sd in sides)
                {
                    float x = q[0] + q[3] * 3.3f * sd, z = q[1] - q[2] * 3.3f * sd; if (!canPlace(x, z, 0.6f) || inPad(x, z, 1)) continue;
                    placeInst("jizostele", x, z, new SO { h = R(1.05f, 1.35f), yaw = atan2(q[0] - x, q[1] - z) + R(-0.15f, 0.15f), sink = 0.03f, roll = R(-0.06f, 0.06f), col = 0.3f });
                }
            }
        }
        static void rows()
        {
            rowAlong(mainA.pts, 0.2f, 0.3f, 2.3f, new[] { 1 }); rowAlong(mainA.pts, 0.78f, 0.9f, 2.3f, new[] { -1 }); rowAlong(mainB1.pts, 0.1f, 0.3f, 2.5f, new[] { 1, -1 }); rowAlong(mainB2.pts, 0.5f, 0.8f, 2.5f, new[] { 1 });
            for (int iz = 0; ; iz++)
            {
                float z = -263 - iz * 2.2f; if (z < -298) break;
                foreach (int sg in SG)
                {
                    if (abs(z + 286) < 4 && sg > 0) continue;
                    placeInst("jizostele", sg * 6.2f, z, new SO { h = R(1.1f, 1.4f), yaw = -sg * HP + R(-0.1f, 0.1f), sink = 0.03f, col = 0.3f });
                }
            }
        }

        // ================= chasers =================
        static void spawn(string zone, int n)
        {
            for (int k = 0; k < n; k++) { var sp = findSpot(v2(-125, 125), ZR[zone], 1.2f, new FO { pathGap = 1, tries = 300, lampGap = 17 }); if (sp == null) continue; makeChaser("arhat", sp[0], sp[1], new CO { zone = zone }); }
        }
        static void spawnMaze(Maze m, string zone, int n)
        {
            for (int k = 0, t = 0; k < n && t < 500; t++)
            {
                int c = floor(rand() * m.cols), r = 1 + floor(rand() * (m.rows - 1)); var ct = m.center(c, r); float x = ct[0], z = ct[1];
                if (nearLantern(x, z, m.cell * 2.1f)) continue; if (!canPlace(x, z, 1.0f)) continue;
                makeChaser("arhat", x + R(-1, 1), z + R(-1, 1), new CO { zone = zone, maze = m }); k++;
            }
        }
        static void chasers()
        {
            spawn("forest", 8); spawn("field", 10); spawn("final", 9);
            spawnMaze(m1, "maze1", 11); spawnMaze(m2, "maze2", 6);
            foreach (var p in new[] { new[] { -6.2f, -270.6f }, new[] { 6.2f, -279.4f }, new[] { -6.2f, -290.4f }, new[] { 6.2f, -294.8f } })
                makeChaser("arhat", p[0], p[1], new CO { zone = "final", yaw = p[0] < 0 ? HP : -HP });
        }

        // ================= trees =================
        class FVd { public string L; public float fh, fw, y0, y1, r1; public float[] s0, s90, top, sc; public Material tm; public Mesh crown; public PHPart tr; public ScDef dT, dC, dT18, dC18; }
        static float[] UVr(float[] c, float w, float h) { return new[] { c[0] / 2048f, 1 - (c[1] + h) / 2048f, (c[0] + w) / 2048f, 1 - c[1] / 2048f }; }
        static void trees()
        {
            // real firs: scanned trunks, crowns rendered from the full-detail scans
            var firW = new W { moss = 0.32f, gray = 0.15f, tint = new[] { 0.8f, 0.76f, 0.72f } };
            var FV = new[]
            {
                new FVd { L = "a", fh = 20, fw = 10, s0 = new[] { 0f, 0f }, s90 = new[] { 512f, 0f }, top = new[] { 1024f, 1024f }, y0 = 7.5f, y1 = 17, r1 = 0.157f, sc = new[] { 1.15f, 1.5f }, tm = stdMat(Tx("fira_d"), null, firW.clone()) },
                new FVd { L = "b", fh = 15, fw = 7.5f, s0 = new[] { 1024f, 0f }, s90 = new[] { 1536f, 0f }, top = new[] { 1536f, 1024f }, y0 = 5.5f, y1 = 12.5f, r1 = 0.131f, sc = new[] { 1.5f, 2.0f }, tm = stdMat(Tx("firb_d"), null, firW.clone()) },
                new FVd { L = "c", fh = 15, fw = 7.5f, s0 = new[] { 0f, 1024f }, s90 = new[] { 512f, 1024f }, top = new[] { 1024f, 1536f }, y0 = 8, y1 = 13, r1 = 0.15f, sc = new[] { 1.45f, 1.9f }, tm = stdMat(Tx("firk_d"), null, firW.clone()) }
            };
            var bmat = foliageMat(Tx("fir"), 0.3f, C(0xa9b29c), 0.05f, false, 1f, 0.95f); const float TK = 1.8f;
            foreach (var V in FV)
            {
                var mb = new MB(); float[] a = UVr(V.s0, 512, 1024), b = UVr(V.s90, 512, 1024), t = UVr(V.top, 512, 512); float hw = V.fw / 2, fh = V.fh, d = hw * 0.7071f; var rn = mulberry32((int)(V.fh * 7 + V.y0 * 13));
                mb.quad(v3(-hw, 0, 0), v3(hw, 0, 0), v3(hw, fh, 0), v3(-hw, fh, 0), a[0], a[1], a[2], a[3]); mb.quad(v3(0, 0, hw), v3(0, 0, -hw), v3(0, fh, -hw), v3(0, fh, hw), b[0], b[1], b[2], b[3]);
                mb.quad(v3(-d, 0, -d), v3(d, 0, d), v3(d, fh, d), v3(-d, fh, -d), b[0], b[1], b[2], b[3]); mb.quad(v3(-d, 0, d), v3(d, 0, -d), v3(d, fh, -d), v3(-d, fh, d), a[0], a[1], a[2], a[3]);
                int n = 6;
                for (int i = 0; i < n; i++)
                {
                    float f = i / (float)(n - 1), y = lerp(V.y0, V.y1, f), r = 3.75f * lerp(1.05f, 0.42f, f), ro = rn() * TAU, c = cos(ro), sn = sin(ro);
                    Func<float, float, float[]> P = (px, pz) => v3((px * c - pz * sn) * r, y, (px * sn + pz * c) * r);
                    mb.quad(P(-1, 1), P(1, 1), P(1, -1), P(-1, -1), t[0], t[1], t[2], t[3]);
                }
                for (int i = 0; i < mb.nor.Count; i += 3) { mb.nor[i] *= 0.35f; mb.nor[i + 2] *= 0.35f; mb.nor[i + 1] = 0.85f; }
                V.crown = mb.geo(); V.tr = Ph("fir.trunk_" + V.L);
                if (V.tr != null) { V.dT = D(V.tr.geo, V.tm, 40, true, true, 6, 9, 1.05f); V.dT18 = D(V.tr.geo, V.tm, 40, true, true, 6, 9, 1.8f); }
                V.dC = D(V.crown, bmat, 40, false, false, 9, 15, 1.05f); V.dC18 = D(V.crown, bmat, 40, false, false, 9, 15, 1.8f);
            }
            Func<float, float, float, float, float, float, float, float, float> addTree = (x, y, z, S, ry, rx, rz, k) =>
            {
                var V = FV[floor(rand() * FV.Length)]; if (V.tr == null) return 0; float sc = S * lerp(V.sc[0], V.sc[1], rand());
                SC.add("ft" + V.L, k > 1.5f ? V.dT18 : V.dT, mtx(x, y - 0.25f * sc, z, ry, sc * TK, rx, rz, sc));
                SC.add("fc" + V.L, k > 1.5f ? V.dC18 : V.dC, mtx(x, y - 0.25f * sc, z, ry, sc, rx, rz)); return V.r1 * sc * TK;
            };
            int count = 0;
            Action<int, float[], float[], float> plant = (n, xr, zr, pad) =>
            {
                for (int k = 0, t = 0; k < n && t < n * 10; t++)
                {
                    float x = R(xr[0], xr[1]), z = R(zr[0], zr[1]); if (inMazeBand(z, 2) || inPad(x, z, pad)) continue;
                    if (pathDist(x, z) < 2.9f) continue; if (z < -256 && abs(x) < 9) continue; if (hypot(x - START.x, z - START.y) < 6) continue; if (!canPlace(x, z, 2.2f)) continue;
                    if (nearLantern(x, z, 5)) continue;
                    bool nearGiant = false; for (int i = 0; i < STATUES.Count; i++) { var s = STATUES[i]; if (s.giant && hypot(s.x - x, s.z - z) < s.height * 0.5f + 5) { nearGiant = true; break; } }
                    if (nearGiant) continue;
                    float rr = addTree(x, terrainH(x, z), z, R(0.9f, 1.12f), R(0, TAU), R(-0.03f, 0.03f), R(-0.03f, 0.03f), 1.05f); addCircle(x, z, rr + 0.12f, false); count++; k++;
                }
            };
            plant(round(1250 * DENS), v2(-XB - 2, XB + 2), v2(M1C.zS + 2, ZS + 4), 1.5f);
            plant(round(330 * DENS), v2(-XB - 2, XB + 2), v2(M2C.zS + 2, M1C.zN - 2), 3f);     // JS {thin:0.0, pad:3}: thin 0 is falsy -> no thinning
            plant(round(980 * DENS), v2(-XB - 2, XB + 2), v2(ZN - 4, M2C.zN - 2), 1.5f);
            plant(round(110 * DENS), v2(-XB - 10, -M1C.hw - 6), v2(M1C.zN, M1C.zS), 1.5f); plant(round(110 * DENS), v2(M1C.hw + 6, XB + 10), v2(M1C.zN, M1C.zS), 1.5f);
            plant(round(70 * DENS), v2(-XB - 10, -M2C.hw - 6), v2(M2C.zN, M2C.zS), 1.5f); plant(round(70 * DENS), v2(M2C.hw + 6, XB + 10), v2(M2C.zN, M2C.zS), 1.5f);
            foreach (var ht in HEADTOPS) for (int k = 0; k < 6; k++)
                {
                    float a = R(0, TAU), rr = R(0, ht.r * 0.85f); float x = ht.x + cos(a) * rr, z = ht.z + sin(a) * rr, y = ht.y - (rr / ht.r) * (rr / ht.r) * ht.r * 0.5f - 0.8f;
                    addTree(x, y, z, R(0.2f, 0.4f), R(0, TAU), R(-0.18f, 0.18f), R(-0.18f, 0.18f), 1.8f);
                }
            Debug.Log("trees " + count);
        }

        // ================= rocks, deadwood, undergrowth =================
        static bool okSpot(float x, float z, float r) { return !(inMazeBand(z, 1) || inPad(x, z, 1) || pathDist(x, z) < r + 1.6f || (z < -256 && abs(x) < 8)); }
        static bool forest(float z) { return !inMazeBand(z, 2); }
        static float patch(float x, float z) { return vnoise(x * 0.045f + 11, z * 0.045f + 3) * 0.6f + vnoise(x * 0.17f + 5, z * 0.17f + 9) * 0.4f; }
        static readonly float[][] FR = { new[] { 40 / 512f, 55 / 512f, 130 / 512f, 480 / 512f }, new[] { 150 / 512f, 15 / 512f, 240 / 512f, 385 / 512f }, new[] { 270 / 512f, 15 / 512f, 340 / 512f, 245 / 512f }, new[] { 255 / 512f, 260 / 512f, 330 / 512f, 490 / 512f }, new[] { 370 / 512f, 55 / 512f, 450 / 512f, 440 / 512f } };
        static readonly float[][] GRc = { new[] { 95 / 512f, 385 / 512f, 250 / 512f, 452 / 512f }, new[] { 300 / 512f, 385 / 512f, 400 / 512f, 440 / 512f }, new[] { 12 / 512f, 448 / 512f, 100 / 512f, 505 / 512f }, new[] { 125 / 512f, 458 / 512f, 258 / 512f, 505 / 512f }, new[] { 295 / 512f, 452 / 512f, 400 / 512f, 505 / 512f } };
        static readonly float[] GRt = { 425 / 512f, 5 / 512f, 505 / 512f, 255 / 512f };
        // fern & grass cards cut from the scanned atlases (image-space UVs, v down)
        static Mesh fernGeo(int seed)
        {
            var rn = mulberry32(seed); var b = new MB(); int n = 6 + floor(rn() * 3);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * TAU + rn() * 0.6f; var r = FR[floor(rn() * FR.Length)]; float L = (r[3] - r[1]) * 1.05f * (0.8f + rn() * 0.4f), w = L * (r[2] - r[0]) / (r[3] - r[1]) * 1.25f; float dx = cos(a), dz = sin(a), sx = -dz * w / 2, sz = dx * w / 2;
                float lift = 0.75f + rn() * 0.5f; float[][] P = { new[] { 0.02f, 0.0f }, new[] { 0.36f, 0.36f * lift }, new[] { 0.72f, 0.42f * lift }, new[] { 1.0f, 0.22f * lift } };
                for (int k = 0; k < 3; k++)
                {
                    float[] p0 = P[k], p1 = P[k + 1]; float x0 = dx * p0[0] * L, z0 = dz * p0[0] * L, y0 = p0[1] * L, x1 = dx * p1[0] * L, z1 = dz * p1[0] * L, y1 = p1[1] * L; float v0 = lerp(r[3], r[1], k / 3f), v1 = lerp(r[3], r[1], (k + 1) / 3f);
                    b.quad(v3(x0 - sx, y0, z0 - sz), v3(x0 + sx, y0, z0 + sz), v3(x1 + sx, y1, z1 + sz), v3(x1 - sx, y1, z1 - sz), r[0], v0, r[2], v1);
                }
            }
            for (int i = 0; i < b.nor.Count; i += 3) { b.nor[i] *= 0.3f; b.nor[i + 1] = abs(b.nor[i + 1]) * 0.6f + 0.6f; b.nor[i + 2] *= 0.3f; }
            return b.geo(true);
        }
        static Mesh grassGeo(int seed, bool tall = false)
        {
            var rn = mulberry32(seed); var b = new MB(); int nq = tall ? 2 : 3;
            for (int i = 0; i < nq; i++)
            {
                float a = i / (float)nq * PI + rn() * 0.5f; var r = tall ? GRt : GRc[floor(rn() * GRc.Length)]; float hh = tall ? 0.62f : 0.2f + rn() * 0.07f, ww = hh * (r[2] - r[0]) / (r[3] - r[1]); float dx = cos(a) * ww / 2, dz = sin(a) * ww / 2; float ox = (rn() - 0.5f) * 0.12f, oz = (rn() - 0.5f) * 0.12f;
                b.quad(v3(ox - dx, -0.01f, oz - dz), v3(ox + dx, -0.01f, oz + dz), v3(ox + dx, hh, oz + dz), v3(ox - dx, hh, oz - dz), r[0], r[3], r[2], r[1]);
            }
            for (int i = 0; i < b.nor.Count; i += 3) { b.nor[i] = 0; b.nor[i + 1] = 1; b.nor[i + 2] = 0; }
            return b.geo(true);
        }
        static void undergrowth()
        {
            Material ra = stdMat(Tx("rocka_d"), Tx("rocka_n"), new W { moss = 0.45f, gray = 0.2f }), rb = stdMat(Tx("rockb_d"), Tx("rockb_n"), new W { moss = 0.45f, gray = 0.2f });
            string[] RKn = { "rock_moss_set_01.rock01", "rock_moss_set_01.rock03", "rock_moss_set_01.rock04", "rock_moss_set_01.rock06", "rock_moss_set_02.rock08", "rock_moss_set_02.rock10", "rock_moss_set_02.rock11", "rock_moss_set_02.rock13" };
            Material[] RKm = { ra, ra, ra, ra, rb, rb, rb, rb }; float[] RKr = { 1.3f, 1.0f, 1.0f, 1.1f, 0.5f, 0.8f, 0.8f, 0.8f };
            var RKg = new PHPart[8]; var RKd = new ScDef[8]; var RKk = new string[8];
            for (int i = 0; i < 8; i++) { RKg[i] = Ph(RKn[i]); RKk[i] = "rk" + i; if (RKg[i] != null) RKd[i] = D(RKg[i].geo, RKm[i], 50, true, true, 4, 0, 0.9f); }
            for (int k = 0, t = 0; k < round(420 * DENS) && t < 4000; t++)
            {
                float x = R(-XB, XB), z = R(ZN, ZS); int i = floor(rand() * RKn.Length); var g = RKg[i]; if (g == null) continue; float s = R(0.5f, 1.5f) * (i < 4 ? R(0.6f, 1.1f) : 1); float rad = RKr[i] * s;
                if (!okSpot(x, z, rad) || !canPlace(x, z, rad + 0.3f)) continue;
                SC.add(RKk[i], RKd[i], mtx(x, terrainH(x, z) - 0.12f * s, z, R(0, TAU), s, R(-0.2f, 0.2f), R(-0.2f, 0.2f)));
                if (rad > 0.7f) addCircle(x, z, rad * 0.8f, false); k++;
            }
            for (int k = 0; k < 16; k++)
            {
                int i = k % 4; var g = RKg[i]; if (g == null) continue; float x = KX + R(-12, 12), z = KZ + (k % 2 != 0 ? 1 : -1) * R(4.2f, 7); float s = R(1.2f, 2.2f);
                SC.add(RKk[i], RKd[i], mtx(x, terrainH(x, z) - 0.3f, z, R(0, TAU), s, R(-0.2f, 0.2f), R(-0.2f, 0.2f))); addCircle(x, z, RKr[i] * s * 0.8f, false);
            }
            Material sm = stdMat(Tx("stump_d"), Tx("stump_n"), new W { moss = 0.45f }), lm = stdMat(Tx("log_d"), Tx("log_n"), new W { moss = 0.5f }), brm = stdMat(Tx("branch_d"), null, new W { moss = 0.2f });
            PHPart sgP = Ph("tree_stump_01.tree_stump_01"), lgP = Ph("dead_tree_trunk.dead_tree_trunk"); PHPart[] bg = { Ph("dry_branches_medium_01.a"), Ph("dry_branches_medium_01.b") };
            ScDef dStump = sgP != null ? D(sgP.geo, sm, 50, false, true, 3, 0, 0.8f) : null, dLog = lgP != null ? D(lgP.geo, lm, 50, false, true, 5, 0, 0.8f) : null;
            ScDef[] dBr = { bg[0] != null ? D(bg[0].geo, brm, 30, false, false, 2, 0, 1, 40) : null, bg[1] != null ? D(bg[1].geo, brm, 30, false, false, 2, 0, 1, 40) : null };
            for (int k = 0, t = 0; k < round(120 * DENS) && t < 2000; t++)
            {
                float x = R(-XB, XB), z = R(ZN, ZS); if (!forest(z) || !okSpot(x, z, 1) || !canPlace(x, z, 1.2f) || sgP == null) continue; float s = R(0.7f, 1.3f);
                SC.add("stump", dStump, mtx(x, terrainH(x, z) - 0.05f, z, R(0, TAU), s)); addCircle(x, z, 0.55f * s, false); k++;
            }
            for (int k = 0, t = 0; k < round(110 * DENS) && t < 2000; t++)
            {
                float x = R(-XB, XB), z = R(ZN, ZS); if (!forest(z) || !okSpot(x, z, 2) || !canPlace(x, z, 1.8f) || lgP == null) continue; float s = R(1.1f, 2.2f);
                SC.add("log", dLog, mtx(x, terrainH(x, z) + 0.02f, z, R(0, TAU), s, 0, R(-0.08f, 0.08f))); k++;
            }
            for (int k = 0; k < round(600 * DENS); k++)
            {
                float x = R(-XB, XB), z = R(ZN, ZS); if (!forest(z) || inPad(x, z, 0.5f)) continue; var g = bg[k % 2]; if (g == null) continue;
                SC.add(k % 2 == 0 ? "br0" : "br1", dBr[k % 2], mtx(x, groundH(x, z) + 0.03f, z, R(0, TAU), R(0.8f, 1.8f)));
            }
            var fm = foliageMat(Tx("fern"), 0.42f, C(0x8f9a7c), 0.04f, true, 1f, 0.9f);
            var gm = foliageMat(Tx("grass"), 0.22f, C(0xe6ecc8), 0.03f, true, 1.55f, 0.95f);
            Mesh[] FG = { fernGeo(11), fernGeo(23), fernGeo(37) }, GG = { grassGeo(5), grassGeo(9), grassGeo(13), grassGeo(17, true) };
            string[] kFern = { "fern0", "fern1", "fern2" }, kGr = { "gr0", "gr1", "gr2", "gr3" }, kFernT = { "fernT0", "fernT1", "fernT2" }, kGrT = { "grT0", "grT1", "grT2" };
            var dFern = new ScDef[3]; var dGr = new ScDef[4]; var dFernT = new ScDef[3]; var dGrT = new ScDef[3];
            for (int i = 0; i < 3; i++) { dFern[i] = D(FG[i], fm, 30, false, false, 2, 0, 1, 48); dFernT[i] = D(FG[i], fm, 60, false, false, 3, 0, 1.8f); dGrT[i] = D(GG[i], gm, 60, false, false, 3, 0, 1.8f); }
            for (int i = 0; i < 4; i++) dGr[i] = D(GG[i], gm, 30, false, false, 1.5f, 0, 1, 38);
            for (int k = 0; k < round(26000 * DENS); k++)
            {
                float x = R(-XB, XB), z = R(ZN, ZS); if (!forest(z) || inPad(x, z, 0.6f)) continue; float pd = pathDist(x, z); if (pd < 1.5f) continue; if (patch(x, z) < (pd < 12 ? -0.25f : 0.05f)) continue;
                int i = floor(rand() * 3); SC.add(kFern[i], dFern[i], mtx(x, groundH(x, z) - 0.02f, z, R(0, TAU), R(0.9f, 1.7f)));
            }
            for (int k = 0; k < round(110000 * DENS); k++)
            {
                float x = R(-XB, XB), z = R(ZN, ZS); if (inPad(x, z, 0.4f)) continue; float pd = pathDist(x, z); if (pd < 1.0f) continue; if (z < M1C.zS && z > M1C.zN && rand() < 0.8f) continue; if (patch(x + 40, z - 20) < (pd < 10 ? -0.35f : -0.05f)) continue;
                int i = rand() < 0.08f ? 3 : floor(rand() * 3); SC.add(kGr[i], dGr[i], mtx(x, groundH(x, z), z, R(0, TAU), R(1.0f, 2.1f) * (i == 3 ? 0.9f : 1)));
            }
            foreach (var ht in HEADTOPS)
            {
                for (int k = 0; k < 70; k++)
                {
                    float a = R(0, TAU), rr = R(0, ht.r); float x = ht.x + cos(a) * rr, z = ht.z + sin(a) * rr, y = ht.y - (rr / ht.r) * (rr / ht.r) * ht.r * 0.55f - 0.3f; int i = floor(rand() * 3);
                    if (k % 2 != 0) SC.add(kFernT[i], dFernT[i], mtx(x, y, z, R(0, TAU), R(1.6f, 3.2f), R(-0.3f, 0.3f), R(-0.3f, 0.3f)));
                    else SC.add(kGrT[i], dGrT[i], mtx(x, y, z, R(0, TAU), R(2.5f, 4.5f)));
                }
            }
        }

        // ================= ground =================
        static void ground()
        {
            int step = 2; float GW = 2 * (XB + 16), GD = ZS - ZN + 32; int sx = round(GW / step), sz = round(GD / step);
            int nx1 = sx + 1, nz1 = sz + 1, cnt = nx1 * nz1; var pos = new Vector3[cnt]; var uv = new Vector2[cnt]; var sp = new Color[cnt];
            for (int iz = 0; iz < nz1; iz++) for (int ix = 0; ix < nx1; ix++)
                {
                    int i = iz * nx1 + ix; float x = -GW / 2 + ix * GW / sx, z = -GD / 2 + iz * GD / sz;
                    pos[i] = new Vector3(x, terrainH(x, z), z); uv[i] = new Vector2(x * 0.21f, z * 0.21f);
                    float moss = smooth(-0.15f, 0.55f, vnoise(x * 0.045f + 5, z * 0.045f + 9)) * 0.9f, path = 0, stone = 0; float pd = pathDist(x, z);
                    if (pd < 2.6f) path = 1 - pd / 2.6f;
                    if (z < M1C.zS + 1 && z > M1C.zN - 1 && abs(x) < M1C.hw + 1) { stone = 0.85f; path = 0; moss *= 0.5f; }
                    if (z < -262 && abs(x) < 5.5f) { stone = 1; path = 0; }
                    for (int k = 0; k < PADS.Count; k++)
                    {
                        var p = PADS[k]; if (p.kofun || p.gate) continue; float dx = max(abs(x - p.x) - (p.hx - 2.5f), 0), dz = max(abs(z - p.z) - (p.hz - 2.5f), 0); float d = hypot(dx, dz); if (d < 3) stone = max(stone, 1 - d / 3);
                    }
                    float br = 0.5f + vnoise(x * 0.09f + 2, z * 0.09f + 4) * 0.14f - smooth(XB - 14, XB + 8, abs(x)) * 0.2f;
                    sp[i] = new Color(moss, path, stone, br);
                }
            var tris = new int[sx * sz * 6]; int ti = 0;
            for (int iz = 0; iz < sz; iz++) for (int ix = 0; ix < sx; ix++)
                {
                    int a = iz * nx1 + ix, b = a + nx1, c = b + 1, d = a + 1;   // +Y facing (three space)
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c; tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                }
            groundMat = new Material(sh("QF/Ground"));
            groundMat.SetTexture("_MainTex", Tx("floor_d")); groundMat.SetTexture("_BumpMap", Tx("floor_n"));
            groundMat.SetTexture("_MossTex", Tx("moss_d")); groundMat.SetTexture("_MossN", Tx("moss_n"));
            groundMat.SetTexture("_MudTex", Tx("mud_d")); groundMat.SetTexture("_MudN", Tx("mud_n"));
            groundMat.SetTexture("_StoneTex", Tx("stone_d")); groundMat.SetTexture("_StoneN", Tx("stone_n"));
            mkMesh(meshFromArrays(pos, null, uv, tris, sp), groundMat, false, true, "ground");
        }

        // ================= final stairs & gate =================
        static void gate()
        {
            var M = hallMats(); float gz = GATE.y; float y0 = terrainH(0, gz); var sb = new MB();
            for (int iz = -300; iz >= -321; iz--) { sb.box(0, terrainH(0, iz) - 0.22f, iz, 9.4f, 0.6f, 1.05f, 2.5f); }
            var sm = mkMesh(sb.geo(), stdMat(Tx("wall_d"), Tx("wall_n"), new W { moss = 0.3f, tint = new[] { 0.8f, 0.8f, 0.78f } }), false, true, "gateStairs"); cullAdd(sm, 0, y0, -310, 16, 1.2f);
            MB pb = new MB(), rb = new MB();
            foreach (int sx in SG) foreach (int sz in SG) { pb.cyl(sx * 4.9f, 0, sz * 1.6f, 0.5f, 9, 12, 1.6f); addCircle(sx * 4.9f, gz + sz * 1.6f, 0.62f, true); }
            foreach (int sz in SG) pb.box(0, 7.9f, sz * 1.6f, 12.4f, 0.7f, 0.7f, 1.6f); pb.box(0, 6.4f, 1.6f, 10.8f, 0.45f, 0.5f, 1.6f);
            foreach (int sx in SG) { pb.box(sx * 7.4f, 3.4f, 0, 4.6f, 6.8f, 0.5f, 2.2f); addRect(sx * 4.9f, sx * 9.8f, gz - 0.4f, gz + 0.4f, true); }
            hipRoof(rb, 7.6f, 3.6f, 8.5f, 3.6f, 1.1f, 1.5f); rb.quad(v3(-7.6f, 8.48f, -3.6f), v3(7.6f, 8.48f, -3.6f), v3(7.6f, 8.48f, 3.6f), v3(-7.6f, 8.48f, 3.6f), 0, 0, 7.6f, 3.6f);
            var g = mkGroup("gate"); g.setPos(0, y0, gz);
            g.add(mkMesh(pb.geo(), M["pilOut"], true, true)); g.add(mkMesh(rb.geo(), M["roof"], true, true));
            var plq = mkMesh(planeGeo(3.2f, 1.07f), litMat(Color.white, 0.8f, 0f, plaqueTex("千佛山", false)), false, false, "gatePlaque"); plq.setPos(0, 7.2f, 1.95f); g.add(plq);
            var doors = new Obj[2]; var locks = new Obj[2, 3]; var lockMat = litMat(C(0x8a6a2c), 0.4f, 0.8f); var lockGeo = boxGeo(0.34f, 0.26f, 0.12f);
            for (int di = 0; di < 2; di++)
            {
                int sx = SG[di]; var dg = mkGroup("gateDoor"); dg.setPos(sx * 4.4f, 0, 0); var b = new MB(); b.box(-sx * 2.2f, 3.1f, 0, 4.4f, 6.2f, 0.22f, 2.2f);
                dg.add(mkMesh(b.geo(), M["woodOut"], true, false));
                for (int k = 0; k < 3; k++) { var lk = mkMesh(lockGeo, lockMat, false, false, "lock"); lk.setPos(-sx * 4.25f, 2.1f + k * 0.75f, 0.18f); dg.add(lk); locks[di, k] = lk; }
                g.add(dg); doors[di] = dg;
            }
            var bell = mkMesh(latheGeo(new[] { new[] { 0f, 1.5f }, new[] { 0.2f, 1.48f }, new[] { 0.35f, 1.35f }, new[] { 0.42f, 1.0f }, new[] { 0.48f, 0.4f }, new[] { 0.62f, 0.05f }, new[] { 0.6f, 0f } }, 20),
                litMat(C(0x3d3b30), 0.5f, 0.7f, null, null, 1f, true), false, false, "gateBell");
            bell.setPos(0, 5.0f, -1.2f); g.add(bell); cullAdd(g, 0, y0, gz, 16, 1.4f);
            var col = addRect(-4.4f, 4.4f, gz - 0.3f, gz + 0.3f, true);
            exitGlowMat = basicMat(C(0xf7f1e2), false, glowTex, true, 0f, false, false, true);
            exitGlow = mkMesh(planeGeo(8.6f, 7.6f), exitGlowMat, false, false, "exitGlow"); exitGlow.setPos(0, y0 + 3.6f, gz - 2.2f);
            GATEOBJ = new GateObj { g = g, doors = doors, locks = locks, col = col, open = 0, opening = false, y0 = y0 };
        }

        // ================= ghost & phantom (rain and moon live in Render.cs) =================
        static void ghostAndPhantom()
        {
            {
                var gmat = basicMat(C(0x030303), true, null, true, 0.94f); var root_ = mkGroup("ghost");
                ModelInfo M; if (MODELS.TryGetValue("arhat", out M)) foreach (var p in M.parts) root_.add(mkMesh(p.geo, gmat, false, false));
                root_.setScale(1.9f); root_.visible = false; ghost = new GhostObj { root = root_, active = false, cool = 25, life = 0 };
            }
            {
                PHPart bgP = Ph("phantom.body"), agP = Ph("phantom.arm");
                if (bgP != null && agP != null)
                {
                    var pm = basicMat(C(0x040404), true, null, true, 0f, false, false);
                    var root_ = mkGroup("phantom"); root_.add(mkMesh(bgP.geo, pm, false, false));
                    var piv = mkGroup("phantomArm"); piv.setPos(-0.2f, 1.42f, 0); var am = mkMesh(agP.geo, pm, false, false); am.setPos(0.2f, -1.42f, 0); piv.add(am); root_.add(piv);
                    root_.visible = false; root_.setScale(1.06f);
                    PHANTOM = new PhantomObj { root = root_, arm = piv, mat = pm, active = false, cool = 45, x = 0, z = 0, hops = 0, life = 0, callT = 0, calls = 0, warnT = -1, fade = 0, gone = 0, dx = 0, dz = 0 };
                }
            }
        }
    }
}
