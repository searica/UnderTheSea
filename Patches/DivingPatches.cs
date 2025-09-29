using HarmonyLib;
using UnityEngine;

namespace UnderTheSea.Patches;

[HarmonyPatch]
internal static class DivingPatches
{

    private static bool InUpdateSwimming = false;
    //TODO: make sure stamina is drained slowly whenever underwater

    /// <summary>
    ///     Modify swim depth of local player and add Diver componenet.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.Awake))]
    private static void Player_Awake_Prefix(Player __instance)
    {
        __instance.gameObject.AddComponent<Diver>();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.UpdateMotion))]
    private static void Character_UpdateMotion_Prefix(Character __instance)
    {
        if (!Utils.TryGetDiver(__instance, out Diver diver) || !UnderTheSea.Instance.IsEnvAllowed())
        {
            return;
        }

        diver.ResetSwimDepthIfNotInWater();

        // Bug fix for swimming on land glitch - originally __instance.m_swimDepth > 2.5f
        if (diver.IsUnderSurface() && diver.IsInsideLiquid())
        {
            diver.player.m_lastGroundTouch = 0.3f;
            diver.player.m_swimTimer = 0f;
        }
    }

    /// <summary>
    ///     Update swim speed based on how long sprint key has been held.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="dt"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.UpdateSwimming))]
    public static void UpdateSwimming_Prefix(Character __instance, float dt, out Vector3? __state)
    {
        InUpdateSwimming = true;
        __state = null;
        if (!Utils.TryGetDiver(__instance, out Diver diver))
        {
            return;
        }
 
        diver.UpdateSwimSpeed(dt);
        if ((ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump")) && diver.IsUnderSurface())
        {
            diver.Dive(dt, ascend: true, out __state);
        }
        else if ((ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch")) && diver.CanDive())
        {
            diver.Dive(dt, ascend: false, out __state);
        }
        else if ((__instance.IsOnGround() || !diver.IsDiving()) && !diver.IsRestingInWater())
        {
            diver.ResetSwimDepthToDefault();
        }
    }

    /// <summary>
    ///     Reset player.m_moveDir after diving to avoid causing issues elsewhere due to the non-zero y-component.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="dt"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.UpdateSwimming))]
    public static void UpdateSwimming_Postfix(Character __instance, float dt, ref Vector3? __state)
    {
        InUpdateSwimming = false;
        if (__state.HasValue)
        {
            __instance.m_moveDir = __state.Value;
            __state = null;
        }
    }

    /// <summary>
    ///     Cache player rotation before UpdateRotation and pass to postfix to allow 
    ///     checking if rotation occured and avoid duplicating rotations.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.UpdateRotation))]
    public static void UpdateRotation_Postfix(Character __instance, out Quaternion? __state)
    {

        if (InUpdateSwimming)
        {
            __state = __instance.transform.rotation;
        }
        else
        {
            __state = null;
        }
    }

    /// <summary>
    ///     Allow 3D rotation towards movement direction when under the surface of a liquid during UpdateSwimming.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="turnSpeed"></param>
    /// <param name="dt"></param>
    /// <param name="smooth"></param>
    [HarmonyPostfix]    
    [HarmonyPatch(typeof(Character), nameof(Character.UpdateRotation))]
    public static void UpdateRotation_Postfix(Character __instance, float turnSpeed, float dt, ref Quaternion? __state)
    {
        // Should not update rotation.
        if (!InUpdateSwimming || !__state.HasValue || !__instance)
        {
            return;
        }

        // Rotation was already updated.
        if (__instance.transform.rotation != __state.Value)
        {
            return;
        }

        // Not under currently diving or surfacing so don't update.
        if (!Utils.TryGetDiver(__instance, out Diver diver) || !diver.IsUnderSurface())
        {
            return;
        }

        // Update rotation to reflect diving direction.
        Player player = diver.player;
        Quaternion quaternion = (player.AlwaysRotateCamera() || player.m_moveDir == Vector3.zero) ? player.m_lookYaw : Quaternion.LookRotation(player.m_moveDir);
        float effectiveSpeed = turnSpeed * player.GetAttackSpeedFactorRotation();
        player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, quaternion, effectiveSpeed * dt);
    }

    /// <summary>
    ///     Handle updating swim skill and stamina when diving or resting in water.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="targetVel"></param>
    /// <param name="dt"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnSwimming))]
    public static void Player_OnSwimming_Prefix(Player __instance, Vector3 targetVel, float dt)
    {
        if (targetVel.magnitude >= 0.1f || !Utils.TryGetDiver(__instance, out Diver diver))
        {
            return;
        }

        if (diver.IsDiving())
        {
            diver.DrainDivingStamina(dt);
            diver.UpdateSwimSkill(dt);
        }
        else if (diver.IsRestingInWater())
        {
            diver.RegenRestingStamina(dt);
        }
    }

    /// <summary>
    ///     Prevent crouching while swimming.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    private static void PreventCrouchWhileSwimming(Player __instance, ref bool crouch)
    {
        if (!Utils.IsValidLocalPlayer(__instance))
        {
            return;
        }
        if (__instance.IsSwimming())
        {
            crouch = false;
        }
    }
}
