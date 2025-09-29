
using UnityEngine;
using HarmonyLib;
using UnityEngine.Rendering;

namespace UnderTheSea.Patches;

[HarmonyPatch]
internal static class WaterCameraPatches
{
    private static float WaterLevelCamera = 0f;
    private static float WaterLevelPlayer = 0f;
    private static bool ShouldResetCamera = false;
    private const float UnderWaterCameraMinWaterDistance = -1000f;
    private static float? CachedMinWaterDistance = null;

    /// <summary>
    ///     Note from 0.1.3: Changed minWaterDistance from -5000f to -1000f
    ///                      in an effort to reduce graphical glitches
    ///                      while swimming on the surface.
    ///     Sets m_minWaterDistance to -1000f and caches previous
    ///     value if there is not a cached value already.
    /// </summary>
    /// <param name="gameCamera"></param>
    private static void SetMinWaterDistanceUnderWater(GameCamera gameCamera)
    {
        if (!CachedMinWaterDistance.HasValue)
        {
            CachedMinWaterDistance = gameCamera.m_minWaterDistance;
        }
        gameCamera.m_minWaterDistance = UnderWaterCameraMinWaterDistance;
    }

    /// <summary>
    ///     Resets m_minWatetDistance to the cached value if it 
    ///     is not null and sets cached value to null.
    /// </summary>
    /// <param name="gameCamera"></param>
    private static void ResetMinWaterDistance(GameCamera gameCamera)
    {
        if (CachedMinWaterDistance.HasValue)
        {
            gameCamera.m_minWaterDistance = CachedMinWaterDistance.Value;
            CachedMinWaterDistance = null;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
    private static void GameCamera_UpdateCamera_Prefix(GameCamera __instance)
    {
        if (!Utils.TryGetDiver(Player.m_localPlayer, out Diver diver) || !__instance)
        {
            return;
        }

        Camera camera = __instance.m_camera;
        bool isMovingInWater = diver.IsDiving() || diver.player.IsSwimming();

        if (isMovingInWater && !diver.IsRestingInWater() && UnderTheSea.Instance.IsEnvAllowed())
        {
            SetMinWaterDistanceUnderWater(__instance);
        }
        else
        {
            ResetMinWaterDistance(__instance);
        }

        if (camera.gameObject.transform.position.y < WaterLevelCamera && isMovingInWater && UnderTheSea.Instance.IsEnvAllowed())
        {
            if (__instance.m_minWaterDistance != UnderWaterCameraMinWaterDistance)
            {
                SetMinWaterDistanceUnderWater(__instance);
            }

            EnvSetup currentEnv = EnvMan.instance.GetCurrentEnvironment();
            Color waterColor = !EnvMan.IsNight() ? currentEnv.m_fogColorDay : currentEnv.m_fogColorNight;
            waterColor.a = 1f;

            waterColor = ChangeColorBrightness(waterColor, diver.player.m_swimDepth * -UnderTheSea.Instance.ColorDarknessFactor.Value);
            RenderSettings.fogColor = waterColor;

            float fogDensity = RenderSettings.fogDensity + (diver.player.m_swimDepth * UnderTheSea.Instance.FogDensityFactor.Value);
            fogDensity = Mathf.Clamp(fogDensity, UnderTheSea.Instance.MinFogDensity.Value, UnderTheSea.Instance.MaxFogDensity.Value);
            RenderSettings.fogDensity = fogDensity;

            ShouldResetCamera = true;
        }
        else if (ShouldResetCamera && camera.gameObject.transform.position.y > WaterLevelCamera)
        {
            EnvMan.instance.SetForceEnvironment(EnvMan.instance.GetCurrentEnvironment().m_name);
            EnvMan.instance.SetForceEnvironment(string.Empty);
            ShouldResetCamera = false;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaterVolume), nameof(WaterVolume.UpdateMaterials))]
    private static void WaterVolume_UpdateMaterials_Prefix(WaterVolume __instance)
    {
        if (!GameCamera.instance || !Player.m_localPlayer)
        {
            return;
        }
    
        WaterLevelCamera = __instance.GetWaterSurface(GameCamera.instance.transform.position);
        WaterLevelPlayer = __instance.GetWaterSurface(Player.m_localPlayer.transform.position);
        MeshRenderer meshRenderer = __instance.m_waterSurface.GetComponent<MeshRenderer>();

        if (GameCamera.instance.transform.position.y < WaterLevelCamera && Player.m_localPlayer.IsSwimming())
        {
            if (meshRenderer.transform.rotation.eulerAngles.y != 180f && UnderTheSea.Instance.IsEnvAllowed())
            {
                __instance.m_waterSurface.transform.Rotate(180f, 0f, 0f);
                __instance.m_waterSurface.shadowCastingMode = ShadowCastingMode.TwoSided;
                if (__instance.m_forceDepth >= 0f)
                {
                    __instance.m_waterSurface.material.SetFloatArray(
                        Shader.PropertyToID("_depth"),
                        new float[4] { __instance.m_forceDepth, __instance.m_forceDepth, __instance.m_forceDepth, __instance.m_forceDepth }
                    );
                }
                else
                {
                    __instance.m_waterSurface.material.SetFloatArray(Shader.PropertyToID("_depth"), __instance.m_normalizedDepth);
                }
                __instance.m_waterSurface.material.SetFloat(Shader.PropertyToID("_UseGlobalWind"), __instance.m_useGlobalWind ? 1f : 0f);
            }

            // Move water surface
            Transform waterSurfaceTransfrom = __instance.m_waterSurface.transform;
            Vector3 waterSurfacePosition = waterSurfaceTransfrom.position;
            waterSurfacePosition = new Vector3(waterSurfacePosition.x, WaterLevelCamera, waterSurfacePosition.z);
            waterSurfaceTransfrom.position = waterSurfacePosition;
        }
        else if (meshRenderer.transform.rotation.eulerAngles.y == 180f && UnderTheSea.Instance.IsEnvAllowed())
        {
            __instance.m_waterSurface.transform.Rotate(-180f, 0f, 0f);
            if (__instance.m_forceDepth >= 0f)
            {
                __instance.m_waterSurface.material.SetFloatArray(
                    Shader.PropertyToID("_depth"), 
                    new float[4] { __instance.m_forceDepth, __instance.m_forceDepth, __instance.m_forceDepth, __instance.m_forceDepth }
                );
            }
            else
            {
                __instance.m_waterSurface.material.SetFloatArray(Shader.PropertyToID("_depth"), __instance.m_normalizedDepth);
            }

            Transform waterSurfaceTransfrom = __instance.m_waterSurface.transform;
            Vector3 waterSurfacePosition = waterSurfaceTransfrom.position;
            waterSurfacePosition = new Vector3(waterSurfacePosition.x, 30f, waterSurfacePosition.z);
            waterSurfaceTransfrom.position = waterSurfacePosition;
            __instance.m_waterSurface.material.SetFloat(Shader.PropertyToID("_UseGlobalWind"), __instance.m_useGlobalWind ? 1f : 0f);
        }
    }


    private static Color ChangeColorBrightness(Color color, float correctionFactor)
    {
        float red = color.r;
        float green = color.g;
        float blue = color.b;
        if (!(correctionFactor < 0f))
        {
            return new Color(red, green, blue, color.a);
        }
        correctionFactor *= -1f;

        red -= red * correctionFactor;
        if (red < 0f)
        {
            red = 0f;
        }

        green -= green * correctionFactor;
        if (green < 0f)
        {
            green = 0f;
        }

        blue -= blue * correctionFactor;
        if (blue < 0f)
        {
            blue = 0f;
        }
        return new Color(red, green, blue, color.a);
    }
}
