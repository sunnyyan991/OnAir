using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OnAir
{
    public sealed class SaveFileStore
    {
        public const int MaximumFileBytes = 16 * 1024 * 1024;
        public string DirectoryPath { get; }
        readonly object gate = new object();
        public SaveFileStore(string directory) { DirectoryPath = Path.GetFullPath(directory); }
        public string PathFor(string name)
        {
            if (name != Path.GetFileName(name)) throw new ArgumentException("Save filename must be local.");
            return Path.Combine(DirectoryPath, name);
        }
        public string Read(string name)
        {
            lock (gate)
            {
                string path = PathFor(name);
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length > MaximumFileBytes) throw new FormatException("Save exceeds size limit.");
                return File.ReadAllText(path, Encoding.UTF8);
            }
        }
        public static string Hash(string text)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public static string Pack(object data)
        {
            string payload=JsonUtility.ToJson(data);
            return JsonUtility.ToJson(new SaveEnvelope{payload=payload,checksum=Hash(payload)});
        }
        public static string Unpack(string text)
        {
            try
            {
                if (text == null) throw new FormatException("Save is missing.");
                var envelope = JsonUtility.FromJson<SaveEnvelope>(text);
                if (envelope == null || string.IsNullOrEmpty(envelope.payload) || envelope.checksum != Hash(envelope.payload))
                    throw new FormatException("Save checksum is invalid.");
                return envelope.payload;
            }
            catch (ArgumentException error) { throw new FormatException("Save JSON is invalid.", error); }
        }
        // This method consumes an immutable JSON string and can run on a worker.
        public void Write(string name, string text, string backupName = null, bool preserveRejected = false)
        {
            lock (gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = PathFor(name), temporary = path + ".tmp";
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                if (bytes.Length > MaximumFileBytes) throw new IOException("Save exceeds size limit.");
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    if (preserveRejected && File.Exists(path))
                        File.Move(path, path + ".rejected-" + Guid.NewGuid().ToString("N"));
                    if (File.Exists(path)) File.Replace(temporary, path, backupName == null ? null : PathFor(backupName));
                    else File.Move(temporary, path);
                    if(backupName!=null&&!File.Exists(PathFor(backupName)))Write(backupName,text);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        public void ArchiveCurrent(string trustedSnapshot, bool preserveRejected = false)
        {
            lock (gate)
            {
                // Keep the previous journey distinct from the rolling save backup.
                if (trustedSnapshot != null) Write("previousJourney.json", trustedSnapshot);
                foreach (string name in new[] { "journey.json", "journey.bak" })
                {
                    string path = PathFor(name);
                    if (File.Exists(path))
                    {
                        if(trustedSnapshot!=null && !(preserveRejected && name=="journey.json"))File.Delete(path);
                        else File.Move(path, path + ".previous-" + Guid.NewGuid().ToString("N"));
                    }
                }
            }
        }
    }
}
