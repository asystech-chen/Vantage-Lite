using System;
using System.IO;
using System.Reflection;

namespace VantageLite.App
{
    /// <summary>
    /// 单文件支持：把 WebView2 的托管 DLL 从嵌入资源加载进来，并解压原生 Loader。
    /// 依赖：无（不能引用 WebView2 类型，否则会在安装解析器之前触发加载）。
    /// 改动注意：Initialize 必须是 Main 的第一条语句。
    /// </summary>
    public static class EmbeddedAssemblies
    {
        private const string CoreResource = "VantageLite.Embedded.Microsoft.Web.WebView2.Core.dll";
        private const string WinFormsResource = "VantageLite.Embedded.Microsoft.Web.WebView2.WinForms.dll";
        private const string LoaderResource = "VantageLite.Embedded.WebView2Loader.dll";

        public static string LoaderDirectory { get; private set; }

        public static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            LoaderDirectory = ExtractLoader();
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;

            if (name == "Microsoft.Web.WebView2.Core")
            {
                return LoadAssembly(CoreResource);
            }

            if (name == "Microsoft.Web.WebView2.WinForms")
            {
                return LoadAssembly(WinFormsResource);
            }

            return null;
        }

        private static Assembly LoadAssembly(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                byte[] buffer = new byte[stream.Length];
                int offset = 0;
                while (offset < buffer.Length)
                {
                    int read = stream.Read(buffer, offset, buffer.Length - offset);
                    if (read <= 0)
                    {
                        break;
                    }
                    offset += read;
                }

                return Assembly.Load(buffer);
            }
        }

        /// <summary>
        /// 每个进程用独立目录，避免多开时文件占用冲突。
        /// </summary>
        private static string ExtractLoader()
        {
            string dir = Path.Combine(
                Path.GetTempPath(),
                "VantageLite",
                "Loader",
                "x64-" + System.Diagnostics.Process.GetCurrentProcess().Id);

            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "WebView2Loader.dll");

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LoaderResource))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("WebView2Loader.dll 未嵌入到程序中。");
                }

                using (FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(file);
                }
            }

            return dir;
        }
    }
}
