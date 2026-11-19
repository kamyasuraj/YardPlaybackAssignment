// Material inspector for "Custom/Lit (Packed Mask)".
// Built on URP's BaseShaderGUI, so Surface Options (opaque/transparent, alpha clip, culling, shadows)
// and Advanced options behave exactly like URP Lit.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class PackedMaskLitGUI : BaseShaderGUI
{
    MaterialProperty mask, metallic, smoothness, occlusion, glossFromBlue, invertGloss, maskSrgb;
    MaterialProperty bumpMap, bumpScale, highlights, reflections, emissionEnabled;

    static readonly GUIContent MaskLabel = new GUIContent("Mask Map",
        "Packed mask: R = Metallic, G = Ambient Occlusion, A = Glossiness. sRGB or linear import both work.");
    static readonly GUIContent EmissionLabel = new GUIContent("Emission", "Makes the material glow. Emission = Emission Map x Color.");
    static readonly GUIContent EmissionMapLabel = new GUIContent("Emission Map", "Emission (RGB) and HDR colour / intensity.");
    static readonly string[] LitOnlyKeywords =
    {
        "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_SPECULAR_SETUP", "_SPECGLOSSMAP", "_PARALLAXMAP",
        "_DETAIL_MULX2", "_DETAIL_SCALED", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", "_CLEARCOAT", "_CLEARCOATMAP"
    };

    public override void FindProperties(MaterialProperty[] props)
    {
        base.FindProperties(props);
        mask            = FindProperty("_MetallicGlossMap", props, false);
        metallic        = FindProperty("_Metallic", props, false);
        smoothness      = FindProperty("_Smoothness", props, false);
        occlusion       = FindProperty("_OcclusionStrength", props, false);
        glossFromBlue   = FindProperty("_GlossFromBlue", props, false);
        invertGloss     = FindProperty("_InvertGloss", props, false);
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
        SetMaterialKeywords(material, SetPackedKeywords);
        ApplyEmission(material);   // after URP's pass, which would otherwise decide emission from GI flags alone
    }

    static void SetPackedKeywords(Material m)
    {
        var maskTex = m.GetTexture("_MetallicGlossMap");
        bool hasMask = maskTex != null;
        if (hasMask && m.HasProperty("_MaskSRGB"))
            m.SetFloat("_MaskSRGB", IsSrgb(maskTex) ? 1f : 0f);    // always follow the texture's import setting
        CoreUtils.SetKeyword(m, "_MASKMAP", hasMask);
        CoreUtils.SetKeyword(m, "_GLOSS_FROM_BLUE", hasMask && m.GetFloat("_GlossFromBlue") > 0.5f);
        CoreUtils.SetKeyword(m, "_INVERT_GLOSS", hasMask && m.GetFloat("_InvertGloss") > 0.5f);
        CoreUtils.SetKeyword(m, "_MASK_SRGB", hasMask && m.HasProperty("_MaskSRGB") && m.GetFloat("_MaskSRGB") > 0.5f);
        CoreUtils.SetKeyword(m, "_NORMALMAP", m.GetTexture("_BumpMap") != null);
        CoreUtils.SetKeyword(m, "_SPECULARHIGHLIGHTS_OFF", m.GetFloat("_SpecularHighlights") == 0f);
        CoreUtils.SetKeyword(m, "_ENVIRONMENTREFLECTIONS_OFF", m.GetFloat("_EnvironmentReflections") == 0f);
        foreach (var k in LitOnlyKeywords) m.DisableKeyword(k);   // leftovers if the material came from URP Lit
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
        if (state < 0f)   // first time this material is seen: keep whatever it had before
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
            smoothness.floatValue = 1f;
            occlusion.floatValue = 1f;
        }

        bool hasMask = mask.textureValue != null;
        EditorGUI.indentLevel += 2;
        materialEditor.ShaderProperty(metallic, hasMask ? "Metallic  (x R)" : "Metallic");
        materialEditor.ShaderProperty(smoothness, hasMask ? "Glossiness  (x A)" : "Glossiness");
        if (hasMask)
        {
            materialEditor.ShaderProperty(occlusion, "AO Strength  (G)");
            materialEditor.ShaderProperty(glossFromBlue, "Gloss In Blue Channel");
            materialEditor.ShaderProperty(invertGloss, "Mask Stores Roughness");
        }
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
            maskSrgb.floatValue = srgb ? 1f : 0f;   // keeps the keyword in step if the import setting changes
        using (new EditorGUI.IndentLevelScope(2))
            EditorGUILayout.LabelField(srgb ? "Mask is sRGB: values are read exactly as painted." : "Mask is linear: values are read exactly as painted.",
                                       EditorStyles.miniLabel);
        if (!ti.DoesSourceTextureHaveAlpha() && glossFromBlue.floatValue < 0.5f)
            EditorGUILayout.HelpBox("This mask has no alpha channel (JPG, or exported without alpha), so glossiness would read as fully glossy. " +
                                    "Re-export as PNG/TGA with gloss in alpha, or tick 'Gloss In Blue Channel' if gloss is in B.", MessageType.Warning);
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
                    emissionColorProp.colorValue = Color.white;   // black colour = no glow, so start at white
                if (on)
                    foreach (var t in materialEditor.targets)
                        if (t is Material tm && (tm.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) == 0)
                            tm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;   // glows into baked lighting too
            }
        }
        else on = materialEditor.EmissionEnabledProperty();

        using (new EditorGUI.DisabledScope(!on))
        using (new EditorGUI.IndentLevelScope(2))
        {
            bool hadMap = emissionMapProp.textureValue != null;
            materialEditor.TexturePropertyWithHDRColor(EmissionMapLabel, emissionMapProp, emissionColorProp, false);
            if (!hadMap && emissionMapProp.textureValue != null && emissionColorProp.colorValue.maxColorComponent <= 0f)
                emissionColorProp.colorValue = Color.white;      // map just assigned while colour was black
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
        base.AssignNewShaderToMaterial(material, oldShader, newShader);   // keeps Base/Normal/Emission maps & colours
        if (material.HasProperty("_EmissionEnabled")) material.SetFloat("_EmissionEnabled", hadEmission ? 1f : 0f);
        SetupMaterialBlendMode(material);
        ValidateMaterial(material);
    }
}
