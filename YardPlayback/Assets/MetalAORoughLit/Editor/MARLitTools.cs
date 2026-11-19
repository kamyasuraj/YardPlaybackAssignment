// Metal AO Roughness Lit - editor helpers.
//   Assets > Convert To Metal AO Roughness Lit          (right-click selected materials)
//   Tools > Metal AO Roughness Lit > Check Shader
//   Tools > Metal AO Roughness Lit > Regenerate From Installed URP
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class MARLitTools
{
    public const string ShaderName = "Custom/Lit (Metal AO Roughness)";
    const string UrpPackage = "com.unity.render-pipelines.universal";
    const string Tag = "[MARLit]";

    // ---------------- convert ----------------
    [MenuItem("Assets/Convert To Metal AO Roughness Lit", false, 2001)]
    static void ConvertSelected()
    {
        var shader = Shader.Find(ShaderName);
        if (!shader) { Debug.LogError($"{Tag} Shader '{ShaderName}' not found or failed to compile."); return; }
        foreach (var m in Selection.objects.OfType<Material>())
        {
            Undo.RecordObject(m, "Convert to Metal AO Roughness Lit");
            if (m.shader == shader) continue;
            float oldSmooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.5f;
            bool hadEmission = m.IsKeywordEnabled("_EMISSION");
            m.shader = shader;   // inspector's AssignNewShaderToMaterial isn't called here, so mirror it:
            bool hasMask = m.GetTexture("_MetallicGlossMap") != null;
            if (hasMask) { m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f); m.SetFloat("_OcclusionStrength", 1f); }
            else m.SetFloat("_Smoothness", 1f - oldSmooth);          // smoothness -> roughness
            m.SetFloat("_EmissionEnabled", hadEmission ? 1f : 0f);
            new MARLitGUI().ValidateMaterial(m);                    // set keywords now, not on first inspect
            EditorUtility.SetDirty(m);
            Debug.Log($"{Tag} Converted {m.name}. Assign your mask (R metal, G AO, B roughness) in the Mask Map slot.", m);
        }
    }
    [MenuItem("Assets/Convert To Metal AO Roughness Lit", true)]
    static bool ConvertSelectedValidate() => Selection.objects.OfType<Material>().Any();

    // ---------------- check ----------------
    [MenuItem("Tools/Metal AO Roughness Lit/Check Shader")]
    static void Check()
    {
        var shader = Shader.Find(ShaderName);
        if (!shader) { Debug.LogError($"{Tag} Shader '{ShaderName}' not found."); return; }
        foreach (var msg in ShaderUtil.GetShaderMessages(shader))
            Debug.Log($"{Tag} {msg.severity}: {msg.message} (line {msg.line}, {msg.file})");
        if (ShaderUtil.ShaderHasError(shader))
            Debug.LogError($"{Tag} Shader has errors. If your URP version differs from 17.4, run Tools > Metal AO Roughness Lit > Regenerate From Installed URP.");
        else
            Debug.Log($"{Tag} Shader compiled OK.");
        Selection.activeObject = shader;
    }

    // ---------------- regenerate ----------------
    // Rebuilds MARLit.shader from the URP Lit.shader installed in THIS project, so passes and keywords
    // always match your URP version. Only the surface inputs (MARLitInput.hlsl) are swapped.
    [MenuItem("Tools/Metal AO Roughness Lit/Regenerate From Installed URP")]
    static void Regenerate()
    {
        var urp = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{UrpPackage}/package.json");
        if (urp == null) { Debug.LogError($"{Tag} URP package not found in this project."); return; }
        string litPath = Path.Combine(urp.resolvedPath, "Shaders", "Lit.shader");
        if (!File.Exists(litPath)) { Debug.LogError($"{Tag} Couldn't find {litPath}"); return; }

        string target = ShaderAssetPath();
        if (target == null) { Debug.LogError($"{Tag} Couldn't locate MARLit.shader / MARLitInput.hlsl in the project."); return; }

        string s = File.ReadAllText(litPath);
        s = s.Replace("Shader \"Universal Render Pipeline/Lit\"",
            "// URP Lit with one packed mask:  R = Metallic,  G = Ambient Occlusion,  B = Roughness\n" +
            $"// Regenerated from URP {urp.version} Lit.shader by Tools > Metal AO Roughness Lit. Surface inputs: MARLitInput.hlsl\n" +
            $"Shader \"{ShaderName}\"");
        s = s.Replace("_Smoothness(\"Smoothness\", Range(0.0, 1.0)) = 0.5", "_Smoothness(\"Roughness\", Range(0.0, 1.0)) = 0.5");
        s = s.Replace("_SmoothnessTextureChannel(\"Smoothness texture channel\", Float) = 0",
                      "[HideInInspector] _SmoothnessTextureChannel(\"Smoothness texture channel\", Float) = 0");
        s = s.Replace("_MetallicGlossMap(\"Metallic\", 2D) = \"white\" {}",
            "[NoScaleOffset] _MetallicGlossMap(\"Mask (R Metallic, G AO, B Roughness)\", 2D) = \"white\" {}\n" +
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
            "            #pragma shader_feature_local_fragment _MASK_SRGB");
        s = s.Replace("#include \"Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl\"", "#include \"MARLitInput.hlsl\"");
        s = s.Replace("CustomEditor \"UnityEditor.Rendering.Universal.ShaderGUI.LitShader\"", "CustomEditor \"MARLitGUI\"");

        File.WriteAllText(target, s);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
        Debug.Log($"{Tag} Regenerated {target} from URP {urp.version}.");
        Check();
    }

    static string ShaderAssetPath()
    {
        var existing = Shader.Find(ShaderName);
        if (existing) { var p = AssetDatabase.GetAssetPath(existing); if (!string.IsNullOrEmpty(p)) return p; }
        var hlsl = AssetDatabase.FindAssets("MARLitInput").Select(AssetDatabase.GUIDToAssetPath)
                                .FirstOrDefault(p => p.EndsWith("MARLitInput.hlsl"));
        return hlsl == null ? null : Path.Combine(Path.GetDirectoryName(hlsl), "MARLit.shader").Replace('\\', '/');
    }
}
