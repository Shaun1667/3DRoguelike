using System.Collections.Generic;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>빌드 씬 목록에 씬을 넣는다. 다른 Setup 메뉴가 넣은 씬은 지우지 않는다.</summary>
    public static class BuildSceneList
    {
        /// <param name="first">true면 목록 맨 앞(게임이 시작하는 씬)</param>
        public static void Ensure(string scenePath, bool first)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path != scenePath && AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) != null) scenes.Add(scene);
            }

            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (first) scenes.Insert(0, entry);
            else scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
