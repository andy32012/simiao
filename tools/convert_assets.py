#!/usr/bin/env python3
"""Convert the web build's assets (a/) into the Unity project.

    python -I tools/convert_assets.py <path-to-a-folder> <unity-project-root>

Outputs (all under <project>/Assets/Resources unless noted):
  Tex/<name>.png            world textures actually used by the game (TEXN_ALL)
  ModelTex/<name>.png       model base-colour textures; normal maps get the suffix _n
  Mesh/<file>.bytes         one binary per glTF (format below)
  Audio/<name>.mp3|wav      voice clips
  assets_manifest.json      copy of a/assets.json
  ../Game/ExtraTex/         textures present in a/tex but not referenced by the game (not shipped)

Mesh binary (little endian), "QFM1":
  char[4] magic, int32 partCount, then per part:
    str key        (uint16 length + utf8)  node name (PH pieces) or primitive index (models)
    str tex        base-colour texture name without extension ("" = none)
    str normalTex  normal-map texture name without extension ("" = none)
    uint8 flags    bit0 unlit, bit1 hasUV, bit2 doubleSided, bit3 32-bit indices
    int32 vcount, int32 icount
    float32 pos[vcount*3]       node transform + quantisation already applied (glTF / three.js space)
    int8    nor[vcount*4]       xyz * 127, 4th byte padding
    float32 uv[vcount*2]        only if hasUV; glTF convention (v down) -> loader flips v
    uint16|uint32 idx[icount]   CCW triangles (glTF winding)
    padding to a multiple of 4 bytes
"""
import sys, os, json, base64, struct, shutil, array, glob, math

TEXN_ALL = ['floor_d', 'floor_n', 'moss_d', 'moss_n', 'mud_d', 'mud_n', 'stone_d', 'stone_n', 'wall_d', 'wall_n', 'roof_d', 'roof_n',
            'wood_d', 'wood_n', 'crag_d', 'crag_n', 'rocka_d', 'rocka_n', 'rockb_d', 'rockb_n', 'cliffa_d', 'cliffa_n', 'cliffb_d', 'cliffb_n',
            'stump_d', 'stump_n', 'log_d', 'log_n', 'branch_d', 'fern', 'grass', 'bamboo', 'fir', 'fira_d', 'firb_d', 'firk_d', 'cliffc_d']
PHN = ['phantom', 'rock_moss_set_01', 'rock_moss_set_02', 'rock_face_01', 'rock_face_02', 'tree_stump_01', 'dead_tree_trunk',
       'dry_branches_medium_01', 'fir', 'cliffc']
PH_KEYS_USED = ['cliffc.cliff', 'dead_tree_trunk.dead_tree_trunk', 'dry_branches_medium_01.a', 'dry_branches_medium_01.b', 'phantom.arm', 'phantom.body',
                'rock_face_01.rock_face_01', 'rock_face_02.rock_face_02', 'tree_stump_01.tree_stump_01', 'fir.trunk_a', 'fir.trunk_b', 'fir.trunk_c',
                'rock_moss_set_01.rock01', 'rock_moss_set_01.rock03', 'rock_moss_set_01.rock04', 'rock_moss_set_01.rock06',
                'rock_moss_set_02.rock08', 'rock_moss_set_02.rock10', 'rock_moss_set_02.rock11', 'rock_moss_set_02.rock13']

CT = {5120: ('b', 1), 5121: ('B', 1), 5122: ('h', 2), 5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}
NORM = {5120: 127.0, 5121: 255.0, 5122: 32767.0, 5123: 65535.0}


def read_accessor(g, buf, ai):
    """-> (flat python list of floats/ints, components) with normalisation applied."""
    a = g['accessors'][ai]
    if 'sparse' in a: raise RuntimeError('sparse accessor')
    code, size = CT[a['componentType']]; nc = NC[a['type']]; count = a['count']
    bv = g['bufferViews'][a['bufferView']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0); esize = size * nc; stride = bv.get('byteStride', esize)
    if stride == esize:
        raw = bytes(buf[off:off + esize * count])
    else:
        mv = memoryview(buf); raw = b''.join(mv[off + i * stride: off + i * stride + esize] for i in range(count))
    arr = array.array(code); arr.frombytes(raw)
    if a.get('normalized'):
        k = NORM[a['componentType']]
        if a['componentType'] in (5120, 5122): return [max(v / k, -1.0) for v in arr], nc
        return [v / k for v in arr], nc
    return arr.tolist(), nc


def wstr(out, s):
    b = s.encode('utf8'); out.append(struct.pack('<H', len(b))); out.append(b)


def image_name(g, tex_index):
    t = g['textures'][tex_index]
    src = t.get('extensions', {}).get('EXT_texture_webp', {}).get('source', t.get('source'))
    return os.path.splitext(g['images'][src]['uri'])[0]


def convert_gltf(path, is_ph, tex_roles):
    """tex_roles: dict image base name -> 'base' | 'normal' (filled in)."""
    g = json.load(open(path, encoding='utf8'))
    assert len(g['buffers']) == 1
    uri = g['buffers'][0]['uri']; buf = base64.b64decode(uri[uri.index(',') + 1:])
    mats = g.get('materials') or []
    roots = g['scenes'][g.get('scene', 0)]['nodes']
    parts = []
    for ni in roots:
        n = g['nodes'][ni]
        if 'children' in n or 'rotation' in n or 'matrix' in n: raise RuntimeError('unsupported node transform in ' + path)
        if 'mesh' not in n: continue
        T = n.get('translation', [0, 0, 0]); S = n.get('scale', [1, 1, 1])
        flip = (S[0] * S[1] * S[2]) < 0
        prims = g['meshes'][n['mesh']]['primitives']
        for pi, p in enumerate(prims):
            if p.get('mode', 4) != 4: raise RuntimeError('non-triangle primitive')
            pos, _ = read_accessor(g, buf, p['attributes']['POSITION'])
            nor, _ = read_accessor(g, buf, p['attributes']['NORMAL'])
            vc = len(pos) // 3
            px = array.array('f', [0.0]) * (vc * 3); nb = array.array('b', [0]) * (vc * 4)
            sx, sy, sz = S; tx, ty, tz = T; isx, isy, isz = 1.0 / sx, 1.0 / sy, 1.0 / sz
            for i in range(vc):
                j = i * 3
                px[j] = pos[j] * sx + tx; px[j + 1] = pos[j + 1] * sy + ty; px[j + 2] = pos[j + 2] * sz + tz
                x = nor[j] * isx; y = nor[j + 1] * isy; z = nor[j + 2] * isz
                l = math.sqrt(x * x + y * y + z * z) or 1.0
                k = i * 4
                nb[k] = int(round(x / l * 127)); nb[k + 1] = int(round(y / l * 127)); nb[k + 2] = int(round(z / l * 127))
            has_uv = 'TEXCOORD_0' in p['attributes']
            if has_uv:
                uv, _ = read_accessor(g, buf, p['attributes']['TEXCOORD_0']); uvb = array.array('f', uv)
            if 'indices' in p: idx, _ = read_accessor(g, buf, p['indices'])
            else: idx = list(range(vc))
            if flip:
                for i in range(0, len(idx), 3): idx[i + 1], idx[i + 2] = idx[i + 2], idx[i + 1]
            big = vc > 65535
            tex = ntex = ''; unlit = False; dbl = False
            mi = p.get('material')
            if mi is not None:
                m = mats[mi]; dbl = bool(m.get('doubleSided')); unlit = 'KHR_materials_unlit' in m.get('extensions', {})
                bct = m.get('pbrMetallicRoughness', {}).get('baseColorTexture')
                if bct is not None: tex = image_name(g, bct['index']); tex_roles[tex] = 'base'
                if 'normalTexture' in m:
                    nimg = image_name(g, m['normalTexture']['index']); tex_roles.setdefault(nimg, 'normal'); ntex = nimg + '_n'
            key = n.get('name', '') if is_ph else str(pi)
            out = []
            wstr(out, key); wstr(out, tex); wstr(out, ntex)
            out.append(struct.pack('<Bii', (1 if unlit else 0) | (2 if has_uv else 0) | (4 if dbl else 0) | (8 if big else 0), vc, len(idx)))
            blob = b''.join(out)
            blob += px.tobytes() + nb.tobytes() + (uvb.tobytes() if has_uv else b'') + array.array('I' if big else 'H', idx).tobytes()
            blob += b'\0' * ((-len(blob)) % 4)
            parts.append((key, vc, len(idx) // 3, tex, ntex, blob))
    data = b'QFM1' + struct.pack('<i', len(parts)) + b''.join(p[5] for p in parts)
    return data, parts


def main():
    src = sys.argv[1]; proj = sys.argv[2]
    try:
        from PIL import Image
    except ImportError:   # `python -I` hides the per-user site-packages where Pillow usually lives
        import site; site.addsitedir(site.getusersitepackages())
        from PIL import Image
    res = os.path.join(proj, 'Assets', 'Resources')
    d_tex, d_mtex, d_mesh, d_aud = (os.path.join(res, x) for x in ('Tex', 'ModelTex', 'Mesh', 'Audio'))
    d_extra = os.path.join(proj, 'Assets', 'Game', 'ExtraTex')
    for d in (d_tex, d_mtex, d_mesh, d_aud): os.makedirs(d, exist_ok=True)

    def conv_img(inp, outp):
        if os.path.exists(outp) and os.path.getmtime(outp) >= os.path.getmtime(inp): return None
        im = Image.open(inp); im.load()
        if im.mode not in ('RGB', 'RGBA'): im = im.convert('RGBA' if 'A' in im.getbands() or 'transparency' in im.info else 'RGB')
        im.save(outp, 'PNG', compress_level=6)
        return im.mode, im.size

    # ---- world textures
    for f in sorted(glob.glob(os.path.join(src, 'tex', '*.webp'))):
        n = os.path.splitext(os.path.basename(f))[0]
        if n in TEXN_ALL: r = conv_img(f, os.path.join(d_tex, n + '.png'))
        else:
            os.makedirs(d_extra, exist_ok=True); r = conv_img(f, os.path.join(d_extra, n + '.png')); print('unused world texture ->ExtraTex:', n)
        if r: print('tex', n, r)
    missing = [n for n in TEXN_ALL if not os.path.exists(os.path.join(d_tex, n + '.png'))]
    if missing: print('!! MISSING world textures', missing)

    # ---- meshes
    man = json.load(open(os.path.join(src, 'assets.json'), encoding='utf8'))
    tex_roles = {}; tot_v = tot_t = tot_b = 0; ph_keys = []; notes = []
    names = list(man['models'].keys())
    for n in names + ['ph_' + x for x in PHN]:
        path = os.path.join(src, 'm', n + '.json'); is_ph = n.startswith('ph_')
        data, parts = convert_gltf(path, is_ph, tex_roles)
        open(os.path.join(d_mesh, n + '.bytes'), 'wb').write(data)
        v = sum(p[1] for p in parts); t = sum(p[2] for p in parts); tot_v += v; tot_t += t; tot_b += len(data)
        if is_ph: ph_keys += [n[3:] + '.' + p[0] for p in parts]
        nm = [p[4] for p in parts if p[4]]
        if nm: notes.append('%s normal maps: %s' % (n, sorted(set(nm))))
        print('mesh %-28s parts %2d verts %7d tris %7d  %6.1f KB' % (n, len(parts), v, t, len(data) / 1024))
    print('TOTAL verts %d tris %d mesh bytes %.1f MB' % (tot_v, tot_t, tot_b / 1048576))
    print('PH keys:', ph_keys)
    bad = [k for k in PH_KEYS_USED if k not in ph_keys]
    print('PH keys used by the game that are missing:', bad if bad else 'none')
    for s in notes: print(s)

    # ---- model textures (only the referenced ones; normal maps get _n)
    allimg = {os.path.splitext(os.path.basename(f))[0] for f in glob.glob(os.path.join(src, 'm', '*.webp'))}
    for n in sorted(allimg):
        role = tex_roles.get(n)
        if role is None: print('unreferenced model texture (skipped):', n); continue
        r = conv_img(os.path.join(src, 'm', n + '.webp'), os.path.join(d_mtex, n + ('_n' if role == 'normal' else '') + '.png'))
        if r and (r[0] != 'RGB'): print('modeltex with alpha', n, r)
    miss = sorted(set(tex_roles) - allimg)
    if miss: print('!! MISSING model textures', miss)

    # ---- audio + manifest
    na = 0
    for f in sorted(glob.glob(os.path.join(src, 'v', '*'))):
        if f.lower().endswith(('.mp3', '.wav')): shutil.copy2(f, os.path.join(d_aud, os.path.basename(f))); na += 1
    shutil.copy2(os.path.join(src, 'assets.json'), os.path.join(res, 'assets_manifest.json'))
    print('audio files', na, '| models', len(names), '| credits', len(man.get('credits', [])))


if __name__ == '__main__':
    main()
