#ifndef PACKED_MASK_LIT_INPUT_INCLUDED
#define PACKED_MASK_LIT_INPUT_INCLUDED

// Reuse URP's own Lit inputs so the material CBUFFER is identical to URP Lit (SRP Batcher compatible,
// DOTS instancing safe). Only the surface setup is swapped for one that reads a single packed mask.
#define InitializeStandardLitSurfaceData InitializeStandardLitSurfaceData_URPLit
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"   // LinearToSRGB

// Packed mask (assigned to the _MetallicGlossMap slot; sRGB or linear import both work - see _MASK_SRGB):
//   R = Metallic     (x _Metallic)
//   G = AO           (blended by _OcclusionStrength)
//   A = Glossiness   (x _Smoothness)   - or B with _GLOSS_FROM_BLUE, inverted with _INVERT_GLOSS
// Returns half3(metallic, occlusion, smoothness).
half3 SamplePackedMask(float2 uv)
{
#if defined(_MASKMAP)
    half4 mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
    #if defined(_MASK_SRGB)
        // Mask imported with sRGB on: the sampler decoded R/G/B as colour. Re-encode so metal/AO/B are
        // exactly the values that were painted. (Alpha is never sRGB-decoded, so gloss in A is already exact.)
        mask.rgb = LinearToSRGB(mask.rgb);
    #endif
    #if defined(_GLOSS_FROM_BLUE)
        half gloss = mask.b;
    #else
        half gloss = mask.a;
    #endif
    #if defined(_INVERT_GLOSS)
        gloss = half(1.0) - gloss;
    #endif
    return half3(mask.r * _Metallic, LerpWhiteTo(mask.g, _OcclusionStrength), gloss * _Smoothness);
#else
    return half3(_Metallic, half(1.0), _Smoothness);
#endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

    half3 mos = SamplePackedMask(uv);
    outSurfaceData.metallic   = mos.x;
    outSurfaceData.specular   = half3(0.0, 0.0, 0.0);
    outSurfaceData.occlusion  = mos.y;
    outSurfaceData.smoothness = mos.z;

    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
}

#endif
