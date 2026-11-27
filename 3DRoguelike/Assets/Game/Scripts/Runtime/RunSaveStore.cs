using System;
using System.IO;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 판 중간 저장 파일(3-6) 한 곳: <c>Application.persistentDataPath/run_save.json</c>, JSON(<see cref="RunSaveData"/>, 형식 번호 포함).
    /// 무엇을 담는지는 규칙 <see cref="RunSaveRules"/>, 여기는 읽기 · 쓰기 · 지우기 · 깨진 파일 백업만. PlayerPrefs는 쓰지 않는다.
    /// 쓰기는 임시 파일에 쓴 뒤 바꿔 끼운다(쓰다 꺼져도 앞 저장이 남게). 판 밖 데이터(3-7 영구 재화 · 해금)는 이 파일에 넣지 않는다.
    /// </summary>
    public sealed class RunSaveStore
    {
        public const string FileName = "run_save.json";
        /// <summary>에디터에서만: 이 환경 변수가 있으면 그 폴더를 쓴다(테스트 · 확인이 사용자 저장을 건드리지 않게).</summary>
        public const string DirectoryOverrideVariable = "GORANI_RUN_SAVE_DIR";

        public RunSaveStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Save directory is required.", nameof(directory));
            Directory = directory;
        }

        /// <summary>게임이 쓰는 저장. 에디터에서 <see cref="DirectoryOverrideVariable"/>이 있으면 그 폴더.</summary>
        public static RunSaveStore Default
        {
            get
            {
#if UNITY_EDITOR
                string overrideDirectory = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);
                if (!string.IsNullOrEmpty(overrideDirectory)) return new RunSaveStore(overrideDirectory);
#endif
                return new RunSaveStore(Application.persistentDataPath);
            }
        }

        public string Directory { get; }
        public string FilePath => Path.Combine(Directory, FileName);
        public bool Exists => File.Exists(FilePath);

        /// <summary>읽은 결과.</summary>
        public enum ReadResult
        {
            None,
            Ok,
            /// <summary>읽지 못함(글자가 깨짐 · 형식 번호가 다름). 백업 뒤 새 판.</summary>
            Broken,
        }

        public static string ToJson(RunSaveData data) => JsonUtility.ToJson(data, prettyPrint: true);

        public void Write(RunSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            System.IO.Directory.CreateDirectory(Directory);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, ToJson(data));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }

        /// <summary>파일을 읽어 모양만 확인한다(형식 번호). 판 값 검사는 <see cref="RunSaveRules.TryRestore"/>.</summary>
        public ReadResult TryRead(out RunSaveData data, out string error)
        {
            data = null;
            error = null;
            if (!Exists) return ReadResult.None;
            try
            {
                string text = File.ReadAllText(FilePath);
                data = string.IsNullOrWhiteSpace(text) ? null : JsonUtility.FromJson<RunSaveData>(text);
                if (data == null) { error = "비어 있음"; return ReadResult.Broken; }
                if (!RunSaveRules.IsReadable(data.version)) { error = $"형식 번호 {data.version} (읽는 번호 {RunSaveRules.MinReadableVersion} ~ {RunSaveRules.FormatVersion})"; data = null; return ReadResult.Broken; }
                return ReadResult.Ok;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
            {
                data = null;
                error = exception.Message;
                return ReadResult.Broken;
            }
        }

        /// <summary>판이 끝나면(사망 · 클리어) · 새 판을 고르면 지운다.</summary>
        public bool Delete()
        {
            if (!Exists) return false;
            File.Delete(FilePath);
            return true;
        }

        /// <summary>깨진 파일을 <c>run_save.broken-{시각}.json</c>으로 옮겨 둔다(지우지 않음). 옮긴 경로를 돌려준다.</summary>
        public string BackUpBroken(DateTime utcNow)
        {
            if (!Exists) return null;
            string target = Path.Combine(Directory, $"run_save.broken-{utcNow:yyyyMMdd-HHmmss}.json");
            for (int i = 1; File.Exists(target); i++) target = Path.Combine(Directory, $"run_save.broken-{utcNow:yyyyMMdd-HHmmss}-{i}.json");
            File.Move(FilePath, target);
            return target;
        }
    }
}
