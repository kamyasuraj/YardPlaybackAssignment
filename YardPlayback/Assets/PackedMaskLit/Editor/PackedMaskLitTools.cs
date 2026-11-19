// Packed Mask Lit - editor helpers.
//   Assets > Convert To Packed Mask Lit                 (right-click selected materials)
//   Tools > Packed Mask Lit > Check Shader
//   Tools > Packed Mask Lit > Regenerate From Installed URP
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class PackedMaskLitTools
{
    public const string ShaderName = "Custom/Lit (Packed Mask)";
    const string UrpPackage = "com.unity.render-pipelines.universal";

    // ---------------- convert ----------------
    [MenuItem("Assets/Convert To Packed Mask Lit", false, 2000)]
    static void ConvertSelected()
    {
        var shader = Shader.Find(ShaderName);
        if (!shader) { Debug.LogError($"[PackedMaskLit] Shader '{ShaderName}' not found or failed to compile."); return; }
        foreach (var m in Selection.objects.OfType<Material>())
        {
            Undo.RecordObject(m, "Convert to Packed Mask Lit");
            m.shader = shader;
            EditorUtility.SetDirty(m);
            Debug.Log($"[PackedMaskLit] Converted {m.name}. Assign your packed mask (R metal, G AO, A gloss) in the Mask Map slot.", m);
        }
    }
    [MenuItem("Assets/Convert To Packed Mask Lit", true)]
    static bool ConvertSelectedValidate() => Selection.objects.OfType<Material>().Any();

    // ---------------- check ----------------
    [MenuItem("Tools/Packed Mask Lit/Check Shader")]
    static void Check()
    {
        var shader = Shader.Find(ShaderName);
        if (!shader) { Debug.LogError($"[PackedMaskLit] Shader '{ShaderName}' not found."); return; }
        var msgs = ShaderUtil.GetShaderMessages(shader);
        foreach (var msg in msgs) Debug.Log($"[PackedMaskLit] {msg.severity}: {msg.message} (line {msg.line}, {msg.file})");
        if (ShaderUtil.ShaderHasError(shader))
            Debug.LogError("[PackedMaskLit] Shader has errors. If your URP version differs from 17.4, run Tools > Packed Mask Lit > Regenerate From Installed URP.");
        else
            Debug.Log("[PackedMaskLit] Shader compiled OK.");
        Selection.activeObject = shader;
    }

    // ---------------- regenerate ----------------
    // Rebuilds PackedMaskLit.shader from the URP Lit.shader installed in THIS project, so the passes and
    // keywords always match your URP version. Only the surface inputs (PackedMaskLitInput.hlsl) are swapped.
    [MenuItem("Tools/Packed Mask Lit/Regenerate From Installed URP")]
    static void Regenerate()
    {
        var urp = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{UrpPackage}/package.json");
        if (urp == null) { Debug.LogError("[PackedMaskLit] URP package not found in this project."); return; }
        string litPath = Path.Combine(urp.resolvedPath, "Shaders", "Lit.shader");
        if (!File.Exists(litPath)) { Debug.LogError($"[PackedMaskLit] Couldn't find {litPath}"); return; }

        string target = ShaderAssetPath();
        if (target == null) { Debug.LogError("[PackedMaskLit] Couldn't locate PackedMaskLit.shader / PackedMaskLitInput.hlsl in the project."); return; }

        string s = File.ReadAllText(litPath);
        s = s.Replace("Shader \"Universal Render Pipeline/Lit\"",
            "// URP Lit with a single packed mask map:  R = Metallic,  G = Ambient Occlusion,  A = Glossiness\n" +
            $"// Regenerated from URP {urp.version} Lit.shader by Tools > Packed Mask Lit. Surface inputs: PackedMaskLitInput.hlsl\n" +
            $"Shader \"{ShaderName}\"");
        s = s.Replace("_Smoothness(\"Smoothness\", Range(0.0, 1.0)) = 0.5", "_Smoothness(\"Glossiness\", Range(0.0, 1.0)) = 0.5");
        s = s.Replace("_SmoothnessTextureChannel(\"Smoothness texture channel\", Float) = 0",
                      "[HideInInspector] _SmoothnessTextureChannel(\"Smoothness texture channel\", Float) = 0");
        s = s.Replace("_MetallicGlossMap(\"Metallic\", 2D) = \"white\" {}",
            "[NoScaleOffset] _MetallicGlossMap(\"Mask (R Metallic, G AO, A Gloss)\", 2D) = \"white\" {}\n" +
            "        [Toggle(_GLOSS_FROM_BLUE)] _GlossFromBlue(\"Gloss In Blue Channel\", Float) = 0.0\n" +
            "        [Toggle(_INVERT_GLOSS)] _InvertGloss(\"Mask Stores Roughness\", Float) = 0.0\n" +
            "        [HideInInspector][Toggle(_MASK_SRGB)] _MaskSRGB(\"Mask Imported As sRGB\", Float) = 0.0");
        s = s.Replace("[HDR] _EmissionColor(\"Color\", Color) = (0,0,0)",
            "[HDR] _EmissionColor(\"Color\", Color) = (0,0,0)\n" +
            "        [HideInInspector] _EmissionEnabled(\"Emission Enabled\", Float) = -1.0");
        foreach (var p in new[] { "_SpecColor(\"Specular\", Color)", "_SpecGlossMap(\"Specular\", 2D)", "_Parallax(\"Scale\"",
                                  "_ParallaxMap(\"Height Map\"", "_OcclusionMap(\"Occlusion\"", "_DetailMask(\"Detail Mask\"",
                                  "_DetailAlbedoMapScale(\"Scale\"", "_DetailAlbedoMap(\"Detail Albedo x2\"",
                                  "_DetailNormalMapScale(\"Scale\"", "[Normal] _DetailNormalMap(", "_WorkflowMode(\"WorkflowMode\"" })
            s = s.Replace(p, "[HideInInspector] " + p);
        foreach (var kw in new[] { "#pragma shader_feature_local _PARALLAXMAP", "#pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED",
                                   "#pragma shader_feature_local_fragment _OCCLUSIONMAP", "#pragma shader_feature_local_fragment _SPECULAR_SETUP",
                                   "#pragma shader_feature_local_fragment _SPECGLOSSMAP",
                                   "#pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                                   "#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A" })
            s = Regex.Replace(s, @"[ \t]*" + Regex.Escape(kw) + @"[ \t]*\r?\n", "");
        s = s.Replace("#pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP",
            "#pragma shader_feature_local_fragment _MASKMAP\n" +
            "            #pragma shader_feature_local_fragment _GLOSS_FROM_BLUE\n" +
            "            #pragma shader_feature_local_fragment _INVERT_GLOSS\n" +
            "            #pragma shader_feature_local_fragment _MASK_SRGB");
        s = s.Replace("#include \"Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl\"", "#include \"PackedMaskLitInput.hlsl\"");
        s = s.Replace("CustomEditor \"UnityEditor.Rendering.Universal.ShaderGUI.LitShader\"", "CustomEditor \"PackedMaskLitGUI\"");

        File.WriteAllText(target, s);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[PackedMaskLit] Regenerated {target} from URP {urp.version}.");
        Check();
    }

    static string ShaderAssetPath()
    {
        var existing = Shader.Find(ShaderName);
        if (existing) { var p = AssetDatabase.GetAssetPath(existing); if (!string.IsNullOrEmpty(p)) return p; }
        var hlsl = AssetDatabase.FindAssets("PackedMaskLitInput").Select(AssetDatabase.GUIDToAssetPath)
                                .FirstOrDefault(p => p.EndsWith("PackedMaskLitInput.hlsl"));
        return hlsl == null ? null : Path.Combine(Path.GetDirectoryName(hlsl), "PackedMaskLit.shader").Replace('\\', '/');
    }
}
