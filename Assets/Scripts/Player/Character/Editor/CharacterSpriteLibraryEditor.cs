#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterSpriteLibrary))]
public class CharacterSpriteLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CharacterSpriteLibrary library = target as CharacterSpriteLibrary;
        if (library == null)
            return;

        EditorGUILayout.LabelField("Character Sprite Library", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Entries", library.Entries.Count.ToString());
        EditorGUILayout.Space();

        if (GUILayout.Button("Rebuild Library"))
            CharacterSpriteLibraryBuilder.BuildLibrary();

        if (GUILayout.Button("Soft Reload (scripts)"))
            CharacterLibraryAutoBuild.SoftReload();

        if (GUILayout.Button("Force Reimport Player Textures"))
            CharacterLibraryAutoBuild.ForceReimportAllPlayerTextures();
    }
}

[CustomPropertyDrawer(typeof(CharacterSpriteLibrary.SheetEntry))]
public class CharacterSpriteLibraryEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 0f;
    }
}
#endif
