using System.Collections.Generic;
using System.IO;
using System.Text;
using CoopCore;
using CoopCore.Replication;
using DolocTown;
using UnityEngine;

namespace DolocCoop.Domains
{
    /// <summary>
    /// 作物长势同步。
    ///
    /// 迁到 ReplicatedDomain 之后,这个类只剩"这个游戏的作物怎么读、怎么编码、怎么写回",
    /// 计时/指纹/差分/批量/重发那套骨架全在基类里 —— 对比迁移前的 CropSync,
    /// 少了约一半代码,而且以后骨架改进(比如加限流、加冲突处理)所有域一起受益。
    /// </summary>
    internal sealed class CropDomain : ReplicatedDomain<CropState>
    {
        public override byte Channel => 1;
        public override string Name => "作物";
        protected override float ScanInterval => 3f;

        protected override IEnumerable<CropState> ReadAll()
        {
            foreach (var b in FindBasins()) yield return Read(b);
        }

        protected override string IdOf(CropState s) => s.BasinId;

        protected override string SignatureOf(CropState s)
        {
            var sb = new StringBuilder();
            sb.Append(s.SeedId).Append('|').Append(s.CurrentLevel).Append('|')
              .Append(s.IsMature ? 1 : 0).Append(s.IsDead ? 1 : 0)
              .Append(s.IsMoist ? 1 : 0).Append(s.IsPolluted ? 1 : 0).Append('|')
              .Append(s.HarvestTimes).Append('|').Append(s.Lifespan).Append('|')
              // 取整到 0.5:作物每帧都在长,不取整会导致每帧都判定"变了"
              .Append(Mathf.RoundToInt(s.GrowthValue * 2f)).Append('|')
              .Append(Mathf.RoundToInt(s.HealthValue * 2f));
            return sb.ToString();
        }

        protected override void Encode(BinaryWriter w, CropState c)
        {
            w.Write(c.BasinId ?? ""); w.Write(c.SeedId ?? "");
            w.Write(c.CurrentLevel); w.Write(c.Lifespan); w.Write(c.HarvestTimes);
            w.Write(c.GrowthValue); w.Write(c.HealthValue);
            w.Write(c.IsMature); w.Write(c.IsDead); w.Write(c.IsMoist); w.Write(c.IsPolluted);
        }

        protected override CropState Decode(BinaryReader r) => new CropState
        {
            BasinId = r.ReadString(), SeedId = r.ReadString(),
            CurrentLevel = r.ReadInt32(), Lifespan = r.ReadInt32(), HarvestTimes = r.ReadInt32(),
            GrowthValue = r.ReadSingle(), HealthValue = r.ReadSingle(),
            IsMature = r.ReadBoolean(), IsDead = r.ReadBoolean(),
            IsMoist = r.ReadBoolean(), IsPolluted = r.ReadBoolean(),
        };

        protected override bool ApplyOne(CropState st)
        {
            var basin = FindBasin(st.BasinId);
            var crop = basin?.Crop;
            if (crop == null || !crop.IsValid) return false;   // 本地这个槽还空着,等交互同步补上

            // 种的不是同一种东西就别硬套数值 —— 阶段数、成熟条件都不一样,
            // 硬写会得到一株阶段对不上的畸形作物
            if (!string.IsNullOrEmpty(st.SeedId) && crop.SeedId != st.SeedId)
            {
                NetLog.Sample($"crop-seed-{st.BasinId}", 10,
                    $"CROP_SEED_MISMATCH id={st.BasinId} 本地={crop.SeedId} 对方={st.SeedId}");
                return false;
            }

            if (SignatureOf(Read(basin)) == SignatureOf(st)) return false;

            crop.SetCropData(new CropData(
                st.IsMature, st.IsDead, st.CurrentLevel, st.Lifespan,
                st.GrowthValue, st.HealthValue, st.IsMoist, st.IsPolluted, st.HarvestTimes));
            return true;
        }

        protected override void AfterApply(int received, int applied)
        {
            if (applied > 0) NetLog.Log($"CROP_APPLY 收到={received} 应用={applied}");
        }

        // ---- 游戏侧读写 ----

        /// <summary>
        /// PlantBasin 继承 Equipment,是纯数据对象、不挂 GameObject,
        /// 所以只能从房间的设备表里枚举 —— FindObjectsOfType 一个也扫不到。
        /// </summary>
        private static IEnumerable<PlantBasin> FindBasins()
        {
            var room = DolocAPI.archiveHandle?.currentRoom;
            if (room?.DM_equipment == null) yield break;
            foreach (var eq in room.DM_equipment.AllEquipments)
                if (eq is PlantBasin b) yield return b;
        }

        private static PlantBasin FindBasin(string id)
        {
            foreach (var b in FindBasins())
                if (IdOfBasin(b) == id) return b;
            return null;
        }

        private static string IdOfBasin(PlantBasin b) => $"{b.GetType().Name}#{b.id}";

        private static CropState Read(PlantBasin b)
        {
            var st = new CropState { BasinId = IdOfBasin(b) };
            var crop = b.Crop;
            if (crop == null || !crop.IsValid) { st.SeedId = ""; return st; }

            // 读写都用 CropData 结构本身:Crop 上那些只读属性只是它的转发,
            // 而写入必须整块给(SetCropData),两边同构才不会漏字段
            var d = crop.data;
            st.SeedId = crop.SeedId ?? "";
            st.CurrentLevel = d.currentLevel;
            st.IsMature = d.isMature;
            st.IsDead = d.isDead;
            st.IsMoist = d.isMoist;
            st.IsPolluted = d.isPolluted;
            st.HarvestTimes = d.harvestTimes;
            st.Lifespan = d.lifespan;
            st.GrowthValue = d.currentGrowthValue;
            st.HealthValue = d.currentHealthValue;
            return st;
        }
    }
}
