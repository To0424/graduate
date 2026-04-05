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
        }
    }

    void Start()
    {
        Load();
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
