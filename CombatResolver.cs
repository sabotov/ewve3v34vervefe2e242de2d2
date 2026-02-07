using UnityEngine;

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
