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
