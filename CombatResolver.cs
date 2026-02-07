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

        bool allowMiss = request.source == DamageSource.Attack;
        bool runTriggers = request.source == DamageSource.Attack;
        abilityManager.ApplyDamageModifiers(ctx, allowMiss, runTriggers);

        var result = new DamageResult(ctx.damage, ctx.isMiss);
        CombatRequests.DamageResolved?.Invoke(new DamageReport(request, result));
        return result;
    }
}
