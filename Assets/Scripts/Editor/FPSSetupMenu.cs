using UnityEngine;
using UnityEditor;

public class FPSSetupMenu : MonoBehaviour
{
    [MenuItem("FPS Game/Setup Scene (One Click)", false, 0)]
    static void SetupScene()
    {
        // Add required tags
        AddTag("Head");

        // Create bootstrap object
        GameObject bootstrap = new GameObject("__FPS_Bootstrap__");
        bootstrap.AddComponent<FPSBootstrap>();

        Selection.activeGameObject = bootstrap;

        Debug.Log("[FPS Setup] Bootstrap created! Press Play to generate the full scene.");
        Debug.Log("[FPS Setup] The scene will auto-build: map, player, weapons, bot, HUD.");

        EditorUtility.DisplayDialog("FPS Setup",
            "Bootstrap created!\n\n" +
            "Press PLAY to generate the full scene.\n\n" +
            "Controls:\n" +
            "WASD - Move\n" +
            "Shift - Run\n" +
            "Ctrl - Crouch\n" +
            "Space - Jump\n" +
            "LMB - Shoot\n" +
            "RMB - ADS\n" +
            "R - Reload\n" +
            "1/2/3 - Switch Weapons\n" +
            "Scroll - Next/Prev Weapon", "OK");
    }

    [MenuItem("FPS Game/Add Head Tag", false, 10)]
    static void AddHeadTag()
    {
        AddTag("Head");
        Debug.Log("[FPS Setup] 'Head' tag added.");
    }

    static void AddTag(string tag)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        SerializedProperty tags = tagManager.FindProperty("tags");

        // Check if tag already exists
        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                return;
        }

        // Add tag
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
