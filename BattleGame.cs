using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

public class BattleGame : MonoBehaviour
{
    [System.Serializable]
    public class CellData
    {
        public string coordinate;
        public Transform cellTransform;
        [HideInInspector] public Vector2Int gridPos;
    }

    [Header("Game Settings")]
    public GameObject cardObjectPrefab, warlordObjectPrefab;
    public int startCardsCount = 3, maxHandSize = 5;
    public float cardSpacing = 2f, materializeDuration = 0.6f;

    [Header("UI References")]
    public Transform playerHandContent;

    [Header("Damage Numbers")]
    public GameObject damageNumberPrefab;

    [Header("Card Database")]
    public List<CardSO> cardDatabase = new();
    public List<WarlordSO> warlordDatabase = new();

    [Header("Decks")]
    public List<CardSO> playerDeck = new(), botDeck = new();

    [Header("Cell Configuration")]
    [SerializeField] private List<CellData> cells = new();

    [Header("Rarity Settings")]
    public Sprite meleeAtkIcon, rangedAtkIcon;
    public Color commonColor, rareColor, epicColor, legendaryColor;

    [Header("Cell Highlight Colors")]
    public Color meleeHighlightColor = Color.yellow, rangedHighlightColor = Color.cyan;

    [Header("Ability Manager")]
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private CombatResolver combatResolver;

    [Header("Ability Icons")]
    public GameObject abilityIconPrefab;

    public const int GRID_WIDTH = 6, GRID_HEIGHT = 4;
    private const float DAMAGE_ANIMATION_DELAY = 0.4f, RANGED_ATTACK_ANIMATION_DELAY = 0.5f;
    private static readonly List<string> PlayerCoords = new() { "A1", "A2", "A3", "B1", "B2", "B3", "C1", "C2", "C3", "D1", "D2", "D3" };
    private static readonly List<string> BotCoords = new() { "A4", "A5", "A6", "B4", "B5", "B6", "C4", "C5", "C6", "D4", "D5", "D6" };

    public GameObject[,] grid = new GameObject[GRID_WIDTH, GRID_HEIGHT];
    private bool isPlayerTurn, battlePhase, hasPlacedCardThisTurn, isPlacingCard;
    private int playerWarlordHP, enemyWarlordHP;
    private Dictionary<string, CellData> cellDict = new();
    private List<GameObject> playerHand = new(), botHand = new();
    public Dictionary<GameObject, CardInstanceData> cardDataMap = new();
    public Dictionary<GameObject, WarlordInstanceData> warlordDataMap = new();
    public GameObject playerWarlordObject, botWarlordObject;
    private GameObject draggedCard;
    private Vector3 startPosition;
    private float dragZ;
    private string lastHighlightedCell;
    private List<string> highlightedCells = new();
    private Dictionary<GameObject, List<Coroutine>> activeCoroutines = new();
    private readonly Dictionary<Rarity, Color> rarityColors = new();

    void OnEnable()
    {
        CombatRequests.DamageResolved += HandleDamageResolved;
        CombatRequests.RequestSummon += HandleRequestSummon;
        CombatRequests.RequestCounterAttack += HandleRequestCounterAttack;
        CombatRequests.RequestSplash += HandleRequestSplash;
        CombatRequests.RequestLineDamage += HandleRequestLineDamage;
    }

    void OnDisable()
    {
        CombatRequests.DamageResolved -= HandleDamageResolved;
        CombatRequests.RequestSummon -= HandleRequestSummon;
        CombatRequests.RequestCounterAttack -= HandleRequestCounterAttack;
        CombatRequests.RequestSplash -= HandleRequestSplash;
        CombatRequests.RequestLineDamage -= HandleRequestLineDamage;
    }

    void Start()
    {
        abilityManager ??= GetComponent<AbilityManager>() ?? throw new System.Exception("AbilityManager not found.");
        rarityColors.Add(Rarity.Common, commonColor);
        rarityColors.Add(Rarity.Rare, rareColor);
        rarityColors.Add(Rarity.Epic, epicColor);
        rarityColors.Add(Rarity.Legendary, legendaryColor);
        InitializeCells();
        InitGame();
    }

    void InitializeCells()
    {
        cellDict = cells.Where(c => c?.coordinate != null && c.cellTransform != null)
            .ToDictionary(c => c.coordinate, c =>
            {
                if (c.coordinate.Contains("Warlord"))
                {
                    c.gridPos = Vector2Int.zero;
                }
                else
                {
                    int x = int.Parse(c.coordinate.Substring(1)), y = c.coordinate[0] - 'A';
                    c.gridPos = (x >= 1 && x <= GRID_WIDTH && y >= 0 && y < GRID_HEIGHT) ? new Vector2Int(x, y) : Vector2Int.zero;
                }
                if (!c.coordinate.Contains("Warlord") && !c.cellTransform.TryGetComponent<BoxCollider>(out _))
                    c.cellTransform.gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, 0.5f, 1f);
                c.cellTransform.gameObject.layer = LayerMask.NameToLayer("CellsLayer");
                c.cellTransform.name = c.coordinate;
                return c;
            });
    }

    void InitGame()
    {
        if (cardDatabase.Count == 0 || warlordDatabase.Count == 0 || cardObjectPrefab == null || warlordObjectPrefab == null) return;
        isPlayerTurn = Random.Range(0, 2) == 0;
        hasPlacedCardThisTurn = isPlacingCard = battlePhase = false;
        playerDeck = cardDatabase.SelectMany(c => Enumerable.Repeat(c, 3)).ToList();
        botDeck = new List<CardSO>(playerDeck);
        ClearGrid();
        CreateWarlords();
        CreateStartingHands();
        StartCoroutine(GameTurnCycle());
    }

    void ClearGrid() => grid.Iterate((x, y) =>
    {
        if (grid[x, y] == null) return;
        StopAllCoroutinesForObject(grid[x, y]);
        StartCoroutine(Materialize(false, grid[x, y], () => { cardDataMap.Remove(grid[x, y]); Destroy(grid[x, y]); grid[x, y] = null; }));
    });

    void CreateWarlords()
    {
        CreateWarlord("PlayerWarlord", true);
        CreateWarlord("BotWarlord", false);
    }

    void CreateWarlord(string coord, bool isPlayer)
    {
        if (!cellDict.TryGetValue(coord, out var cell) || cell?.cellTransform == null) return;
        var data = warlordDatabase.Random();
        var obj = Instantiate(warlordObjectPrefab, cell.cellTransform);
        obj.name = $"{data.warlordName}_Warlord";
        obj.transform.localPosition = Vector3.zero;
        var instance = new WarlordInstanceData { currentHP = data.hp, maxHP = data.hp, hpText = obj.transform.Find("HP ATK Text/HP/HP Text")?.GetComponent<TextMeshProUGUI>() };
        SetupCharacterModel(obj, data.warlordPrefab, isPlayer, instance);
        SetupAbilitiesUI(obj, instance);
        warlordDataMap[obj] = instance;
        if (isPlayer) { playerWarlordObject = obj; playerWarlordHP = data.hp; } else { botWarlordObject = obj; enemyWarlordHP = data.hp; }
        UpdateEntityUI(obj, instance);
        UpdateIconColors(obj, null);
    }

    void SetupCharacterModel(GameObject obj, GameObject prefab, bool isPlayer, IEntityInstanceData data)
    {
        if (prefab == null) return;
        var model = Instantiate(prefab, obj.transform);
        data.characterModel = model;
        data.originalCharacterRotation = model.transform.localRotation;
        if (!isPlayer) model.transform.localRotation *= Quaternion.Euler(0, 180, 0);
        model.GetComponent<Collider>()?.SetEnabled(false);
        if (data is CardInstanceData cardData && cardData.attackType == AttackType.Ranged)
            cardData.firePoint = FindRecursive(model.transform, "FirePoint");
        StartCoroutine(Materialize(true, model));
    }

    Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
            if (FindRecursive(child, name) is Transform found) return found;
        return null;
    }

    void CreateStartingHands()
    {
        CreateHand(true, playerDeck, playerHand, playerHandContent);
        CreateHand(false, botDeck, botHand, null);
    }

    void CreateHand(bool isPlayer, List<CardSO> deck, List<GameObject> hand, Transform content)
    {
        for (int i = 0; i < startCardsCount && hand.Count < maxHandSize && deck.Any(c => !hand.Any(h => h.name == $"{c.cardName}_Card")); i++)
        {
            var cardData = deck.Where(c => !hand.Any(h => h.name == $"{c.cardName}_Card")).Random();
            var cardObj = CreateCard(cardData, isPlayer);
            if (cardObj == null) continue;
            if (content != null)
            {
                cardObj.transform.SetParent(content, false);
                cardObj.transform.localScale = cardDataMap[cardObj].originalScale;
                cardObj.transform.localPosition = Vector3.zero;
            }
            hand.Add(cardObj);
            if (isPlayer) UpdateHandUI();
            deck.Remove(cardData);
        }
    }

    public List<string> GetFreeCells(bool isPlayer, AttackType? attackType = null)
    {
        int[] validIndices = isPlayer
            ? attackType == AttackType.Melee ? new[] { 2, 3 } : attackType == AttackType.Ranged ? new[] { 1, 2 } : new[] { 1, 2, 3 }
            : attackType == AttackType.Melee ? new[] { 4, 5 } : attackType == AttackType.Ranged ? new[] { 5, 6 } : new[] { 4, 5, 6 };
        return (isPlayer ? PlayerCoords : BotCoords)
            .Where(c => cellDict.TryGetValue(c, out var cell) && cell.gridPos.x >= 1 && cell.gridPos.x <= GRID_WIDTH && grid[cell.gridPos.x - 1, cell.gridPos.y] == null && validIndices.Contains(cell.gridPos.x))
            .ToList();
    }

    public GameObject CreateCard(CardSO data, bool isPlayer)
    {
        if (data == null || cardObjectPrefab == null) return null;
        var obj = Instantiate(cardObjectPrefab);
        obj.name = $"{data.cardName}_Card";
        var instance = new CardInstanceData
        {
            currentHP = data.hp, maxHP = data.hp, currentATK = data.atk, baseATK = data.atk, isPlayerCard = isPlayer, attackType = data.attackType, rarity = data.rarity, faction = data.faction,
            originalScale = obj.transform.localScale, abilities = data.abilities.Select(a => new Ability { type = a.type, triggers = new List<TriggerType>(a.triggers), value = a.value, summonId = a.summonId, statType = a.statType, targetType = a.targetType, targetCount = a.targetCount, summonLocation = a.summonLocation }).ToList(),
            statuses = new List<Status>()
        };
        SetupCardUI(obj, instance);
        SetupCharacterModel(obj, data.characterPrefab, isPlayer, instance);
        SetupAbilitiesUI(obj, instance);
        cardDataMap[obj] = instance;
        UpdateEntityUI(obj, instance);
        return obj;
    }

    void SetupCardUI(GameObject obj, CardInstanceData data)
    {
        data.hpText = obj.transform.Find("HP ATK Text/HP/HP Text")?.GetComponent<TextMeshProUGUI>();
        data.atkText = obj.transform.Find("HP ATK Text/ATK/ATK Text")?.GetComponent<TextMeshProUGUI>();
        var hpIcon = obj.transform.Find("HP ATK Text/HP/HP Icon")?.GetComponent<Image>();
        var atkIcon = obj.transform.Find("HP ATK Text/ATK/ATK Icon")?.GetComponent<Image>();
        if (hpIcon != null) UpdateIconColors(obj, data.rarity, hpIcon);
        if (atkIcon != null)
        {
            atkIcon.sprite = data.attackType == AttackType.Ranged ? rangedAtkIcon : meleeAtkIcon;
            UpdateIconColors(obj, data.rarity, atkIcon);
        }
    }

    public void SetupAbilitiesUI(GameObject obj, IEntityInstanceData instance)
    {
        var abilitiesParent = obj.transform.Find("Abilities");
        if (abilitiesParent == null || abilityIconPrefab == null) return;

        foreach (Transform child in abilitiesParent)
        {
            Destroy(child.gameObject);
        }

        List<Ability> abilities = instance is CardInstanceData card ? card.abilities : (instance as WarlordInstanceData)?.abilities;
        if (abilities == null) return;

        foreach (var ab in abilities)
        {
            var iconObj = Instantiate(abilityIconPrefab, abilitiesParent);
            iconObj.name = $"{ab.type}_Icon";
            var image = iconObj.GetComponent<Image>();
            if (image != null) image.sprite = abilityManager.GetAbilitySprite(ab.type);
        }
    }

    void UpdateIconColors(GameObject entity, Rarity? rarity = null, Image icon = null)
    {
        var color = rarityColors.GetValueOrDefault(rarity ?? (cardDataMap.TryGetValue(entity, out var d) ? d.rarity : Rarity.Common), Color.white).WithAlpha(1f);
        icon?.SetColor(color);
    }

    void UpdateHandUI()
    {
        if (playerHandContent == null || playerHand.Count == 0) return;
        float startZ = -(playerHand.Count - 1) * cardSpacing / 2f;
        for (int i = 0; i < playerHand.Count; i++)
            if (playerHand[i] != null && playerHand[i] != draggedCard)
                playerHand[i].transform.localPosition = new Vector3(0, 0, startZ + i * cardSpacing);
    }

    public void UpdateEntityUI<T>(GameObject entity, T data) where T : IEntityInstanceData
    {
        if (entity == null || data == null) return;
        if (data.hpText != null && data.hpText.text != data.currentHP.ToString())
        {
            data.hpText.text = data.currentHP.ToString();
            StartCoroutine(AnimateTextScale(data.hpText));
        }
        if (data is CardInstanceData card && card.atkText != null && card.atkText.text != card.currentATK.ToString())
        {
            card.atkText.text = card.currentATK.ToString();
            StartCoroutine(AnimateTextScale(card.atkText));
        }
    }

    IEnumerator AnimateTextScale(TextMeshProUGUI text, float duration = 0.25f)
    {
        if (text == null) yield break;
        float o = text.fontSize, m = o * 1.3f, h = duration / 2f;
        for (float e = 0; e < h; e += Time.deltaTime) { text.fontSize = Mathf.Lerp(o, m, e / h); yield return null; }
        for (float e = 0; e < h; e += Time.deltaTime) { text.fontSize = Mathf.Lerp(m, o, e / h); yield return null; }
        text.fontSize = o;
    }

    IEnumerator AnimateDamageNumber(GameObject target, int damage, bool isMiss = false)
    {
        if (damageNumberPrefab == null || target == null) yield break;
        var cellTransform = cardDataMap.TryGetValue(target, out var cardData) ? cellDict.GetValueOrDefault(cardData.currentCoord)?.cellTransform : warlordDataMap.ContainsKey(target) ? cellDict.GetValueOrDefault(target == playerWarlordObject ? "PlayerWarlord" : "BotWarlord")?.cellTransform : null;
        if (cellTransform == null) yield break;
        var num = Instantiate(damageNumberPrefab, cellTransform);
        var txt = num.GetComponent<TextMeshProUGUI>();
        if (txt == null) { Destroy(num); yield break; }
        txt.text = isMiss ? "MISS" : (damage <= 0 ? "BLOCKED" : $"-{damage}");
        txt.fontSize = 0.1f; txt.color = txt.color.WithAlpha(1f); txt.alignment = TextAlignmentOptions.Center;
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            float t = elapsed += Time.deltaTime;
            txt.fontSize = t < 0.25f ? Mathf.Lerp(0.1f, 0.35f, Mathf.SmoothStep(0f, 1f, t / 0.25f)) : t < 0.4f ? Mathf.Lerp(0.35f, 0.3f, Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.15f)) : txt.fontSize;
            if (t >= 0.6f) txt.color = txt.color.WithAlpha(1f - (t - 0.6f) / 0.4f);
            yield return null;
        }
        Destroy(num);
    }

    public bool GetIsPlayerSide(GameObject obj) => cardDataMap.TryGetValue(obj, out var d) ? d.isPlayerCard : obj == playerWarlordObject;

    public void ApplyDamage(GameObject target, int damage, GameObject from = null)
    {
        IEntityInstanceData data;
        if (cardDataMap.TryGetValue(target, out var cardData)) data = cardData;
        else if (warlordDataMap.TryGetValue(target, out var warlordData)) data = warlordData;
        else 
        {
            Debug.LogWarning($"ApplyDamage: Target {target?.name} not found in cardDataMap or warlordDataMap");
            return;
        }

        var anim = data.characterModel?.GetComponent<Animator>();
        data.currentHP -= damage;
        UpdateEntityUI(target, data);
        if (warlordDataMap.ContainsKey(target)) StartCoroutine(HighlightWarlordCell(target == playerWarlordObject ? "PlayerWarlord" : "BotWarlord"));
        StartCoroutine(AnimateDamageNumber(target, damage));
        if (warlordDataMap.ContainsKey(target)) SyncWarlordHP(target);

        if (data.currentHP <= 0)
        {
            BattleEvents.OnDeath?.Invoke(target, from);
            if (data.currentHP <= 0)
            {
                StopAllCoroutinesForObject(target);
                if (cardData != null)
                {
                    grid[cardData.gridPos.x - 1, cardData.gridPos.y] = null;
                    StartCoroutine(Materialize(false, target, () =>
                    {
                        cardDataMap.Remove(target);
                        playerHand.Remove(target);
                        botHand.Remove(target);
                        Destroy(target);
                    }));
                }
                else
                {
                    StartCoroutine(ClearSide(GetIsPlayerSide(target)));
                }
            }
        }
        if (anim != null)
        {
            anim.SetInteger("state", 2);
            if (data.currentHP > 0) StartCoroutine(ResetTargetAnimation(anim));
        }
    }

    IEnumerator HighlightWarlordCell(string coord)
    {
        SetCellEmission(coord, true);
        yield return new WaitForSeconds(0.5f);
        SetCellEmission(coord, false);
    }

    IEnumerator DelayedPlace(GameObject card, string coord)
    {
        yield return new WaitForSeconds(0.5f);
        if (cellDict.TryGetValue(coord, out var cell) && cell?.cellTransform != null && cardDataMap.TryGetValue(card, out var data)) TryPlaceCard(card, coord, data.isPlayerCard);
    }

    public void SyncWarlordHP(GameObject warlordObj)
    {
        if (warlordObj == null || !warlordDataMap.TryGetValue(warlordObj, out var wd)) return;
        if (warlordObj == playerWarlordObject) playerWarlordHP = wd.currentHP;
        else enemyWarlordHP = wd.currentHP;
    }

    public List<GameObject> GetNeighbors(GameObject target, bool enemyOnly, bool isPlayerSide)
    {
        if (!cardDataMap.TryGetValue(target, out var d)) return new();
        var neighbors = new List<GameObject>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && d.gridPos.x + dx is var nx && nx >= 1 && nx <= GRID_WIDTH && d.gridPos.y + dy is var ny && ny >= 0 && ny < GRID_HEIGHT && grid[nx - 1, ny] != null)
                    if (!enemyOnly || cardDataMap[grid[nx - 1, ny]].isPlayerCard != isPlayerSide) neighbors.Add(grid[nx - 1, ny]);
        return neighbors;
    }

    public void StopAllCoroutinesForObject(GameObject obj)
    {
        if (obj != null && activeCoroutines.TryGetValue(obj, out var coros))
        {
            foreach (var c in coros) if (c != null) StopCoroutine(c);
            activeCoroutines.Remove(obj);
        }
    }

    IEnumerator GameTurnCycle()
    {
        while (playerWarlordHP > 0 && enemyWarlordHP > 0)
        {
            var turnOwner = isPlayerTurn ? playerWarlordObject : botWarlordObject;
            BattleEvents.OnTurnStart?.Invoke(turnOwner);
            hasPlacedCardThisTurn = battlePhase = false;
            if (isPlayerTurn)
            {
                yield return new WaitUntil(() => hasPlacedCardThisTurn || Input.GetKeyDown(KeyCode.Space));
                yield return new WaitUntil(() => !isPlacingCard);
                yield return StartCoroutine(BattlePhase(true));
                if (playerWarlordHP <= 0 || enemyWarlordHP <= 0) yield break;
                DrawCard(true);
                isPlayerTurn = false;
            }
            else
            {
                yield return new WaitForSeconds(1f);
                if (botHand.Count > 0 && GetFreeCells(false).Count > 0 && !hasPlacedCardThisTurn)
                {
                    var card = botHand.Where(c => cardDataMap.ContainsKey(c)).Random();
                    if (card != null)
                    {
                        var freeCells = GetFreeCells(false, cardDataMap[card].attackType);
                        if (freeCells.Count > 0) TryPlaceCard(card, freeCells.Random(), true, false);
                    }
                    else hasPlacedCardThisTurn = true;
                }
                else hasPlacedCardThisTurn = true;
                yield return new WaitUntil(() => !isPlacingCard);
                yield return StartCoroutine(BattlePhase(false));
                if (playerWarlordHP <= 0 || enemyWarlordHP <= 0) yield break;
                DrawCard(false);
                isPlayerTurn = true;
            }
            BattleEvents.OnTurnEnd?.Invoke(turnOwner);
            ResetAllPlayerCellEmissions();
            yield return null;
        }
    }

    public bool TryPlaceCard(GameObject card, string coord, bool isAuto, bool ignoreTurnLimits = false)
    {
        if (!ignoreTurnLimits && (battlePhase || hasPlacedCardThisTurn)) return false;
        if (isPlacingCard || !cellDict.TryGetValue(coord, out var cell) || cell?.cellTransform == null || !cardDataMap.TryGetValue(card, out var data)) return false;
        bool isPlayerZone = PlayerCoords.Contains(coord);
        var validIndices = data.isPlayerCard ? (data.attackType == AttackType.Melee ? new[] { 2, 3 } : new[] { 1, 2 }) : (data.attackType == AttackType.Melee ? new[] { 4, 5 } : new[] { 5, 6 });
        if (isPlayerZone != data.isPlayerCard || cell.gridPos.x < 1 || cell.gridPos.x > GRID_WIDTH || grid[cell.gridPos.x - 1, cell.gridPos.y] != null || (!ignoreTurnLimits && !validIndices.Contains(cell.gridPos.x))) return false;

        isPlacingCard = true;
        (data.isPlayerCard ? playerHand : botHand).Remove(card);
        data.isOnField = true;
        data.currentCoord = coord;
        data.gridPos = cell.gridPos;
        grid[cell.gridPos.x - 1, cell.gridPos.y] = card;
        card.transform.SetParent(cell.cellTransform, false);
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = data.originalScale;
        card.transform.position = cell.cellTransform.position;
        if (!data.isPlayerCard && data.characterModel != null) data.characterModel.transform.localRotation = data.originalCharacterRotation * Quaternion.Euler(0, 180, 0);
        StartCoroutine(Materialize(true, card, () =>
        {
            isPlacingCard = false;
            BattleEvents.OnSpawn?.Invoke(card);
        }));
        if (!ignoreTurnLimits) hasPlacedCardThisTurn = true;
        if (data.isPlayerCard) UpdateHandUI();
        return true;
    }

    IEnumerator MoveCard(Transform t, Vector3 from, Vector3 to, float duration, System.Action onComplete = null)
    {
        if (t == null) { onComplete?.Invoke(); yield break; }
        for (float e = 0; e < duration; e += Time.deltaTime) { t.position = Vector3.Lerp(from, to, e / duration); yield return null; }
        t.position = to; onComplete?.Invoke();
    }

    void SetCellEmission(string coord, bool enable, AttackType? attackType = null, bool isBright = false)
    {
        if (cellDict.TryGetValue(coord, out var cell) && cell.cellTransform != null && cell.cellTransform.TryGetComponent<Renderer>(out var renderer) && renderer.material.HasProperty("_EmissionColor"))
        {
            var color = enable ? (attackType == AttackType.Melee ? meleeHighlightColor : attackType == AttackType.Ranged ? rangedHighlightColor : new Color(0.1f, 0.1f, 0.1f)) * (isBright ? 1.5f : 1f) : Color.black;
            renderer.material.SetColor("_EmissionColor", color);
            if (enable) renderer.material.EnableKeyword("_EMISSION"); else renderer.material.DisableKeyword("_EMISSION");
        }
    }

    void ResetAllPlayerCellEmissions()
    {
        foreach (var coord in highlightedCells) SetCellEmission(coord, false);
        highlightedCells.Clear();
        lastHighlightedCell = null;
    }

    void Update()
    {
        if (!isPlayerTurn || battlePhase || hasPlacedCardThisTurn || isPlacingCard) { ResetAllPlayerCellEmissions(); return; }
        var mask = 1 << LayerMask.NameToLayer("CellsLayer");
        var pos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        var ray = Camera.main.ScreenPointToRay(pos);
        bool began = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        bool moved = Input.GetMouseButton(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved);
        bool ended = Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);

        if (began && Physics.Raycast(ray, out var cardHit, 100f))
        {
            var card = cardHit.collider.gameObject;
            if (playerHand.Contains(card) && cardDataMap.TryGetValue(card, out var d) && !d.isOnField && !d.isAttacking)
            {
                draggedCard = card;
                startPosition = card.transform.position;
                dragZ = Camera.main.WorldToScreenPoint(card.transform.position).z;
                card.transform.SetAsLastSibling();
                card.transform.localScale *= 1.1f;
                highlightedCells = GetFreeCells(true, d.attackType);
                foreach (var coord in highlightedCells) SetCellEmission(coord, true, d.attackType, false);
            }
        }
        if (draggedCard != null && moved)
        {
            draggedCard.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(pos.x, pos.y, dragZ));
            if (Physics.Raycast(ray, out var cellHit, 100f, mask) && cellDict.TryGetValue(cellHit.collider.name, out var cell) && cell.gridPos.x >= 1 && cell.gridPos.x <= GRID_WIDTH && grid[cell.gridPos.x - 1, cell.gridPos.y] == null && PlayerCoords.Contains(cellHit.collider.name) && highlightedCells.Contains(cellHit.collider.name))
            {
                var coord = cellHit.collider.name;
                if (lastHighlightedCell != coord)
                {
                    if (!string.IsNullOrEmpty(lastHighlightedCell) && highlightedCells.Contains(lastHighlightedCell)) SetCellEmission(lastHighlightedCell, true, cardDataMap[draggedCard].attackType, false);
                    SetCellEmission(coord, true, cardDataMap[draggedCard].attackType, true);
                    lastHighlightedCell = coord;
                }
            }
            else if (!string.IsNullOrEmpty(lastHighlightedCell))
            {
                if (highlightedCells.Contains(lastHighlightedCell)) SetCellEmission(lastHighlightedCell, true, cardDataMap[draggedCard].attackType, false);
                lastHighlightedCell = null;
            }
        }
        if (draggedCard != null && ended)
        {
            bool placed = Physics.Raycast(ray, out var cellHit, 100f, mask) && cellDict.TryGetValue(cellHit.collider.name, out var cell) && cell.gridPos.x >= 1 && cell.gridPos.x <= GRID_WIDTH && grid[cell.gridPos.x - 1, cell.gridPos.y] == null && PlayerCoords.Contains(cellHit.collider.name) && highlightedCells.Contains(cellHit.collider.name) && TryPlaceCard(draggedCard, cellHit.collider.name, false, false);
            ResetAllPlayerCellEmissions();
            if (!placed && draggedCard != null)
            {
                draggedCard.transform.position = startPosition;
                draggedCard.transform.localScale = cardDataMap[draggedCard].originalScale;
                UpdateHandUI();
            }
            draggedCard = null;
        }
    }

    IEnumerator BattlePhase(bool isPlayerPhase)
    {
        battlePhase = true;
        foreach (var card in cardDataMap.Keys) card?.GetComponent<Collider>()?.SetEnabled(false);
        yield return new WaitForSeconds(0.5f);

        var order = new List<(int, int)>();
        var range = isPlayerPhase ? Enumerable.Range(1, 3).Reverse() : Enumerable.Range(4, 3);
        for (int y = 0; y < GRID_HEIGHT; y++)
            foreach (int x in range)
                order.Add((x, y));

        foreach (var (x, y) in order)
        {
            var card = grid[x - 1, y];
            if (x >= 1 && x <= GRID_WIDTH && card != null && cardDataMap.TryGetValue(card, out var d) && d.isPlayerCard == isPlayerPhase && (d.attackType == AttackType.Ranged || !IsAllyBlocking(card)))
            {
                var attackContext = new AttackContext { attacker = card };
                BattleEvents.OnBeforeAttack?.Invoke(attackContext);
                if (!attackContext.canAct || attackContext.attackCount <= 0) continue;
                yield return StartCoroutine(ExecuteAttacks(card, attackContext.attackCount));
                if (playerWarlordHP <= 0 || enemyWarlordHP <= 0) yield break;
            }
        }

        grid.Iterate((x, y) =>
        {
            if (grid[x, y] is var t && t != null && cardDataMap.TryGetValue(t, out var data) && data.currentHP <= 0)
            {
                grid[x, y] = null;
                StartCoroutine(Materialize(false, t, () => { cardDataMap.Remove(t); playerHand.Remove(t); botHand.Remove(t); Destroy(t); }));
            }
        });

        battlePhase = false;
        foreach (var card in cardDataMap.Keys) card?.GetComponent<Collider>()?.SetEnabled(true);
    }

    IEnumerator ExecuteAttacks(GameObject card, int attacks)
    {
        if (card == null || !cardDataMap.TryGetValue(card, out var d)) yield break;

        bool isMelee = d.attackType == AttackType.Melee;
        Vector3 startPos = card.transform.position;
        Vector2Int origGrid = d.gridPos;
        string origCoord = d.currentCoord;
        Vector2Int? movedTo = null;

        for (int a = 0; a < attacks; a++)
        {
            var attackData = FindAttackTarget(card);
            if (attackData.target == null || !(cardDataMap.ContainsKey(attackData.target) || warlordDataMap.ContainsKey(attackData.target))) break;

            // Move to attack position for melee attacks if needed (only for first attack in multi-attack)
            if (isMelee && attackData.attackPos.HasValue && (a == 0 || attackData.attackPos.Value != movedTo))
            {
                movedTo = attackData.attackPos;
                var attackPosWorld = GetWorldPosition(movedTo.Value);
                if (attackPosWorld != Vector3.zero)
                {
                    grid[d.gridPos.x - 1, d.gridPos.y] = null;
                    d.gridPos = movedTo.Value;
                    d.currentCoord = $"{(char)('A' + movedTo.Value.y)}{movedTo.Value.x}";
                    grid[movedTo.Value.x - 1, movedTo.Value.y] = card;
                    yield return StartCoroutine(MoveCard(card.transform, card.transform.position, attackPosWorld, 0.3f));
                }
            }

            // Execute the attack
            yield return StartCoroutine(ExecuteAttack(card, attackData));

            if (playerWarlordHP <= 0 || enemyWarlordHP <= 0)
            {
                yield return StartCoroutine(ClearSide(playerWarlordHP <= 0));
                yield break;
            }
        }

        // Return to original position for melee attacks after all attacks
        if (isMelee && movedTo.HasValue && card != null)
        {
            var returnPos = GetWorldPosition(origGrid);
            if (returnPos != Vector3.zero)
            {
                grid[d.gridPos.x - 1, d.gridPos.y] = null;
                d.gridPos = origGrid;
                d.currentCoord = origCoord;
                grid[origGrid.x - 1, origGrid.y] = card;
                yield return StartCoroutine(MoveCard(card.transform, card.transform.position, returnPos, 0.3f));
                if (!d.isPlayerCard && d.characterModel != null)
                    d.characterModel.transform.localRotation = d.originalCharacterRotation * Quaternion.Euler(0, 180, 0);
                UpdateEntityUI(card, d);
            }
        }
    }

    public IEnumerator ExecuteAttack(GameObject attacker, (GameObject target, Vector2Int? attackPos) attackData)
    {
        if (attacker == null || !cardDataMap.TryGetValue(attacker, out var attackerData)) yield break;
        attackerData.isAttacking = true;
        var anim = attackerData.characterModel?.GetComponent<Animator>();
        var startPos = attacker.transform.position;
        var origGrid = attackerData.gridPos;
        var origCoord = attackerData.currentCoord;
        var request = new DamageRequest(attacker, attackData.target, attackerData.currentATK, attackerData.attackType, DamageSource.Attack, true);
        DamageResult result = default;
        BattleEvents.OnAttack?.Invoke(attacker, attackData.target);

        void RequestDamage()
        {
            var resolver = CombatRequests.RequestDamage;
            result = resolver != null ? resolver(request) : default;
        }

        if (attackerData.attackType == AttackType.Ranged)
        {
            anim?.SetInteger("state", 1);
            yield return new WaitForSeconds(RANGED_ATTACK_ANIMATION_DELAY);
            anim?.SetInteger("state", 0);
            var targetPos = GetWorldPositionForTarget(attackData.target, attackerData.isPlayerCard);
            if (targetPos != Vector3.zero && attackerData.firePoint != null)
            {
                var projectile = Instantiate(attackerData.firePoint.gameObject, attackerData.firePoint.position, attackerData.firePoint.rotation);
                var particleSystem = projectile.GetComponent<ParticleSystem>();
                var collisionInstance = projectile.GetComponent<ParticleCollisionInstance>();
                if (particleSystem != null && collisionInstance != null)
                {
                    Debug.Log($"Ranged attack: Instantiated FirePoint copy for {attacker.name} targeting {attackData.target?.name}");
                    Vector3 direction = attackerData.isPlayerCard ? Vector3.forward : Vector3.back;
                    projectile.transform.rotation = Quaternion.LookRotation(direction);
                    particleSystem.Clear(true);
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particleSystem.Play();
                    collisionInstance.Initialize(new Vector3(projectile.transform.position.x, projectile.transform.position.y, targetPos.z), RequestDamage);
                    yield return new WaitForSeconds(RANGED_ATTACK_ANIMATION_DELAY);
                    Destroy(projectile);
                }
                else
                {
                    Debug.LogWarning($"Ranged attack: Missing ParticleSystem or ParticleCollisionInstance on FirePoint copy for {attacker.name}");
                    RequestDamage();
                }
            }
            else
            {
                Debug.LogWarning($"Ranged attack: Invalid target position or missing FirePoint for {attacker.name}");
                RequestDamage();
            }
        }
        else
        {
            // Move to attack position if needed (handled in ExecuteAttacks for multi-attacks)
            if (attackData.attackPos is { } pos)
            {
                var attackPosWorld = GetWorldPosition(pos);
                if (attackPosWorld != Vector3.zero)
                {
                    grid[attackerData.gridPos.x - 1, attackerData.gridPos.y] = null;
                    attackerData.gridPos = pos;
                    attackerData.currentCoord = $"{(char)('A' + pos.y)}{pos.x}";
                    grid[pos.x - 1, pos.y] = attacker;
                    yield return StartCoroutine(MoveCard(attacker.transform, startPos, attackPosWorld, 0.3f));
                }
            }
            anim?.SetInteger("state", 1);
            yield return new WaitForSeconds(DAMAGE_ANIMATION_DELAY);
            if (attackData.target != null)
            {
                RequestDamage();
                if (!result.isMiss)
                {
                    yield return new WaitForSeconds(DAMAGE_ANIMATION_DELAY);
                }
            }
            anim?.SetInteger("state", 0);
            // Return to original position for melee attacks after single attack
            if (attackerData.attackType == AttackType.Melee && attackData.attackPos.HasValue && attacker != null)
            {
                var returnPos = GetWorldPosition(origGrid);
                if (returnPos != Vector3.zero)
                {
                    grid[attackerData.gridPos.x - 1, attackerData.gridPos.y] = null;
                    attackerData.gridPos = origGrid;
                    attackerData.currentCoord = origCoord;
                    grid[origGrid.x - 1, origGrid.y] = attacker;
                    yield return StartCoroutine(MoveCard(attacker.transform, attacker.transform.position, returnPos, 0.3f));
                    if (!attackerData.isPlayerCard && attackerData.characterModel != null)
                        attackerData.characterModel.transform.localRotation = attackerData.originalCharacterRotation * Quaternion.Euler(0, 180, 0);
                    UpdateEntityUI(attacker, attackerData);
                }
            }
        }

        attackerData.isAttacking = false;
    }

    void DrawCard(bool isPlayer)
    {
        var hand = isPlayer ? playerHand : botHand;
        var deck = isPlayer ? playerDeck : botDeck;
        var content = isPlayer ? playerHandContent : null;
        if (hand.Count >= maxHandSize || deck.Count == 0) return;
        var data = deck.Where(c => !hand.Any(h => h.name == $"{c.cardName}_Card")).Random();
        if (data == null) return;
        var obj = CreateCard(data, isPlayer);
        if (obj == null) return;
        if (content != null)
        {
            obj.transform.SetParent(content, false);
            obj.transform.localScale = cardDataMap[obj].originalScale;
            obj.transform.localPosition = Vector3.zero;
        }
        hand.Add(obj);
        if (isPlayer) UpdateHandUI();
        deck.Remove(data);
    }

    bool IsAllyBlocking(GameObject card)
    {
        if (!cardDataMap.TryGetValue(card, out var d)) return false;
        int step = d.isPlayerCard ? 1 : -1, endX = d.isPlayerCard ? 3 : 4;
        for (int x = d.gridPos.x + step; d.isPlayerCard ? x <= endX : x >= endX; x += step)
            if (x >= 1 && x <= GRID_WIDTH && grid[x - 1, d.gridPos.y] != null) return true;
        return false;
    }

    (GameObject target, Vector2Int? attackPos) FindAttackTarget(GameObject card)
    {
        if (!cardDataMap.TryGetValue(card, out var d)) return (null, null);
        int startX = d.isPlayerCard ? 4 : 3, endX = d.isPlayerCard ? GRID_WIDTH + 1 : 0, step = d.isPlayerCard ? 1 : -1;

        // Ranged attack: Find first target in line
        if (d.attackType == AttackType.Ranged)
        {
            for (int x = startX; x != endX; x += step)
            {
                if (x >= 1 && x <= GRID_WIDTH && grid[x - 1, d.gridPos.y] is var t && t != null &&
                    cardDataMap.TryGetValue(t, out var td) && td.isPlayerCard != d.isPlayerCard && td.currentHP > 0)
                {
                    return (t, null);
                }
            }
            var warlord = d.isPlayerCard ? botWarlordObject : playerWarlordObject;
            if (warlord != null && warlordDataMap.TryGetValue(warlord, out var warlordData) && warlordData.currentHP > 0)
            {
                return (warlord, null);
            }
            return (null, null);
        }

        // Melee attack: Find target and attack position
        int warlordX = d.isPlayerCard ? GRID_WIDTH : 1;
        for (int x = startX; x != warlordX + step; x += step)
        {
            if (x >= 1 && x <= GRID_WIDTH && grid[x - 1, d.gridPos.y] is var t && t != null &&
                cardDataMap.TryGetValue(t, out var td) && td.isPlayerCard != d.isPlayerCard && td.currentHP > 0)
            {
                int ax = x + (d.isPlayerCard ? -1 : 1);
                Vector2Int? attackPos = (ax >= 1 && ax <= GRID_WIDTH && grid[ax - 1, d.gridPos.y] == null)
                    ? new Vector2Int(ax, d.gridPos.y)
                    : null;
                return (t, attackPos);
            }
        }

        // Check if warlord can be attacked
        var warlordTarget = d.isPlayerCard ? botWarlordObject : playerWarlordObject;
        if (warlordTarget != null && warlordDataMap.TryGetValue(warlordTarget, out var warlordTargetData) && warlordTargetData.currentHP > 0)
        {
            if (d.gridPos.x == warlordX)
            {
                return (warlordTarget, null);
            }
            else if (warlordX >= 1 && warlordX <= GRID_WIDTH && grid[warlordX - 1, d.gridPos.y] == null)
            {
                return (warlordTarget, new Vector2Int(warlordX, d.gridPos.y));
            }
        }

        // If no target, find nearest free cell
        Vector2Int? nearestPos = FindNearestFreeCell(card, warlordX);
        return (null, nearestPos);
    }

    public (GameObject target, Vector2Int? attackPos) FindCounterAttackData(GameObject counterer, GameObject attacker)
    {
        if (!cardDataMap.TryGetValue(counterer, out var cd) || !cardDataMap.TryGetValue(attacker, out var ad)) return (attacker, null);
        if (cd.attackType == AttackType.Ranged) return (attacker, null);
        int step = cd.isPlayerCard ? 1 : -1, targetX = ad.gridPos.x;
        for (int x = cd.gridPos.x + step; cd.isPlayerCard ? x <= targetX : x >= targetX; x += step)
            if (x >= 1 && x <= GRID_WIDTH && grid[x - 1, cd.gridPos.y] == null) return (attacker, new Vector2Int(x, cd.gridPos.y));
        return (attacker, null);
    }

    Vector2Int? FindNearestFreeCell(GameObject card, int targetX)
    {
        if (!cardDataMap.TryGetValue(card, out var d)) return null;
        int step = d.isPlayerCard ? 1 : -1;
        for (int x = d.gridPos.x + step; d.isPlayerCard ? x <= targetX : x >= targetX; x += step)
            if (x >= 1 && x <= GRID_WIDTH && grid[x - 1, d.gridPos.y] == null) return new Vector2Int(x, d.gridPos.y);
        return null;
    }

    Vector3 GetWorldPositionForTarget(GameObject target, bool isPlayerAttacker)
    {
        if (cardDataMap.TryGetValue(target, out var d) && cellDict.TryGetValue(d.currentCoord, out var cell)) return cell.cellTransform.position;
        return cellDict.TryGetValue(isPlayerAttacker ? "BotWarlord" : "PlayerWarlord", out var wCell) ? wCell.cellTransform.position : Vector3.zero;
    }

    Vector3 GetWorldPosition(Vector2Int pos) => cellDict.TryGetValue($"{(char)('A' + pos.y)}{pos.x}", out var cell) ? cell.cellTransform.position : Vector3.zero;

    IEnumerator ResetTargetAnimation(Animator anim)
    {
        yield return new WaitForSeconds(DAMAGE_ANIMATION_DELAY);
        try { anim?.SetInteger("state", 0); } catch (MissingReferenceException) { }
    }

    IEnumerator ClearSide(bool isPlayer)
    {
        battlePhase = isPlacingCard = true;
        var hand = isPlayer ? playerHand : botHand;
        var warlord = isPlayer ? playerWarlordObject : botWarlordObject;
        var toDissolve = new List<GameObject>(hand);
        grid.Iterate((x, y) => { if (grid[x, y] is var c && c != null && cardDataMap.TryGetValue(c, out var d) && d.isPlayerCard == isPlayer) { toDissolve.Add(c); grid[x, y] = null; } });
        if (warlord != null && warlordDataMap.ContainsKey(warlord)) toDissolve.Add(warlord);
        foreach (var obj in toDissolve)
        {
            if (obj == null) continue;
            StopAllCoroutinesForObject(obj);
            yield return StartCoroutine(Materialize(false, obj, () => { cardDataMap.Remove(obj); warlordDataMap.Remove(obj); hand.Remove(obj); Destroy(obj); }));
        }
        yield return new WaitForSeconds(materializeDuration);
        if (isPlayer) UpdateHandUI();
        battlePhase = isPlacingCard = false;
        if (isPlayer) playerWarlordObject = null; else botWarlordObject = null;
    }

    public IEnumerator Materialize(bool appear, GameObject obj, System.Action onComplete = null)
    {
        if (obj == null) { onComplete?.Invoke(); yield break; }
        IEntityInstanceData data = null;
        if (cardDataMap.ContainsKey(obj)) data = cardDataMap[obj];
        else if (warlordDataMap.ContainsKey(obj)) data = warlordDataMap[obj];
        var model = data?.characterModel ?? obj;
        var mat = model?.GetComponentInChildren<SkinnedMeshRenderer>()?.material;
        if (mat == null || !mat.HasProperty("_Materialize")) { onComplete?.Invoke(); yield break; }
        if (!activeCoroutines.ContainsKey(obj)) activeCoroutines[obj] = new List<Coroutine>();
        float t = 0f, start = appear ? 0f : 1f, end = appear ? 1f : 0f;
        mat.SetFloat("_Materialize", start);
        while (t < materializeDuration && obj != null && model != null && mat != null)
        {
            mat.SetFloat("_Materialize", Mathf.Lerp(start, end, t / materializeDuration));
            t += Time.deltaTime;
            yield return null;
        }
        if (mat != null) mat.SetFloat("_Materialize", end);
        activeCoroutines.Remove(obj);
        onComplete?.Invoke();
    }

    private void HandleDamageResolved(DamageReport report)
    {
        var request = report.request;
        var result = report.result;

        if (request.target == null) return;

        if (result.isMiss)
        {
            StartCoroutine(AnimateDamageNumber(request.target, 0, true));
        }
        else if (result.finalDamage > 0)
        {
            ApplyDamage(request.target, result.finalDamage, request.attacker);
        }
        else
        {
            StartCoroutine(AnimateDamageNumber(request.target, 0));
        }

        if (request.triggerAfterEffects)
        {
            CombatRequests.DamageApplied?.Invoke(report);
        }
    }

    private void HandleRequestSummon(SummonRequest request)
    {
        if (request.summoner == null || request.summonId < 0) return;
        var summon = cardDatabase.FirstOrDefault(c => c.id == request.summonId);
        if (summon == null) return;

        var isPlayer = GetIsPlayerSide(request.summoner);
        var count = Mathf.Max(1, request.count);
        List<string> possibleCoords = new List<string>();
        switch (request.summonLocation)
        {
            case SummonLocation.Behind:
                if (cardDataMap.TryGetValue(request.summoner, out var data))
                {
                    int col = data.gridPos.y;
                    int[] targetRows = isPlayer ? new[] { 2, 1 } : new[] { 5, 6 };
                    foreach (int row in targetRows)
                    {
                        if (row >= 1 && row <= 6 && grid[row - 1, col] == null)
                        {
                            possibleCoords.Add($"{(char)('A' + col)}{row}");
                        }
                    }
                }
                break;
            case SummonLocation.Random:
                possibleCoords = GetFreeCells(isPlayer, summon.attackType);
                break;
            case SummonLocation.FrontRow:
                int frontX = isPlayer ? 3 : 4;
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    if (grid[frontX - 1, y] == null)
                    {
                        possibleCoords.Add($"{(char)('A' + y)}{frontX}");
                    }
                }
                break;
        }

        if (possibleCoords.Count == 0) return;

        count = Mathf.Min(count, possibleCoords.Count);
        for (int i = 0; i < count && possibleCoords.Count > 0; i++)
        {
            string coord = possibleCoords[0];
            possibleCoords.RemoveAt(0);
            var newCard = CreateCard(summon, isPlayer);
            if (newCard == null) continue;
            bool placed = TryPlaceCard(newCard, coord, true, true);
            if (!placed)
            {
                if (cardDataMap.ContainsKey(newCard)) cardDataMap.Remove(newCard);
                for (int x = 0; x < GRID_WIDTH; x++)
                    for (int y = 0; y < GRID_HEIGHT; y++)
                        if (grid[x, y] == newCard) grid[x, y] = null;
                Destroy(newCard);
            }
        }
    }

    private void HandleRequestCounterAttack(CounterAttackRequest request)
    {
        if (request.attacker == null || request.target == null) return;
        var counterData = FindCounterAttackData(request.attacker, request.target);
        if (counterData.target != null)
        {
            StartCoroutine(ExecuteAttack(request.attacker, counterData));
        }
    }

    private void HandleRequestSplash(SplashRequest request)
    {
        if (request.attacker == null || request.target == null || request.damage <= 0) return;
        foreach (var neighbor in GetNeighbors(request.target, true, GetIsPlayerSide(request.attacker)))
        {
            var resolver = CombatRequests.RequestDamage;
            if (resolver != null)
            {
                resolver(new DamageRequest(request.attacker, neighbor, request.damage, request.attackType, DamageSource.Ability, false));
            }
        }
    }

    private void HandleRequestLineDamage(LineDamageRequest request)
    {
        if (request.attacker == null || request.target == null || request.damage <= 0) return;
        if (!cardDataMap.TryGetValue(request.attacker, out var actData)) return;
        if (!cardDataMap.TryGetValue(request.target, out var targetData)) return;

        var step = actData.isPlayerCard ? 1 : -1;
        var y = targetData.gridPos.y;
        for (int x = targetData.gridPos.x + step; actData.isPlayerCard ? x <= GRID_WIDTH : x >= 1; x += step)
        {
            if (x >= 1 && x <= GRID_WIDTH && grid[x - 1, y] != null && cardDataMap[grid[x - 1, y]].isPlayerCard != actData.isPlayerCard)
            {
                var resolver = CombatRequests.RequestDamage;
                if (resolver != null)
                {
                    resolver(new DamageRequest(request.attacker, grid[x - 1, y], request.damage, request.attackType, DamageSource.Ability, false));
                }
            }
        }
    }
}

public static class Extensions
{
    public static void Iterate<T>(this T[,] array, System.Action<int, int> action)
    {
        for (int x = 0; x < array.GetLength(0); x++)
            for (int y = 0; y < array.GetLength(1); y++)
                action(x, y);
    }

    public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

    public static void SetEnabled(this Collider c, bool enabled)
    {
        if (c != null) c.enabled = enabled;
    }

    public static void SetColor(this Image i, Color c)
    {
        if (i != null) i.color = c;
    }

    public static T Random<T>(this IEnumerable<T> list) => list.Any() ? list.ElementAt(UnityEngine.Random.Range(0, list.Count())) : default;
}

public static class BattleEvents
{
    public static System.Action<GameObject> OnTurnStart;
    public static System.Action<GameObject> OnTurnEnd;
    public static System.Action<GameObject> OnSpawn;
    public static System.Action<AttackContext> OnBeforeAttack;
    public static System.Action<GameObject, GameObject> OnAttack;
    public static System.Action<GameObject, GameObject> OnDeath;
}

public class AttackContext
{
    public GameObject attacker;
    public int attackCount = 1;
    public bool canAct = true;
}

public class DamageContext
{
    public GameObject attacker;
    public GameObject target;
    public int damage;
    public AttackType attackType;
    public bool isMiss;
}
