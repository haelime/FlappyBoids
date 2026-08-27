using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FlappyBoids.Editor
{
    [CustomEditor(typeof(MarchingCubesGateVisual)), CanEditMultipleObjects]
    public sealed class MarchingCubesGateVisualEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "Mass builds broad rock shelves, Ridge breaks the silhouette, and Chips cut small facets. " +
                "The circular gameplay opening remains exact.",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool settingsChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Rebuild Selected Rock Gates", GUILayout.Height(28f)))
                    RebuildSelected();
                if (GUILayout.Button("New Seed + Rebuild"))
                    RandomizeSeedAndRebuild();
            }

            int triangles = 0;
            foreach (Object item in targets)
                if (item is MarchingCubesGateVisual visual) triangles += visual.TriangleCount;
            EditorGUILayout.LabelField("Preview triangles", triangles.ToString("N0"));
            if (settingsChanged)
                EditorGUILayout.HelpBox("Settings changed. Rebuild to refresh the Scene preview.", MessageType.None);
        }

        private void RebuildSelected()
        {
            foreach (Object item in targets)
            {
                if (!(item is MarchingCubesGateVisual visual)) continue;
                Undo.RecordObject(visual, "Rebuild rugged rock gate");
                visual.Rebuild();
                EditorUtility.SetDirty(visual);
                if (visual.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
            }
            SceneView.RepaintAll();
        }

        private void RandomizeSeedAndRebuild()
        {
            serializedObject.Update();
            SerializedProperty seed = serializedObject.FindProperty("_seed");
            seed.intValue = Random.Range(1, int.MaxValue);
            serializedObject.ApplyModifiedProperties();
            RebuildSelected();
        }
    }

    [CustomEditor(typeof(MarchingCubesSeaChunk)), CanEditMultipleObjects]
    public sealed class MarchingCubesSeaChunkEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "Rock displacement is always pushed outside the ECS corridor boundary, so rough visuals never " +
                "create a hidden collision inside the playable water volume.",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool settingsChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Rebuild Selected Sea Chunks", GUILayout.Height(28f)))
                    RebuildSelected();
                if (GUILayout.Button("Rebuild Every Rock Surface"))
                    MarchingCubesRockEditorTools.RebuildAllRockSurfaces();
            }

            int triangles = 0;
            foreach (Object item in targets)
                if (item is MarchingCubesSeaChunk chunk) triangles += chunk.TriangleCount;
            EditorGUILayout.LabelField("Preview triangles", triangles.ToString("N0"));
            if (settingsChanged)
                EditorGUILayout.HelpBox("Settings changed. Rebuild to refresh the Scene preview.", MessageType.None);
        }

        private void RebuildSelected()
        {
            foreach (Object item in targets)
            {
                if (!(item is MarchingCubesSeaChunk chunk)) continue;
                Undo.RecordObject(chunk, "Rebuild rugged sea chunk");
                chunk.Rebuild();
                EditorUtility.SetDirty(chunk);
                if (chunk.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(chunk.gameObject.scene);
            }
            SceneView.RepaintAll();
        }
    }

    public static class MarchingCubesRockEditorTools
    {
        [MenuItem("Tools/Flappy Boids/Rebuild Every Marching Cubes Rock Surface")]
        public static void RebuildAllRockSurfaces()
        {
            MarchingCubesGateVisual[] gates = Object.FindObjectsByType<MarchingCubesGateVisual>(
                FindObjectsInactive.Include);
            MarchingCubesSeaChunk[] chunks = Object.FindObjectsByType<MarchingCubesSeaChunk>(
                FindObjectsInactive.Include);
            Undo.RecordObjects(gates, "Rebuild all rugged rock gates");
            Undo.RecordObjects(chunks, "Rebuild all rugged sea chunks");
            foreach (MarchingCubesGateVisual gate in gates)
            {
                gate.Rebuild();
                EditorUtility.SetDirty(gate);
            }
            foreach (MarchingCubesSeaChunk chunk in chunks)
            {
                chunk.Rebuild();
                EditorUtility.SetDirty(chunk);
            }
            if (gates.Length > 0 && gates[0].gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(gates[0].gameObject.scene);
            else if (chunks.Length > 0 && chunks[0].gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(chunks[0].gameObject.scene);
            SceneView.RepaintAll();
            Debug.Log($"Rebuilt {gates.Length} rugged gates and {chunks.Length} rugged sea chunks.");
        }
    }
}
