namespace CoopCore
{
    /// <summary>
    /// 版本号的唯一出处。
    ///
    /// 为什么放在 CoopCore:握手会校验 mod 版本,而模拟客机(测试工具)也走同一套
    /// 握手流程。如果版本号各写各的,测试工具会被自己的校验挡在门外 ——
    /// 实测时就撞上了这个:模拟客机报 "sim-0.1",被主机以"Mod 版本不同"拒绝。
    ///
    /// 这里是 const,所以插件的 [BepInPlugin] 特性也能直接引用(特性参数要求编译期常量)。
    /// </summary>
    public static class BuildInfo
    {
        public const string ModVersion = "0.5.0";
    }
}


