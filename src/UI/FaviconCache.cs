using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;

namespace VantageLite.UI
{
    /// <summary>
    /// 站点 favicon 内存缓存：按 host 拉取一次 /favicon.ico，失败也缓存，避免反复请求。
    /// 依赖：无。改动注意：回调在线程池线程执行，调用方需要自行切回 UI 线程。
    /// </summary>
    internal static class FaviconCache
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Image> Cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<Action<Image>>> Pending =
            new Dictionary<string, List<Action<Image>>>(StringComparer.OrdinalIgnoreCase);

        public static Image GetOrLoad(string url, Action<Image> onLoaded)
        {
            string key = KeyFor(url);
            if (key == null)
            {
                return null;
            }

            lock (Gate)
            {
                Image cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                List<Action<Image>> callbacks;
                if (Pending.TryGetValue(key, out callbacks))
                {
                    if (onLoaded != null)
                    {
                        callbacks.Add(onLoaded);
                    }

                    return null;
                }

                callbacks = new List<Action<Image>>();
                if (onLoaded != null)
                {
                    callbacks.Add(onLoaded);
                }

                Pending[key] = callbacks;
            }

            ThreadPool.QueueUserWorkItem(delegate { Load(key); });
            return null;
        }

        private static void Load(string key)
        {
            Image image = null;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://" + key + "/favicon.ico");
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                request.UserAgent = "VantageLite";
                request.AllowAutoRedirect = true;

                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    buffer.Position = 0;

                    using (Image decoded = Image.FromStream(buffer))
                    {
                        image = new Bitmap(decoded);
                    }
                }
            }
            catch
            {
            }

            List<Action<Image>> callbacks;

            lock (Gate)
            {
                Cache[key] = image;
                Pending.TryGetValue(key, out callbacks);
                Pending.Remove(key);
            }

            if (callbacks != null)
            {
                for (int i = 0; i < callbacks.Count; i++)
                {
                    try
                    {
                        callbacks[i](image);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string KeyFor(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return null;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            return uri.Authority;
        }
    }
}
