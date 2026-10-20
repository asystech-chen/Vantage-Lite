using System;
using System.Runtime.Serialization;

namespace VantageLite.Storage
{
    /// <summary>
    /// 上次退出时的窗口位置与状态。依赖：无。
    /// 改动注意：坐标为屏幕物理像素（进程按 PerMonitorV2 运行）。
    /// </summary>
    [DataContract]
    public sealed class WindowStateData
    {
        [DataMember(Name = "x", Order = 0)]
        public int X { get; set; }

        [DataMember(Name = "y", Order = 1)]
        public int Y { get; set; }

        [DataMember(Name = "width", Order = 2)]
        public int Width { get; set; }

        [DataMember(Name = "height", Order = 3)]
        public int Height { get; set; }

        [DataMember(Name = "maximized", Order = 4)]
        public bool Maximized { get; set; }
    }
}
