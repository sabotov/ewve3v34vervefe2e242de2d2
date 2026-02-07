using System;
using UnityEngine;

public interface ICombatResolver
{
    DamageResult ResolveDamage(DamageRequest request);
}

public enum DamageSource
{
    Attack,
    Ability,
    Status
}

public readonly struct DamageRequest
{
    public readonly GameObject attacker;
    public readonly GameObject target;
    public readonly int baseDamage;
    public readonly AttackType attackType;
    public readonly DamageSource source;
    public readonly bool triggerAfterEffects;

    public DamageRequest(GameObject attacker, GameObject target, int baseDamage, AttackType attackType, DamageSource source, bool triggerAfterEffects)
    {
        this.attacker = attacker;
        this.target = target;
        this.baseDamage = baseDamage;
        this.attackType = attackType;
        this.source = source;
        this.triggerAfterEffects = triggerAfterEffects;
    }
}

public readonly struct DamageResult
{
    public readonly int finalDamage;
    public readonly bool isMiss;

    public DamageResult(int finalDamage, bool isMiss)
    {
        this.finalDamage = finalDamage;
        this.isMiss = isMiss;
    }
}

public readonly struct DamageReport
{
    public readonly DamageRequest request;
    public readonly DamageResult result;

    public DamageReport(DamageRequest request, DamageResult result)
    {
        this.request = request;
        this.result = result;
    }
}

public readonly struct SummonRequest
{
    public readonly GameObject summoner;
    public readonly int summonId;
    public readonly int count;
    public readonly SummonLocation summonLocation;

    public SummonRequest(GameObject summoner, int summonId, int count, SummonLocation summonLocation)
    {
        this.summoner = summoner;
        this.summonId = summonId;
        this.count = count;
        this.summonLocation = summonLocation;
    }
}

public readonly struct CounterAttackRequest
{
    public readonly GameObject attacker;
    public readonly GameObject target;

    public CounterAttackRequest(GameObject attacker, GameObject target)
    {
        this.attacker = attacker;
        this.target = target;
    }
}

public readonly struct SplashRequest
{
    public readonly GameObject attacker;
    public readonly GameObject target;
    public readonly int damage;
    public readonly AttackType attackType;

    public SplashRequest(GameObject attacker, GameObject target, int damage, AttackType attackType)
    {
        this.attacker = attacker;
        this.target = target;
        this.damage = damage;
        this.attackType = attackType;
    }
}

public readonly struct LineDamageRequest
{
    public readonly GameObject attacker;
    public readonly GameObject target;
    public readonly int damage;
    public readonly AttackType attackType;

    public LineDamageRequest(GameObject attacker, GameObject target, int damage, AttackType attackType)
    {
        this.attacker = attacker;
        this.target = target;
        this.damage = damage;
        this.attackType = attackType;
    }
}

public static class CombatRequests
{
    public static Func<DamageRequest, DamageResult> RequestDamage;
    public static Action<DamageReport> DamageResolved;
    public static Action<DamageReport> DamageApplied;
    public static Action<SummonRequest> RequestSummon;
    public static Action<CounterAttackRequest> RequestCounterAttack;
    public static Action<SplashRequest> RequestSplash;
    public static Action<LineDamageRequest> RequestLineDamage;
}

public class CombatResolver : MonoBehaviour, ICombatResolver
{
    [SerializeField] private AbilityManager abilityManager;

    private void Awake()
    {
        if (abilityManager == null)
        {
            abilityManager = GetComponent<AbilityManager>();
        }
    }

    private void OnEnable()
    {
        CombatRequests.RequestDamage = ResolveDamage;
    }

    private void OnDisable()
    {
        if (CombatRequests.RequestDamage == ResolveDamage)
        {
            CombatRequests.RequestDamage = null;
        }
    }

    public DamageResult ResolveDamage(DamageRequest request)
    {
        if (request.target == null)
        {
            var empty = new DamageResult(0, false);
            CombatRequests.DamageResolved?.Invoke(new DamageReport(request, empty));
            return empty;
        }

        if (abilityManager == null)
        {
            var fallback = new DamageResult(request.baseDamage, false);
            CombatRequests.DamageResolved?.Invoke(new DamageReport(request, fallback));
            return fallback;
        }

        var ctx = new DamageContext
        {
            attacker = request.attacker,
            target = request.target,
            damage = request.baseDamage,
            attackType = request.attackType,
            isMiss = false
        };

        bool isAttackSource = request.source == DamageSource.Attack;
        if (isAttackSource)
        {
            abilityManager.ProcessBeforeDamageTriggers(ctx);
        }

        if (isAttackSource)
        {
            if (abilityManager.HasStatus(ctx.attacker, AbilityType.Miss)
                || (ctx.attackType == AttackType.Ranged && abilityManager.HasAbility(ctx.target, AbilityType.Evasion, out _))
                || (ctx.attackType == AttackType.Melee && abilityManager.HasAbility(ctx.target, AbilityType.Flight, out _) && !abilityManager.HasAbility(ctx.attacker, AbilityType.Flight, out _)))
            {
                ctx.isMiss = true;
            }

            if (abilityManager.HasAbility(ctx.attacker, AbilityType.Accuracy, out _))
            {
                ctx.isMiss = false;
            }
        }

        if (ctx.isMiss)
        {
            var missResult = new DamageResult(0, true);
            CombatRequests.DamageResolved?.Invoke(new DamageReport(request, missResult));
            return missResult;
        }

        if (abilityManager.HasAbility(ctx.target, AbilityType.Invulnerability, out _))
        {
            ctx.damage = 0;
            abilityManager.ConsumeInvulnerability(ctx.target);
        }

        if (abilityManager.HasAbility(ctx.target, AbilityType.Resistance, out var res))
        {
            ctx.damage = Mathf.RoundToInt(ctx.damage * (100 - res) / 100f);
        }

        if (abilityManager.HasAbility(ctx.target, AbilityType.Block, out var block))
        {
            ctx.damage = Mathf.Max(0, ctx.damage - block);
        }

        var result = new DamageResult(ctx.damage, ctx.isMiss);
        CombatRequests.DamageResolved?.Invoke(new DamageReport(request, result));
        return result;
    }
}
