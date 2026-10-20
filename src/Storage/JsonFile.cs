using System;
using System.IO;
using System.Runtime.Serialization.Json;
using VantageLite.App;
using VantageLite.Diagnostics;

namespace VantageLite.Storage
{
    /// <summary>
    /// 极简 JSON 读写：原子写入（临时文件 + 替换）。
    /// 依赖：AppPaths / Log。改动注意：读取失败一律回退到默认值，不能让数据文件损坏阻断启动。
    /// </summary>
    internal static class JsonFile
    {
        public static T Load<T>(string path, T fallback) where T : class
        {
            try
            {
                if (!File.Exists(path))
                {
                    return fallback;
                }

                using (FileStream stream = File.OpenRead(path))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    return serializer.ReadObject(stream) as T ?? fallback;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("读取 " + path + " 失败：" + ex.Message);
                return fallback;
            }
        }

        public static void Save<T>(string path, T value) where T : class
        {
            try
            {
                AppPaths.EnsureDirectoryForFile(path);

                string temp = path + ".tmp";
                using (FileStream stream = File.Create(temp))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                }

                if (File.Exists(path))
                {
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
                else
                {
                    File.Move(temp, path);
                }

                Log.Debug("已保存 " + path);
            }
            catch (Exception ex)
            {
                Log.Warn("写入 " + path + " 失败：" + ex.Message);
            }
        }
    }
}
