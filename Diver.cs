using System.Collections.Generic;
using UnityEngine;
using Logging;
using System;


namespace UnderTheSea;
internal class Diver : MonoBehaviour
{
    private const float Tol = 0.01f;
    private static readonly Dictionary<Player, Diver> Divers = [];

    private Vector3 LastDiveDirection = Vector3.zero;


    public enum DiveDirection
    {
        NEUTRAL = 0,
        UP = 1,
        DOWN = 2
    }

    
    public Player player;
    public float BaseSwimSpeed { get; private set; }    
    public const float DefaultSwimDepth = 1.4f;
    public const float DivingSwimDepth = 2.5f;
    //public const float SprintSwimAcceleration = 0.25f;  // m/s^2

    public float RestingStaminaRegenDelay => -UnderTheSea.Instance.RestingStaminaRegenDelay.Value * player.m_staminaRegenDelay;
    public float RestingStaminaRegenRate => UnderTheSea.Instance.RestingStaminaRegenRate.Value * player.m_staminaRegen;

    public void Awake()
    {
        player = GetComponent<Player>();    
        player.m_swimDepth = DefaultSwimDepth;
        BaseSwimSpeed = player.m_swimSpeed;
        Divers.Add(player, this);
    }


    /// <summary>
    ///     Reset m_swimDepth to default depth for swimming on surface if not in water.
    /// </summary>
    public void ResetSwimDepthIfNotInWater()
    {
        if (!player.InWater())
        {
            ResetSwimDepthToDefault();
        }
    }

    /// <summary>
    ///     Set m_swimDepth to default depth for swimming on surface.
    /// </summary>
    public void ResetSwimDepthToDefault()
    {
        player.m_swimDepth = DefaultSwimDepth;
    }

    /// <summary>
    ///     In water, swimmming, not on ground, and the ground is at least 1m below player.
    /// </summary>
    /// <returns></returns>
    public bool CanDive()
    {
        if (!player.InWater() || player.IsOnGround() || !player.IsSwimming())
        {
            return false;
        }

        if (player.GetGroundHeight(player.transform.position, out float height, out Vector3 _))
        {
            if (player.transform.position.y - height < 1f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Checks if player position is deeper inside a liquid than the default swim depth.
    /// </summary>
    /// <returns></returns>
    public bool IsInsideLiquid()
    {
        return Mathf.Max(0f, player.GetLiquidLevel() - player.transform.position.y) > DefaultSwimDepth;
    }

    /// <summary>
    ///     Boolean indicating if player is deeper than default swimming depth.
    /// </summary>
    public bool IsUnderSurface()
    {
        return player.m_swimDepth > DefaultSwimDepth;
    }

    /// <summary>
    ///     Player is deep enough underwater to be diving.
    /// </summary>
    /// <returns></returns>
    public bool IsDiving()
    {
        return player.m_swimDepth > DivingSwimDepth;
    }

    /// <summary>
    ///     Player is not deep enough to be diving but is below default swim depth.
    /// </summary>
    /// <returns></returns>
    public bool IsSurfacing()
    {
        return !IsDiving() && IsUnderSurface();
    }

    /// <summary>
    ///     Is Swimming in water without diving or moving.
    /// </summary>
    /// <returns></returns>
    public bool IsRestingInWater()
    {
        return !IsDiving() && player.IsSwimming() && player.GetVelocity().magnitude < 1.0f;
    }

    public void RegenRestingStamina(float dt)
    {
        player.m_staminaRegenTimer -= dt;

        if (player.GetStamina() < player.GetMaxStamina() && player.m_staminaRegenTimer <= RestingStaminaRegenDelay)
        {
            float skillFactor = player.m_skills.GetSkillFactor(Skills.SkillType.Swim);
            float regenSpeed = (1f + skillFactor) * RestingStaminaRegenRate;
            player.m_stamina = Mathf.Min(player.GetMaxStamina(), player.m_stamina + (regenSpeed * dt * Game.m_staminaRegenRate));
        }
    }

    public void DrainDivingStamina(float dt)
    {
        float skillFactor = player.m_skills.GetSkillFactor(Skills.SkillType.Swim);
        float num = Mathf.Lerp(player.m_swimStaminaDrainMinSkill, player.m_swimStaminaDrainMaxSkill, skillFactor);
        num += num * player.GetEquipmentSwimStaminaModifier();
        player.m_seman.ModifySwimStaminaUsage(num, ref num);
        player.UseStamina(dt * num * Game.m_moveStaminaRate * UnderTheSea.Instance.UnderwaterRestingStaminaDrainRate.Value);
    }

    /// <summary>
    ///     Accumulate swimming XP if diving
    /// </summary>
    public void UpdateSwimSkill(float dt)
    {
        player.m_swimSkillImproveTimer += dt;
        if (player.m_swimSkillImproveTimer > 1f)
        {
            player.m_swimSkillImproveTimer = 0f;
            player.RaiseSkill(Skills.SkillType.Swim);
        }
    }

    /// <summary>
    ///     Update swim speed based on whether the player is "running" while swimming.
    /// </summary>
    /// <param name="dt"></param>
    public void UpdateSwimSpeed(float dt)
    {
        float multiplier = (ZInput.GetButton("Run") || ZInput.GetButton("JoyRun")) ? dt : -dt;
        float swimSpeed = player.m_swimSpeed + (player.m_swimAcceleration * multiplier);
        player.m_swimSpeed = Mathf.Clamp(swimSpeed, BaseSwimSpeed, UnderTheSea.Instance.MaxSwimSpeed.Value);
    }

    /// <summary>
    ///     Change m_moveDir to dive and adjust m_swimDepth to allow for diving.
    /// </summary>
    public void Dive(float dt, bool ascend, out Vector3? defaultMoveDir)
    {
        defaultMoveDir = player.m_moveDir;

        // Get diving direction based on ascend/descending and current swim depth
        player.m_moveDir = GetDiveDirection(ascend);
        Vector3 diveVelocity = CalculateSwimVelocity();

        // if ascending y is positive but m_swimDepth needs to decrease.
        // if descending, y is negative but m_swimDepth needs to increase.
        float newDepth = player.m_swimDepth - (diveVelocity.y * dt);

        // clamp to prevent reducing swim depth below default value
        player.m_swimDepth = Mathf.Max(newDepth, DefaultSwimDepth);
    }

    /// <summary>
    ///     Get move direction for diving.
    /// </summary>
    /// <param name="ascend">Whether diver is ascending or descending.</param>
    /// <returns></returns>
    public Vector3 GetDiveDirection(bool ascend)
    {
        Vector3 verticalDir = ascend ? Vector3.up : Vector3.down;
        Vector3 horizontalDir = player.m_moveDir;
    
        // Provide an horizontal direction when not holding movement keys
        // this is to help with animations that rotate the characters movement up or down.
        if (horizontalDir.magnitude < 0.1f)
        {
            //  make the direction more horizontal if ascending and surfacing
            float scale = ascend && IsSurfacing() ? 0.6f : 0.05f;
            horizontalDir = GetHorizontalLookDir(scale);
        }

        Vector3 diveDir = horizontalDir + verticalDir;
        diveDir.Normalize();
        return diveDir;
    }
    
    private Vector3 GetHorizontalLookDir(float scale = 0.05f)
    {
        Vector3 horiztonalDir = player.m_lookDir;
        horiztonalDir.y = 0f;
        horiztonalDir.Normalize();
        return horiztonalDir * scale;
    }

    /// <summary>
    ///     Calcuate adjusted swim velocity for player based on current m_moveDir.
    /// </summary>
    /// <returns></returns>
    public Vector3 CalculateSwimVelocity()
    {
        float speed = player.m_swimSpeed * player.GetAttackSpeedFactorMovement();
        if (player.InMinorActionSlowdown())
        {
            speed = 0f;
        }
        player.m_seman.ApplyStatusEffectSpeedMods(ref speed, player.m_moveDir);
        Vector3 velocity = player.m_moveDir * speed;
        velocity = Vector3.Lerp(player.m_currentVel, velocity, player.m_swimAcceleration);
        player.AddPushbackForce(ref velocity);
        return velocity;
    }
}
