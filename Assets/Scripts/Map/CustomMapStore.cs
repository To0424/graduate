using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON persistence for player- / dev-authored maps. Maps are stored as a
/// folder of .json files under <c>Application.persistentDataPath/CustomMaps/</c>
/// so they survive across editor sessions and built-game sessions, and can be
/// shared by copying the file.
///
/// Serialization is deliberately simple: a flat <see cref="Data"/> POCO that
/// mirrors <see cref="PathPatternData"/> plus per-spawn paths.
/// </summary>
public static class CustomMapStore
{
    [Serializable]
    public class SerializableSpawn
    {
        public Vector3   spawn;
        public Vector3[] waypoints;       // ordered, EXCLUDING the spawn itself
    }

    [Serializable]
    public class Data
    {
        public string              mapName;
        public int                 difficultyTier = 1;
        public Vector3             exitPosition;
        public List<SerializableSpawn> spawns      = new List<SerializableSpawn>();
        public List<Vector3>       towerSlots      = new List<Vector3>();
    }

    static string Folder
    {
        get
        {
            string p = Path.Combine(Application.persistentDataPath, "CustomMaps");
            if (!Directory.Exists(p)) Directory.CreateDirectory(p);
            return p;
        }
    }

    static string FilePath(string mapName) => Path.Combine(Folder, mapName + ".json");

    public static string[] ListMapNames()
    {
        if (!Directory.Exists(Folder)) return new string[0];
        string[] files = Directory.GetFiles(Folder, "*.json");
        string[] names = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
            names[i] = Path.GetFileNameWithoutExtension(files[i]);
        return names;
    }

    public static bool Exists(string mapName) => File.Exists(FilePath(mapName));

    public static void Save(Data data)
    {
        if (data == null || string.IsNullOrEmpty(data.mapName)) return;
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(FilePath(data.mapName), json);
        Debug.Log($"[CustomMapStore] Saved '{data.mapName}' to {FilePath(data.mapName)}");
    }

    public static Data LoadRaw(string mapName)
    {
        string path = FilePath(mapName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonUtility.FromJson<Data>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[CustomMapStore] Failed to load '{mapName}': {e.Message}");
            return null;
        }
    }

    public static void Delete(string mapName)
    {
        string path = FilePath(mapName);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// Loads a saved map and converts it into a <see cref="PathPatternData"/>
    /// instance suitable for <see cref="PathManager"/>. The first spawn's
    /// waypoint list is mirrored into the legacy <see cref="PathPatternData.waypointPositions"/>
    /// for backward compatibility; per-spawn paths are written to
    /// <see cref="PathPatternData.spawnWaypointPositions"/>.
    /// </summary>
    public static PathPatternData Load(string mapName)
    {
        Data raw = LoadRaw(mapName);
        if (raw == null) return null;

        PathPatternData p = ScriptableObject.CreateInstance<PathPatternData>();
        p.patternName       = raw.mapName;
        p.difficultyTier    = Mathf.Clamp(raw.difficultyTier, 1, 4);
        p.exitPosition      = raw.exitPosition;
        p.towerSlotPositions = raw.towerSlots != null ? raw.towerSlots.ToArray() : new Vector3[0];

        int spawnCount = raw.spawns != null ? raw.spawns.Count : 0;
        p.spawnPointPositions     = new Vector3[spawnCount];
        p.spawnWaypointPositions  = new PathPatternData.SpawnWaypoints[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            SerializableSpawn s = raw.spawns[i];
            p.spawnPointPositions[i] = s.spawn;

            // Per-spawn full chain = spawn -> ...waypoints -> exit
            var chain = new List<Vector3> { s.spawn };
            if (s.waypoints != null) chain.AddRange(s.waypoints);
            chain.Add(raw.exitPosition);
            p.spawnWaypointPositions[i] = new PathPatternData.SpawnWaypoints { positions = chain.ToArray() };
        }

        // Legacy single-chain fallback uses the first spawn's path
        p.waypointPositions = spawnCount > 0
            ? p.spawnWaypointPositions[0].positions
            : new Vector3[] { raw.exitPosition };

        return p;
    }
}
