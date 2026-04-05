using UnityEngine;

public enum SkillSection
{
    Social,
    Internship,
    PartTimeWork,
    Certifications
}

[System.Serializable]
public class SkillNode
{
    public string nodeName;
    public string description;
    public int cost = 1;  // skill points
    public SkillSection section;
    public BuffEffect buff;
    public string[] prerequisiteNodeNames;  // nodes that must be unlocked first
}

[CreateAssetMenu(fileName = "NewSkillTreeData", menuName = "Graduation/Skill Tree Data")]
public class SkillTreeData : ScriptableObject
{
    public SkillNode[] nodes;
}
