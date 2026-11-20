// Import settings for the converted web assets (tools/convert_assets.py output).
//  Resources/Tex/*.png       world textures: *_n = tangent-space normal maps kept as plain LINEAR RGB (the QF shaders unpack rgb*2-1 themselves)
//  Resources/ModelTex/*.png  scanned model textures; normal maps are the files ending in _n
//  Resources/Audio/*         voice clips, decompressed on load so scripts may call AudioClip.GetData
using System.IO;
using UnityEditor;
using UnityEngine;

public class QFTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        bool tex = p.StartsWith("Assets/Resources/Tex/"), mtex = p.StartsWith("Assets/Resources/ModelTex/");
        if (!tex && !mtex) return;
        var ti = (TextureImporter)assetImporter;
        string n = Path.GetFileNameWithoutExtension(p);
        bool normal = n.EndsWith("_n");
        float cutoff = 0f;                                   // alpha-tested atlases (alphaTest values used by the game)
        if (tex) { if (n == "bamboo") cutoff = 0.4f; else if (n == "fir") cutoff = 0.3f; else if (n == "fern") cutoff = 0.42f; else if (n == "grass") cutoff = 0.22f; }

        ti.textureType = TextureImporterType.Default;        // NOT NormalMap: no DXT5nm/AG swizzle, shaders read rgb
        ti.textureShape = TextureImporterShape.Texture2D;
        ti.sRGBTexture = !normal;
        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 4;
        ti.maxTextureSize = 2048;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.isReadable = false;
        ti.textureCompression = normal ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
        if (cutoff > 0f)
        {
            ti.wrapMode = TextureWrapMode.Clamp;             // card atlases: avoid mip bleeding across the border
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;               // keep foliage from thinning out in the distance
            ti.alphaTestReferenceValue = cutoff;
        }
        else
        {
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = false;
        }
    }

    void OnPreprocessAudio()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.StartsWith("Assets/Resources/Audio/")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.75f;
        ai.defaultSampleSettings = s;
        ai.loadInBackground = false;
    }
}
