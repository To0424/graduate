using UnityEngine;
using System.Collections.Generic;
using System;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    public SkillTreeData skillTreeData;

    private HashSet<string> unlockedNodes = new HashSet<string>();

    public static event Action<string> OnNodeUnlocked;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (skillTreeData == null) skillTreeData = BuildPlaceholderTree();
    }

    void Start()
    {
        Load();
    }

    void OnEnable()  { WaveSpawner.OnRoundStart += HandleRoundStart; }
    void OnDisable() { WaveSpawner.OnRoundStart -= HandleRoundStart; }

    void HandleRoundStart(int _)
    {
        if (CurrencyManager.Instance == null) return;
        BuffEffect buffs = GetTotalBuffs();
        if (buffs.bonusGoldPerRound > 0)
            CurrencyManager.Instance.AddGold(buffs.bonusGoldPerRound);
    }

    /// <summary>Built once if no SkillTreeData asset is provided. Lets the UI work in any scene.</summary>
    static SkillTreeData BuildPlaceholderTree()
    {
        SkillTreeData d = ScriptableObject.CreateInstance<SkillTreeData>();
        d.nodes = new SkillNode[]
        {
            // Social
            new SkillNode {
                nodeName = "Tutoring", description = "+10% tower damage.",
                cost = 1, section = SkillSection.Social,
                buff = new BuffEffect { damageMultiplier = 1.1f, rangeMultiplier = 1f, fireRateMultiplier = 1f },
                prerequisiteNodeNames = new string[0]
            },
            new SkillNode {
                nodeName = "Study Buddies", description = "+1 starting life.",
                cost = 1, section = SkillSection.Social,
                buff = new BuffEffect { bonusLives = 1 },
                prerequisiteNodeNames = new string[] { "Tutoring" }
            },
            // Internship
            new SkillNode {
                nodeName = "Resume Polish", description = "+10% tower range.",
                cost = 1, section = SkillSection.Internship,
                buff = new BuffEffect { damageMultiplier = 1f, rangeMultiplier = 1.1f, fireRateMultiplier = 1f },
                prerequisiteNodeNames = new string[0]
            },
            // Part-time Work
            new SkillNode {
                nodeName = "Cup of Coffee", description = "+20 gold at the start of every round.",
                cost = 1, section = SkillSection.PartTimeWork,
                buff = new BuffEffect { bonusGoldPerRound = 20 },
                prerequisiteNodeNames = new string[0]
            },
            new SkillNode {
                nodeName = "Part-time Cashier", description = "+50 starting gold.",
                cost = 1, section = SkillSection.PartTimeWork,
                buff = new BuffEffect { bonusStartGold = 50 },
                prerequisiteNodeNames = new string[0]
            },
            // Certifications
            new SkillNode {
                nodeName = "Certified Sniper", description = "+10% fire rate.",
                cost = 1, section = SkillSection.Certifications,
                buff = new BuffEffect { damageMultiplier = 1f, rangeMultiplier = 1f, fireRateMultiplier = 1.1f },
                prerequisiteNodeNames = new string[0]
            }
        };
        return d;
    }

    public bool IsNodeUnlocked(string nodeName) => unlockedNodes.Contains(nodeName);

    public bool CanUnlockNode(SkillNode node)
    {
        if (unlockedNodes.Contains(node.nodeName)) return false;
        if (SkillPointManager.Instance == null || SkillPointManager.Instance.SkillPoints < node.cost) return false;

        // Check prerequisites
        if (node.prerequisiteNodeNames != null)
        {
            foreach (string prereq in node.prerequisiteNodeNames)
            {
                if (!unlockedNodes.Contains(prereq)) return false;
            }
        }
        return true;
    }

    public bool UnlockNode(string nodeName)
    {
        SkillNode node = FindNode(nodeName);
        if (node == null || !CanUnlockNode(node)) return false;

        SkillPointManager.Instance.SpendSkillPoints(node.cost);
        unlockedNodes.Add(nodeName);
        OnNodeUnlocked?.Invoke(nodeName);
        Save();
        return true;
    }

    public BuffEffect GetTotalBuffs()
    {
        BuffEffect total = BuffEffect.Default();
        if (skillTreeData == null) return total;

        foreach (SkillNode node in skillTreeData.nodes)
        {
            if (unlockedNodes.Contains(node.nodeName) && node.buff != null)
            {
                total.AddBuff(node.buff);
            }
        }
        return total;
    }

    SkillNode FindNode(string name)
    {
        if (skillTreeData == null) return null;
        foreach (SkillNode node in skillTreeData.nodes)
        {
            if (node.nodeName == name) return node;
        }
        return null;
    }

    public void Save()
    {
        string data = string.Join(",", unlockedNodes);
        PlayerPrefs.SetString("UnlockedSkillNodes", data);
        PlayerPrefs.Save();
    }

    public void Load()
    {
        string data = PlayerPrefs.GetString("UnlockedSkillNodes", "");
        unlockedNodes.Clear();
        if (!string.IsNullOrEmpty(data))
        {
            foreach (string node in data.Split(','))
            {
                if (!string.IsNullOrEmpty(node))
                    unlockedNodes.Add(node);
            }
        }
    }

    public void ResetTree()
    {
        unlockedNodes.Clear();
        PlayerPrefs.DeleteKey("UnlockedSkillNodes");
    }
}
