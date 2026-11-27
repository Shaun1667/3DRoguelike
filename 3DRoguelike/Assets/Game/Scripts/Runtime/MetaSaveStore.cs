using System;
using System.IO;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 판 밖 진행 파일(3-7) 한 곳: <c>Application.persistentDataPath/meta_progress.json</c>, JSON(<see cref="MetaSaveData"/>, 형식 번호 포함).
    /// 판 저장(<see cref="RunSaveStore"/>, run_save.json)과 따로 — 판이 끝나도 지우지 않는다. PlayerPrefs는 쓰지 않는다.
    /// 쓰기는 임시 파일에 쓴 뒤 바꿔 끼운다. 읽지 못한 파일은 meta_progress.broken-{UTC}.json으로 옮겨 두고 새로 시작한다.
    /// </summary>
    public sealed class MetaSaveStore
    {
        public const string FileName = "meta_progress.json";
        /// <summary>에디터에서만: 이 환경 변수가 있으면 그 폴더(없으면 판 저장의 <see cref="RunSaveStore.DirectoryOverrideVariable"/>)를 쓴다.</summary>
        public const string DirectoryOverrideVariable = "GORANI_META_SAVE_DIR";

        public MetaSaveStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Save directory is required.", nameof(directory));
            Directory = directory;
        }

        public static MetaSaveStore Default
        {
            get
            {
#if UNITY_EDITOR
                string overrideDirectory = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);
                if (string.IsNullOrEmpty(overrideDirectory)) overrideDirectory = Environment.GetEnvironmentVariable(RunSaveStore.DirectoryOverrideVariable);
                if (!string.IsNullOrEmpty(overrideDirectory)) return new MetaSaveStore(overrideDirectory);
#endif
                return new MetaSaveStore(Application.persistentDataPath);
            }
        }

        public string Directory { get; }
        public string FilePath => Path.Combine(Directory, FileName);
        public bool Exists => File.Exists(FilePath);

        public static string ToJson(MetaSaveData data) => JsonUtility.ToJson(data, prettyPrint: true);

        public void Write(MetaSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            System.IO.Directory.CreateDirectory(Directory);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, ToJson(data));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }

        /// <returns>파일이 없으면 None, 글자를 읽었으면 Ok(값 검사는 <see cref="MetaSaveRules.TryRestore"/>), 못 읽으면 Broken.</returns>
        public RunSaveStore.ReadResult TryRead(out MetaSaveData data, out string error)
        {
            data = null;
            error = null;
            if (!Exists) return RunSaveStore.ReadResult.None;
            try
            {
                string text = File.ReadAllText(FilePath);
                data = string.IsNullOrWhiteSpace(text) ? null : JsonUtility.FromJson<MetaSaveData>(text);
                if (data == null) { error = "비어 있음"; return RunSaveStore.ReadResult.Broken; }
                return RunSaveStore.ReadResult.Ok;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
            {
                data = null;
                error = exception.Message;
                return RunSaveStore.ReadResult.Broken;
            }
        }

        /// <summary>읽지 못한 파일을 옮겨 둔다(지우지 않음). 옮긴 경로.</summary>
        public string BackUpBroken(DateTime utcNow)
        {
            if (!Exists) return null;
            string target = Path.Combine(Directory, $"meta_progress.broken-{utcNow:yyyyMMdd-HHmmss}.json");
            for (int i = 1; File.Exists(target); i++) target = Path.Combine(Directory, $"meta_progress.broken-{utcNow:yyyyMMdd-HHmmss}-{i}.json");
            File.Move(FilePath, target);
            return target;
        }
    }
}
