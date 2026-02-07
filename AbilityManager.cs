using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AbilityManager : MonoBehaviour
{
    [System.Serializable]
    public class AbilityIconPair
    {
        public AbilityType type;
        [SerializeField] public Sprite icon;
    }

    [Header("Ability Icons - Assign sprites here")]
    [SerializeField]
    private List<AbilityIconPair> abilityIconPairs;

    private void OnValidate()
    {
        if (abilityIconPairs == null || abilityIconPairs.Count == 0)
        {
            InitializeAbilityIconPairs();
        }
    }

    private void InitializeAbilityIconPairs()
    {
        abilityIconPairs = new List<AbilityIconPair>
        {
            new AbilityIconPair { type = AbilityType.BuffATK },
            new AbilityIconPair { type = AbilityType.DebuffATK },
            new AbilityIconPair { type = AbilityType.Damage },
            new AbilityIconPair { type = AbilityType.Heal },
            new AbilityIconPair { type = AbilityType.CounterAttack },
            new AbilityIconPair { type = AbilityType.Summon },
            new AbilityIconPair { type = AbilityType.SetStat },
            new AbilityIconPair { type = AbilityType.StealHealth },
            new AbilityIconPair { type = AbilityType.Vampirism },
            new AbilityIconPair { type = AbilityType.Poisoning },
            new AbilityIconPair { type = AbilityType.Freeze },
            new AbilityIconPair { type = AbilityType.StealAttack },
            new AbilityIconPair { type = AbilityType.PunchThrough },
            new AbilityIconPair { type = AbilityType.Miss },
            new AbilityIconPair { type = AbilityType.Silence },
            new AbilityIconPair { type = AbilityType.BuffHP },
            new AbilityIconPair { type = AbilityType.Cleanse },
            new AbilityIconPair { type = AbilityType.Invulnerability },
            new AbilityIconPair { type = AbilityType.Reborn },
            new AbilityIconPair { type = AbilityType.MultiAttack },
            new AbilityIconPair { type = AbilityType.Splash },
            new AbilityIconPair { type = AbilityType.Evasion },
            new AbilityIconPair { type = AbilityType.Flight },
            new AbilityIconPair { type = AbilityType.Accuracy },
            new AbilityIconPair { type = AbilityType.Resistance },
            new AbilityIconPair { type = AbilityType.Block },
            new AbilityIconPair { type = AbilityType.Immunity },
            new AbilityIconPair { type = AbilityType.Infection },
            new AbilityIconPair { type = AbilityType.Electroshock }
        };
        
        Debug.Log($"Initialized {abilityIconPairs.Count} ability icon pairs");
    }

    public Sprite GetAbilityIcon(AbilityType type)
    {
        if (abilityIconPairs == null) return null;
        return abilityIconPairs.FirstOrDefault(p => p.type == type)?.icon;
    }

    public Sprite GetAbilitySprite(AbilityType type)
    {
        return GetAbilityIcon(type);
    }

    private BattleGame battleGame;
    private HashSet<GameObject> summonedThisTurn = new HashSet<GameObject>(); // Track summoners per turn

    private void Awake()
    {
        battleGame = GetComponent<BattleGame>();
        if (battleGame == null)
        {
            Debug.LogError("AbilityManager requires BattleGame component on the same GameObject.");
        }
        
        if (abilityIconPairs == null || abilityIconPairs.Count == 0)
        {
            InitializeAbilityIconPairs();
        }
    }

    private void OnEnable()
    {
        BattleEvents.OnTurnStart += HandleTurnStart;
        BattleEvents.OnTurnEnd += HandleTurnEnd;
        BattleEvents.OnSpawn += HandleSpawn;
        BattleEvents.OnBeforeAttack += HandleBeforeAttack;
        BattleEvents.OnDeath += HandleDeath;
        CombatRequests.DamageApplied += HandleDamageApplied;
    }

    private void OnDisable()
    {
        BattleEvents.OnTurnStart -= HandleTurnStart;
        BattleEvents.OnTurnEnd -= HandleTurnEnd;
        BattleEvents.OnSpawn -= HandleSpawn;
        BattleEvents.OnBeforeAttack -= HandleBeforeAttack;
        BattleEvents.OnDeath -= HandleDeath;
        CombatRequests.DamageApplied -= HandleDamageApplied;
    }

    private void HandleTurnStart(GameObject _)
    {
        ProcessTrigger(TriggerType.StartTurn, null, null, null);
        ProcessStatuses();
    }

    private void HandleTurnEnd(GameObject _)
    {
        ProcessTrigger(TriggerType.EndTurn, null, null, null);
        summonedThisTurn.Clear();
    }

    private void HandleSpawn(GameObject card)
    {
        ProcessTrigger(TriggerType.SelfSpawn, card);
        ProcessTrigger(TriggerType.AllySpawn, card);
        ProcessTrigger(TriggerType.EnemySpawn, card);
        ProcessTrigger(TriggerType.AllyFactionSpawn, card);
    }

    private void HandleBeforeAttack(AttackContext ctx)
    {
        if (ctx == null || battleGame == null || ctx.attacker == null) return;

        if (HasStatus(ctx.attacker, AbilityType.Freeze))
        {
            ctx.canAct = false;
            return;
        }

        if (HasAbility(ctx.attacker, AbilityType.MultiAttack, out var multiVal))
        {
            ctx.attackCount = Mathf.Max(1, 1 + multiVal);
        }
    }

    public void ProcessBeforeDamageTriggers(DamageContext ctx)
    {
        if (ctx == null) return;

        ProcessTrigger(TriggerType.BeforeAttack, ctx.attacker, ctx.target, ctx);
        ProcessTrigger(TriggerType.OwnAttack, ctx.attacker, ctx.target, ctx);
        ProcessTrigger(TriggerType.AllyAttack, ctx.attacker, ctx.target, ctx);
        ProcessTrigger(TriggerType.EnemyAttack, ctx.attacker, ctx.target, ctx);
    }

    private void HandleDamageApplied(DamageReport report)
    {
        var ctx = new DamageContext
        {
            attacker = report.request.attacker,
            target = report.request.target,
            damage = report.result.finalDamage,
            attackType = report.request.attackType,
            isMiss = report.result.isMiss
        };
        HandleAfterDamage(ctx);
    }
    private void HandleAfterDamage(DamageContext ctx)
    {
        if (ctx == null || battleGame == null) return;

        ProcessTrigger(TriggerType.SelfAttacked, ctx.attacker, ctx.target, ctx);
        ProcessTrigger(TriggerType.AllyAttacked, ctx.attacker, ctx.target, ctx);
        if (ctx.target != null && battleGame.warlordDataMap.ContainsKey(ctx.target))
        {
            ProcessTrigger(TriggerType.AlliedWarlordAttacked, ctx.attacker, ctx.target);
            ProcessTrigger(TriggerType.AnyWarlordAttacked, ctx.attacker, ctx.target);
        }

        if (!ctx.isMiss && ctx.attacker != null && ctx.target != null && HasAbility(ctx.attacker, AbilityType.Splash, out var splash))
        {
            CombatRequests.RequestSplash?.Invoke(new SplashRequest(ctx.attacker, ctx.target, splash, ctx.attackType));
        }

        if (ctx.target != null && HasAbility(ctx.target, AbilityType.Electroshock, out var shock))
        {
            var resolver = CombatRequests.RequestDamage;
            if (resolver != null)
            {
                resolver(new DamageRequest(ctx.target, ctx.attacker, shock, ctx.attackType, DamageSource.Ability, false));
            }
        }
    }

    private void HandleDeath(GameObject target, GameObject from)
    {
        if (target == null || battleGame == null) return;

        if (battleGame.cardDataMap.TryGetValue(target, out var cardData) && HasAbility(target, AbilityType.Reborn, out _))
        {
            cardData.currentHP = Mathf.Max(1, cardData.currentHP);
            battleGame.UpdateEntityUI(target, cardData);
            battleGame.StopAllCoroutinesForObject(target);
            battleGame.StartCoroutine(battleGame.Materialize(false, target, () =>
            {
                battleGame.StartCoroutine(RebornSequence(target, cardData));
            }));
            return;
        }

        ProcessTrigger(TriggerType.AllyDeath, from, target);
        ProcessTrigger(TriggerType.EnemyDeath, from, target);
        if (from != null) ProcessTrigger(TriggerType.KillEnemy, from, target);
    }

    private IEnumerator RebornSequence(GameObject target, CardInstanceData cardData)
    {
        yield return new WaitForSeconds(0.5f);
        if (target == null || battleGame == null || !battleGame.cardDataMap.ContainsKey(target))
        {
            Debug.LogWarning($"RebornSequence: Target {target?.name} no longer exists");
            yield break;
        }
        Debug.Log($"Reborn: Restoring {target.name}");
        cardData.currentHP = cardData.maxHP;
        cardData.currentATK = cardData.baseATK;
        cardData.abilities.RemoveAll(a => a.type == AbilityType.Reborn);
        Debug.Log($"Reborn: Restored {target.name}, HP={cardData.currentHP}, ATK={cardData.currentATK}, abilities: {string.Join(", ", cardData.abilities.Select(a => a.type.ToString()))}");
        battleGame.UpdateEntityUI(target, cardData);
        battleGame.SetupAbilitiesUI(target, cardData);
        yield return battleGame.StartCoroutine(battleGame.Materialize(true, target, () =>
        {
            if (target != null)
            {
                Debug.Log($"Reborn: Reappeared {target.name}");
                BattleEvents.OnSpawn?.Invoke(target);
            }
        }));
    }

    public void ConsumeInvulnerability(GameObject target)
    {
        if (target == null || battleGame == null) return;
        Ability ab;
        IEntityInstanceData instance;
        if (battleGame.cardDataMap.TryGetValue(target, out var td))
        {
            ab = td.abilities.First(a => a.type == AbilityType.Invulnerability);
            instance = td;
        }
        else if (battleGame.warlordDataMap.TryGetValue(target, out var wd))
        {
            ab = wd.abilities.First(a => a.type == AbilityType.Invulnerability);
            instance = wd;
        }
        else
        {
            return;
        }

        if (--ab.value <= 0)
        {
            if (instance is CardInstanceData cdi) cdi.abilities.Remove(ab);
            else if (instance is WarlordInstanceData wdi) wdi.abilities.Remove(ab);
        }
        battleGame.SetupAbilitiesUI(target, instance);
    }

    public void ProcessTrigger(TriggerType trigger, GameObject source = null, GameObject target = null, object extra = null)
    {
        if (battleGame == null) return;

        foreach (var entity in battleGame.cardDataMap.Keys.ToList())
        {
            if (entity == null || !battleGame.cardDataMap.ContainsKey(entity) || !battleGame.cardDataMap[entity].isOnField) continue;
            
            var inst = battleGame.cardDataMap[entity];
            bool sameTeamSource = source != null && battleGame.GetIsPlayerSide(source) == battleGame.GetIsPlayerSide(entity);
            bool sameTeamTarget = target != null && battleGame.GetIsPlayerSide(target) == battleGame.GetIsPlayerSide(entity);
            bool sameFactionSource = sameTeamSource && source != null && battleGame.cardDataMap.TryGetValue(source, out var sd) && sd.faction == inst.faction;
            bool alliedWarlord = target != null && battleGame.warlordDataMap.ContainsKey(target) && sameTeamTarget;
            bool anyWarlord = target != null && battleGame.warlordDataMap.ContainsKey(target);
            
            foreach (var ab in inst.abilities)
            {
                foreach (var tr in ab.triggers)
                {
                    bool activate = tr switch
                    {
                        TriggerType.OwnAttack => trigger == TriggerType.OwnAttack && source == entity,
                        TriggerType.AllyAttack => trigger == TriggerType.AllyAttack && source != entity && sameTeamSource,
                        TriggerType.EnemyAttack => trigger == TriggerType.EnemyAttack && source != entity && !sameTeamSource,
                        TriggerType.SelfAttacked => trigger == TriggerType.SelfAttacked && target == entity,
                        TriggerType.AllyAttacked => trigger == TriggerType.AllyAttacked && target != entity && sameTeamTarget,
                        TriggerType.AlliedWarlordAttacked => trigger == TriggerType.AlliedWarlordAttacked && alliedWarlord,
                        TriggerType.AnyWarlordAttacked => trigger == TriggerType.AnyWarlordAttacked && anyWarlord,
                        TriggerType.SelfSpawn => trigger == TriggerType.SelfSpawn && source == entity && !summonedThisTurn.Contains(entity),
                        TriggerType.AllySpawn => trigger == TriggerType.AllySpawn && source != entity && sameTeamSource,
                        TriggerType.EnemySpawn => trigger == TriggerType.EnemySpawn && source != entity && !sameTeamSource,
                        TriggerType.BeforeAttack => trigger == TriggerType.BeforeAttack && source == entity,
                        TriggerType.KillEnemy => trigger == TriggerType.KillEnemy && source == entity && !sameTeamTarget,
                        TriggerType.AllyFactionSpawn => trigger == TriggerType.AllyFactionSpawn && source != entity && sameFactionSource,
                        TriggerType.EndTurn => trigger == TriggerType.EndTurn,
                        TriggerType.StartTurn => trigger == TriggerType.StartTurn,
                        TriggerType.EnemyDeath => trigger == TriggerType.EnemyDeath && !sameTeamTarget,
                        TriggerType.AllyDeath => trigger == TriggerType.AllyDeath && target != entity && sameTeamTarget,
                        _ => false
                    };
                    if (activate)
                    {
                        if (tr == TriggerType.SelfSpawn) summonedThisTurn.Add(entity); // Mark as summoned
                        StartCoroutine(ApplyAbility(ab, entity, source, target, extra));
                    }
                }
            }

            if (trigger == TriggerType.AllyAttacked && target != entity && sameTeamTarget)
            {
                var infStatus = inst.statuses.FirstOrDefault(s => s.type == AbilityType.Infection);
                if (infStatus != null)
                {
                    var resolver = CombatRequests.RequestDamage;
                    if (resolver != null)
                    {
                        resolver(new DamageRequest(null, entity, infStatus.value, AttackType.Melee, DamageSource.Status, false));
                    }
                }
            }
        }

        if (trigger == TriggerType.EndTurn)
        {
            summonedThisTurn.Clear(); // Reset summon tracking at turn end
        }
    }

    public IEnumerator ApplyAbility(Ability ab, GameObject activator, GameObject source, GameObject target, object extra)
    {
        if (battleGame == null || !battleGame.cardDataMap.TryGetValue(activator, out var actData)) yield break;
        if (actData.currentHP <= 0) yield break;
        if (target != null && target != activator && HasAbility(target, AbilityType.Immunity, out _)) yield break;
        var ctx = extra as DamageContext;
        if (ab.triggers.Contains(TriggerType.OwnAttack) && ctx != null && ctx.isMiss) yield break;
        var targets = GetTargets(ab, activator);
        bool isAttackTrigger = ab.triggers.Contains(TriggerType.OwnAttack);
        if (isAttackTrigger) yield return new WaitForSeconds(0.4f);

        switch (ab.type)
        {
            case AbilityType.BuffATK:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        td.currentATK += ab.value;
                        battleGame.UpdateEntityUI(tgt, td);
                    }
                }
                break;
            case AbilityType.Damage:
                if (target != null)
                {
                    var resolver = CombatRequests.RequestDamage;
                    if (resolver != null)
                    {
                        resolver(new DamageRequest(activator, target, ab.value, actData.attackType, DamageSource.Ability, false));
                    }
                }
                break;
            case AbilityType.Heal:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        td.currentHP = Mathf.Min(td.currentHP + ab.value, td.maxHP);
                        battleGame.UpdateEntityUI(tgt, td);
                    }
                    else if (battleGame.warlordDataMap.TryGetValue(tgt, out var wd))
                    {
                        wd.currentHP = Mathf.Min(wd.currentHP + ab.value, wd.maxHP);
                        battleGame.UpdateEntityUI(tgt, wd);
                        battleGame.SyncWarlordHP(tgt);
                    }
                }
                break;
            case AbilityType.CounterAttack:
                if (source != null)
                {
                    CombatRequests.RequestCounterAttack?.Invoke(new CounterAttackRequest(activator, source));
                }
                break;
            case AbilityType.Summon:
                if (ab.summonId == -1)
                {
                    Debug.LogWarning($"Summon failed: Invalid summonId {ab.summonId}");
                    yield break;
                }
                CombatRequests.RequestSummon?.Invoke(new SummonRequest(activator, ab.summonId, ab.value > 0 ? ab.value : 1, ab.summonLocation));
                break;
            case AbilityType.SetStat:
                if (ab.statType == StatType.ATK) actData.currentATK = ab.value;
                else if (ab.statType == StatType.HP) actData.currentHP = ab.value;
                battleGame.UpdateEntityUI(activator, actData);
                break;
            case AbilityType.StealHealth:
                int totalStolen = 0;
                int pendingSteal = targets.Count;
                var damageResolver = CombatRequests.RequestDamage;
                if (damageResolver == null)
                {
                    battleGame.UpdateEntityUI(activator, actData);
                    break;
                }
                System.Action<DamageReport> stealHandler = null;
                stealHandler = report =>
                {
                    if (report.request.attacker == activator
                        && report.request.source == DamageSource.Ability
                        && targets.Contains(report.request.target))
                    {
                        totalStolen += report.result.finalDamage;
                        pendingSteal--;
                        if (pendingSteal <= 0)
                        {
                            CombatRequests.DamageResolved -= stealHandler;
                        }
                    }
                };
                CombatRequests.DamageResolved += stealHandler;
                foreach (var tgt in targets)
                {
                    damageResolver(new DamageRequest(activator, tgt, ab.value, actData.attackType, DamageSource.Ability, false));
                }
                actData.currentHP += totalStolen;
                battleGame.UpdateEntityUI(activator, actData);
                break;
            case AbilityType.DebuffATK:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        td.currentATK = Mathf.Max(0, td.currentATK - ab.value);
                        battleGame.UpdateEntityUI(tgt, td);
                    }
                }
                break;
            case AbilityType.Vampirism:
                if (target != null && !battleGame.warlordDataMap.ContainsKey(target))
                {
                    int drained = 0;
                    var damageResolver = CombatRequests.RequestDamage;
                    if (damageResolver == null)
                    {
                        battleGame.UpdateEntityUI(activator, actData);
                        break;
                    }
                    System.Action<DamageReport> vampHandler = null;
                    vampHandler = report =>
                    {
                        if (report.request.attacker == activator
                            && report.request.target == target
                            && report.request.source == DamageSource.Ability)
                        {
                            drained = report.result.finalDamage;
                            CombatRequests.DamageResolved -= vampHandler;
                        }
                    };
                    CombatRequests.DamageResolved += vampHandler;
                    damageResolver(new DamageRequest(activator, target, ab.value, actData.attackType, DamageSource.Ability, false));
                    actData.currentHP += drained;
                    battleGame.UpdateEntityUI(activator, actData);
                }
                break;
            case AbilityType.Poisoning:
                if (target != null) AddStatus(target, AbilityType.Poisoning, -1, ab.value);
                break;
            case AbilityType.Freeze:
                if (target != null) AddStatus(target, AbilityType.Freeze, ab.value, 0);
                break;
            case AbilityType.StealAttack:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        int stolen = Mathf.Min(ab.value, td.currentATK);
                        td.currentATK -= stolen;
                        actData.currentATK += stolen;
                        battleGame.UpdateEntityUI(tgt, td);
                        battleGame.UpdateEntityUI(activator, actData);
                    }
                }
                break;
            case AbilityType.PunchThrough:
                if (target != null && ctx != null)
                {
                    CombatRequests.RequestLineDamage?.Invoke(new LineDamageRequest(activator, target, ctx.damage, ctx.attackType));
                }
                break;
            case AbilityType.Miss:
                if (target != null) AddStatus(target, AbilityType.Miss, ab.value, 0);
                break;
            case AbilityType.Silence:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        var s = new Status { type = AbilityType.Silence, turns = ab.value, value = 0 };
                        s.backedAbilities = new List<Ability>(td.abilities);
                        td.abilities.Clear();
                        td.statuses.RemoveAll(status => status.type != AbilityType.Silence);
                        td.statuses.Add(s);
                        battleGame.SetupAbilitiesUI(tgt, td);
                    }
                }
                break;
            case AbilityType.BuffHP:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        td.currentHP += ab.value;
                        battleGame.UpdateEntityUI(tgt, td);
                    }
                    else if (battleGame.warlordDataMap.TryGetValue(tgt, out var wd))
                    {
                        wd.currentHP += ab.value;
                        battleGame.UpdateEntityUI(tgt, wd);
                        battleGame.SyncWarlordHP(tgt);
                    }
                }
                break;
            case AbilityType.Cleanse:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        td.statuses.RemoveAll(s => s.type != AbilityType.Silence);
                    }
                }
                break;
            case AbilityType.Invulnerability:
                foreach (var tgt in targets)
                {
                    if (battleGame.cardDataMap.TryGetValue(tgt, out var td))
                    {
                        var existing = td.abilities.FirstOrDefault(a => a.type == AbilityType.Invulnerability);
                        if (existing != null)
                        {
                            existing.value += ab.value;
                        }
                        else
                        {
                            td.abilities.Add(new Ability { type = AbilityType.Invulnerability, triggers = new List<TriggerType>(), value = ab.value });
                        }
                        battleGame.SetupAbilitiesUI(tgt, td);
                    }
                    else if (battleGame.warlordDataMap.TryGetValue(tgt, out var wd))
                    {
                        var existing = wd.abilities.FirstOrDefault(a => a.type == AbilityType.Invulnerability);
                        if (existing != null)
                        {
                            existing.value += ab.value;
                        }
                        else
                        {
                            wd.abilities.Add(new Ability { type = AbilityType.Invulnerability, triggers = new List<TriggerType>(), value = ab.value });
                        }
                        battleGame.SetupAbilitiesUI(tgt, wd);
                    }
                }
                break;
            default:
                Debug.Log($"Ability {ab.type} not implemented.");
                break;
        }
    }

    private List<GameObject> GetTargets(Ability ab, GameObject activator)
    {
        if (battleGame == null || !battleGame.cardDataMap.TryGetValue(activator, out var actData)) return new List<GameObject>();
        bool isPlayer = actData.isPlayerCard;
        switch (ab.targetType)
        {
            case TargetType.Self:
                return new List<GameObject> { activator };
            case TargetType.Warlord:
                return new List<GameObject> { isPlayer ? battleGame.playerWarlordObject : battleGame.botWarlordObject };
            case TargetType.AllAllies:
                return battleGame.cardDataMap.Keys.Where(k => battleGame.cardDataMap[k].isOnField && battleGame.cardDataMap[k].isPlayerCard == isPlayer).ToList();
            case TargetType.RandomAllies:
                var allies = battleGame.cardDataMap.Keys.Where(k => battleGame.cardDataMap[k].isOnField && battleGame.cardDataMap[k].isPlayerCard == isPlayer).ToList();
                if (ab.targetCount == 0 || ab.targetCount >= allies.Count) return allies;
                allies = allies.OrderBy(_ => Random.value).Take(ab.targetCount).ToList();
                return allies;
            case TargetType.AllEnemies:
                return battleGame.cardDataMap.Keys.Where(k => battleGame.cardDataMap[k].isOnField && battleGame.cardDataMap[k].isPlayerCard != isPlayer).ToList();
            case TargetType.RandomEnemies:
                var enemies = battleGame.cardDataMap.Keys.Where(k => battleGame.cardDataMap[k].isOnField && battleGame.cardDataMap[k].isPlayerCard != isPlayer).ToList();
                if (ab.targetCount == 0 || ab.targetCount >= enemies.Count) return enemies;
                enemies = enemies.OrderBy(_ => Random.value).Take(ab.targetCount).ToList();
                return enemies;
            default:
                return new List<GameObject>();
        }
    }

    public bool HasAbility(GameObject obj, AbilityType type, out int value)
    {
        value = 0;
        if (battleGame == null) return false;
        
        if (HasStatus(obj, AbilityType.Silence)) return false;
        if (TryGetEntityAbilities(obj, out var abilities) &&
            abilities.FirstOrDefault(a => a.type == type) is { } ab)
        {
            value = ab.value;
            return true;
        }
        return false;
    }

    private bool TryGetEntityAbilities(GameObject obj, out List<Ability> abilities)
    {
        abilities = null;
        if (battleGame == null || obj == null) return false;
        if (battleGame.cardDataMap.TryGetValue(obj, out var card))
        {
            abilities = card.abilities;
            return true;
        }
        if (battleGame.warlordDataMap.TryGetValue(obj, out var warlord))
        {
            abilities = warlord.abilities;
            return true;
        }
        return false;
    }

    public void AddStatus(GameObject obj, AbilityType type, int turns, int value, GameObject source = null)
    {
        if (battleGame == null || !battleGame.cardDataMap.TryGetValue(obj, out var d) || HasAbility(obj, AbilityType.Immunity, out _))
            return;
            
        if (type == AbilityType.Miss && HasAbility(obj, AbilityType.Accuracy, out _)) return;
        d.statuses.Add(new Status { type = type, turns = turns, value = value, source = source });
    }

    public bool HasStatus(GameObject obj, AbilityType type)
    {
        return battleGame != null && battleGame.cardDataMap.TryGetValue(obj, out var d) && d.statuses.Any(s => s.type == type);
    }

    public void ProcessStatuses()
    {
        if (battleGame == null) return;
        
        foreach (var entity in battleGame.cardDataMap.Keys.ToList())
        {
            if (entity == null || !battleGame.cardDataMap.ContainsKey(entity)) continue;
            var data = battleGame.cardDataMap[entity];
            var toRemove = new List<Status>();
            foreach (var s in data.statuses)
            {
                if (s.type == AbilityType.Poisoning)
                {
                    var resolver = CombatRequests.RequestDamage;
                    if (resolver != null)
                    {
                        resolver(new DamageRequest(s.source, entity, s.value, AttackType.Melee, DamageSource.Status, false));
                    }
                }
                if (--s.turns <= 0)
                {
                    if (s.type == AbilityType.Silence)
                    {
                        data.abilities = s.backedAbilities;
                        battleGame.SetupAbilitiesUI(entity, data);
                    }
                    toRemove.Add(s);
                }
            }
            data.statuses.RemoveAll(toRemove.Contains);
        }
    }
}
