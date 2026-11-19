// Material inspector for "Custom/Lit (Metal AO Roughness)".
// Built on URP's BaseShaderGUI, so Surface Options (opaque/transparent, alpha clip, culling, shadows)
// and Advanced options behave exactly like URP Lit.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class MARLitGUI : BaseShaderGUI
{
    MaterialProperty mask, metallic, roughness, occlusion, maskSrgb;
    MaterialProperty bumpMap, bumpScale, highlights, reflections, emissionEnabled;

    static readonly GUIContent MaskLabel = new GUIContent("Mask Map",
        "Packed mask: R = Metallic, G = Ambient Occlusion, B = Roughness. sRGB or linear import both work.");
    static readonly GUIContent EmissionLabel = new GUIContent("Emission", "Makes the material glow. Emission = Emission Map x Color.");
    static readonly GUIContent EmissionMapLabel = new GUIContent("Emission Map", "Emission (RGB) and HDR colour / intensity.");
    static readonly string[] LitOnlyKeywords =
    {
        "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_SPECULAR_SETUP", "_SPECGLOSSMAP", "_PARALLAXMAP",
        "_DETAIL_MULX2", "_DETAIL_SCALED", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_CLEARCOAT", "_CLEARCOATMAP",
        "_GLOSS_FROM_BLUE", "_INVERT_GLOSS"
    };

    public override void FindProperties(MaterialProperty[] props)
    {
        base.FindProperties(props);
        mask            = FindProperty("_MetallicGlossMap", props, false);
        metallic        = FindProperty("_Metallic", props, false);
        roughness       = FindProperty("_Smoothness", props, false);   // stores ROUGHNESS in this shader
        occlusion       = FindProperty("_OcclusionStrength", props, false);
        maskSrgb        = FindProperty("_MaskSRGB", props, false);
        bumpMap         = FindProperty("_BumpMap", props, false);
        bumpScale       = FindProperty("_BumpScale", props, false);
        highlights      = FindProperty("_SpecularHighlights", props, false);
        reflections     = FindProperty("_EnvironmentReflections", props, false);
        emissionEnabled = FindProperty("_EmissionEnabled", props, false);
    }

    // ---------------- keywords ----------------
    public override void ValidateMaterial(Material material)
    {
        SetMaterialKeywords(material, SetMARKeywords);
        ApplyEmission(material);
    }

    static void SetMARKeywords(Material m)
    {
        var maskTex = m.GetTexture("_MetallicGlossMap");
        bool hasMask = maskTex != null;
        if (hasMask && m.HasProperty("_MaskSRGB"))
            m.SetFloat("_MaskSRGB", IsSrgb(maskTex) ? 1f : 0f);
        CoreUtils.SetKeyword(m, "_MASKMAP", hasMask);
        CoreUtils.SetKeyword(m, "_MASK_SRGB", hasMask && m.HasProperty("_MaskSRGB") && m.GetFloat("_MaskSRGB") > 0.5f);
        CoreUtils.SetKeyword(m, "_NORMALMAP", m.GetTexture("_BumpMap") != null);
        CoreUtils.SetKeyword(m, "_SPECULARHIGHLIGHTS_OFF", m.GetFloat("_SpecularHighlights") == 0f);
        CoreUtils.SetKeyword(m, "_ENVIRONMENTREFLECTIONS_OFF", m.GetFloat("_EnvironmentReflections") == 0f);
        foreach (var k in LitOnlyKeywords) m.DisableKeyword(k);
    }

    static bool IsSrgb(Texture tex)
    {
        var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
        return ti != null && ti.sRGBTexture;
    }

    static void ApplyEmission(Material m)
    {
        if (!m.HasProperty("_EmissionEnabled")) return;
        float state = m.GetFloat("_EmissionEnabled");
        if (state < 0f)
        {
            bool had = m.IsKeywordEnabled("_EMISSION") || m.GetColor("_EmissionColor").maxColorComponent > 0f;
            state = had ? 1f : 0f;
            m.SetFloat("_EmissionEnabled", state);
        }
        bool on = state > 0.5f;
        if (!on)
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        MaterialEditor.FixupEmissiveFlag(m);
        CoreUtils.SetKeyword(m, "_EMISSION", on);
    }

    // ---------------- GUI ----------------
    public override void DrawSurfaceInputs(Material material)
    {
        base.DrawSurfaceInputs(material);   // Base Map + Color

        bool hadMask = mask.textureValue != null;
        EditorGUI.BeginChangeCheck();
        materialEditor.TexturePropertySingleLine(MaskLabel, mask);
        if (EditorGUI.EndChangeCheck() && !hadMask && mask.textureValue != null)
        {
            // With a mask the sliders become multipliers, so start them at 1 (= use the map as authored).
            metallic.floatValue = 1f;
            roughness.floatValue = 1f;
            occlusion.floatValue = 1f;
        }

        bool hasMask = mask.textureValue != null;
        EditorGUI.indentLevel += 2;
        materialEditor.ShaderProperty(metallic, hasMask ? "Metallic  (x R)" : "Metallic");
        if (hasMask) materialEditor.ShaderProperty(occlusion, "AO Strength  (G)");
        materialEditor.ShaderProperty(roughness, hasMask ? "Roughness  (x B)" : "Roughness");
        EditorGUI.indentLevel -= 2;
        if (hasMask) MaskImportInfo(mask.textureValue);

        DrawNormalArea(materialEditor, bumpMap, bumpScale);
        DrawEmission(material);
        DrawTileOffset(materialEditor, baseMapProp);
    }

    void MaskImportInfo(Texture tex)
    {
        if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter ti)) return;
        bool srgb = ti.sRGBTexture;
        if (maskSrgb != null && (maskSrgb.floatValue > 0.5f) != srgb)
            maskSrgb.floatValue = srgb ? 1f : 0f;
        using (new EditorGUI.IndentLevelScope(2))
            EditorGUILayout.LabelField(srgb ? "Mask is sRGB: values are read exactly as painted." : "Mask is linear: values are read exactly as painted.",
                                       EditorStyles.miniLabel);
    }

    void DrawEmission(Material material)
    {
        if (emissionMapProp == null || emissionColorProp == null) return;

        bool on;
        if (emissionEnabled != null)
        {
            EditorGUI.showMixedValue = emissionEnabled.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            on = EditorGUILayout.Toggle(EmissionLabel, emissionEnabled.floatValue > 0.5f);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                emissionEnabled.floatValue = on ? 1f : 0f;
                if (on && emissionColorProp.colorValue.maxColorComponent <= 0f)
                    emissionColorProp.colorValue = Color.white;
                if (on)
                    foreach (var t in materialEditor.targets)
                        if (t is Material tm && (tm.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) == 0)
                            tm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
        }
        else on = materialEditor.EmissionEnabledProperty();

        using (new EditorGUI.DisabledScope(!on))
        using (new EditorGUI.IndentLevelScope(2))
        {
            bool hadMap = emissionMapProp.textureValue != null;
            materialEditor.TexturePropertyWithHDRColor(EmissionMapLabel, emissionMapProp, emissionColorProp, false);
            if (!hadMap && emissionMapProp.textureValue != null && emissionColorProp.colorValue.maxColorComponent <= 0f)
                emissionColorProp.colorValue = Color.white;
        }
        if (on)
            materialEditor.LightmapEmissionFlagsProperty(MaterialEditor.kMiniTextureFieldLabelIndentLevel, true);
    }

    public override void DrawAdvancedOptions(Material material)
    {
        if (highlights != null && reflections != null)
        {
            materialEditor.ShaderProperty(highlights, "Specular Highlights");
            materialEditor.ShaderProperty(reflections, "Environment Reflections");
        }
        base.DrawAdvancedOptions(material);
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        bool hadEmission = material.IsKeywordEnabled("_EMISSION");
        // Read the old shader's smoothness before the switch: this shader stores roughness in that slot.
        float oldSmooth = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness")
                        : material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
        bool cameFromMAR = oldShader != null && oldShader.name == MARLitTools.ShaderName;

        base.AssignNewShaderToMaterial(material, oldShader, newShader);   // keeps Base/Normal/Emission maps & colours

        if (!cameFromMAR)
        {
            bool hasMask = material.GetTexture("_MetallicGlossMap") != null;
            if (hasMask) { material.SetFloat("_Smoothness", 1f); material.SetFloat("_Metallic", 1f); material.SetFloat("_OcclusionStrength", 1f); }
            else material.SetFloat("_Smoothness", 1f - oldSmooth);
        }
        if (material.HasProperty("_EmissionEnabled")) material.SetFloat("_EmissionEnabled", hadEmission ? 1f : 0f);
        SetupMaterialBlendMode(material);
        ValidateMaterial(material);
    }
}
