using System.Linq;

namespace DolocCoop
{
    internal static class SyncRegistryExt
    {
        /// <summary>按名字取某个同步域跟踪的条目数,面板显示用。</summary>
        public static int TrackedOf(this CoopCore.Replication.SyncRegistry reg, string name)
        {
            var d = reg?.Domains?.FirstOrDefault(x => x.Name == name);
            return d?.TrackedCount ?? 0;
        }
    }
}
