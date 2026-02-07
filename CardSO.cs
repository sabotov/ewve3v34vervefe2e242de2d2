using UnityEngine;
using TMPro;
using System.Collections.Generic;

public enum StatType
{
    None,
    ATK,
    HP
}

[System.Serializable]
public class Status
{
    public AbilityType type;
    public int turns;
    public int value;
    public GameObject source;
    public List<Ability> backedAbilities = new List<Ability>();  // Для backup способностей при Silence
}

public enum TargetType
{
    None,
    Self,
    Warlord,
    AllAllies,
    RandomAllies,
    AllEnemies,
    RandomEnemies
}

public enum SummonLocation
{
    Random,
    Behind,
    FrontRow
}

[System.Serializable]
public class Ability
{
    public AbilityType type;
    public List<TriggerType> triggers = new List<TriggerType>();
    public int value;
    public int summonId = -1;
    public StatType statType = StatType.None;
    public TargetType targetType = TargetType.None;
    public int targetCount = 0;
    public SummonLocation summonLocation = SummonLocation.Random;
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Card")]
public class CardSO : ScriptableObject
{
    public int id;
    public string cardName;
    public int hp;
    public int atk;
    public AttackType attackType;
    public Rarity rarity;
    public Faction faction;
    public GameObject characterPrefab;
    public List<Ability> abilities = new List<Ability>();
}

[CreateAssetMenu(fileName = "NewWarlord", menuName = "Warlord")]
public class WarlordSO : ScriptableObject
{
    public int id;
    public string warlordName;
    public GameObject warlordPrefab;
    public int hp;
}

public enum Faction
{
    Syndicate,
    Fremen,
    Peacekeepers
}

public enum AbilityType
{
    Reborn,
    Electroshock,
    BuffATK,
    StealHealth,
    Evasion,
    Splash,
    Freeze,
    CounterAttack,
    Immunity,
    DebuffATK,
    Flight,
    SetStat,
    Summon,
    Vampirism,
    Poisoning,
    Resistance,
    Heal,
    Block,
    Invulnerability,
    Accuracy,
    MultiAttack,
    StealAttack,
    PunchThrough,
    Damage,
    Miss,
    Silence,
    BuffHP,
    Cleanse,
    Infection
}

public enum TriggerType
{
    OwnAttack,
    AllyAttack,
    EnemyAttack,
    SelfAttacked,
    AllyAttacked,
    AlliedWarlordAttacked,
    AnyWarlordAttacked,
    SelfSpawn,
    AllySpawn,
    EnemySpawn,
    BeforeAttack,
    KillEnemy,
    AllyFactionSpawn,
    EndTurn,
    StartTurn,
    EnemyDeath,
    AllyDeath
}

public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum AttackType
{
    Melee,
    Ranged
}

public interface IEntityInstanceData
{
    int currentHP { get; set; }
    TextMeshProUGUI hpText { get; set; }
    GameObject characterModel { get; set; }
    Quaternion originalCharacterRotation { get; set; }
}

[System.Serializable]
public class CardInstanceData : IEntityInstanceData
{
    public int currentHP { get; set; }
    public int currentATK;
    public int maxHP;
    public int baseATK;
    public Vector2Int gridPos;
    public bool isPlayerCard;
    public bool isOnField;
    public string currentCoord;
    public TextMeshProUGUI hpText { get; set; }
    public TextMeshProUGUI atkText;
    public GameObject characterModel { get; set; }
    public Quaternion originalCharacterRotation { get; set; }
    public AttackType attackType;
    public bool isAttacking;
    public Transform firePoint;
    public Rarity rarity;
    public Vector3 originalScale;
    public Faction faction;
    public List<Ability> abilities = new List<Ability>();
    public List<Status> statuses = new List<Status>();
}

[System.Serializable]
public class WarlordInstanceData : IEntityInstanceData
{
    public int currentHP { get; set; }
    public int maxHP { get; set; }
    public TextMeshProUGUI hpText { get; set; }
    public GameObject characterModel { get; set; }
    public Quaternion originalCharacterRotation { get; set; }
    public List<Ability> abilities = new List<Ability>();
}

