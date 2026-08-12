using System.Collections.Generic;
using CoopCore;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 远端玩家渲染:每人一个独立化身,不是"幽灵分身"。
    /// 运行时从本地主角复制纯视觉层级(只带 Transform + SpriteRenderer,
    /// 不带任何游戏逻辑脚本),挂同一个 AnimatorController,
    /// 用对方发来的动画状态哈希独立驱动 —— 对方跑就播跑,砍树就播砍树。
    ///
    /// 由 PlayerLoop 驱动(游戏会销毁 Mod 的 GameObject,不能依赖 MonoBehaviour)。
    /// </summary>
    internal static class RemotePlayerRenderer
    {
        private static readonly Dictionary<ulong, Avatar> Avatars = new Dictionary<ulong, Avatar>();

        public static void Upsert(RemotePeer peer)
        {
            if (!Avatars.TryGetValue(peer.Id, out var av))
            {
                av = new Avatar(peer.Id);
                Avatars[peer.Id] = av;
            }
            av.Target = new Vector3(peer.X, peer.Y, 0f);
            av.FacingLeft = peer.FacingLeft;
            av.AnimHash = peer.AnimHash;
            av.AnimTime = peer.AnimTime;
            av.DisplayName = string.IsNullOrEmpty(peer.Name) ? peer.Id.ToString() : peer.Name;
        }

        public static void Remove(ulong id)
        {
            if (Avatars.TryGetValue(id, out var av))
            {
                av.Destroy();
                Avatars.Remove(id);
            }
        }

        public static void Tick()
        {
            foreach (var av in Avatars.Values) av.Tick();
        }

        private sealed class Avatar
        {
            private readonly ulong _id;
            private GameObject _root;
            private Transform _visual;
            private Animator _animator;
            private TextMesh _label;
            private int _playingHash;
            private bool _snapped;

            public Vector3 Target;
            public bool FacingLeft;
            public int AnimHash;
            public float AnimTime;
            public string DisplayName = "";

            public Avatar(ulong id) { _id = id; }

            public void Tick()
            {
                if (_root == null && !TryBuild()) return;

                if (!_snapped) { _root.transform.position = Target; _snapped = true; }
                else _root.transform.position = Vector3.Lerp(_root.transform.position, Target, 12f * Time.deltaTime);

                _visual.localScale = FacingLeft ? new Vector3(-1f, 1f, 1f) : Vector3.one;
                if (_label != null) _label.text = DisplayName;

                if (AnimHash != 0 && AnimHash != _playingHash && _animator != null)
                {
                    _playingHash = AnimHash;
                    try { _animator.Play(AnimHash, 0, AnimTime); } catch { }
                }
            }

            private bool TryBuild()
            {
                DolocTown.BodyController agent;
                try { agent = DolocAPI.agent; } catch { return false; }
                if (agent == null || !agent.gameObject.activeInHierarchy) return false;

                var srcAnimator = agent.GetComponentInChildren<Animator>();
                if (srcAnimator == null) return false;

                _root = new GameObject($"CoopAvatar_{_id}");
                Object.DontDestroyOnLoad(_root);

                var visualGo = new GameObject("visual");
                _visual = visualGo.transform;
                _visual.SetParent(_root.transform, false);
                CopyVisualTree(srcAnimator.transform, _visual);

                _animator = visualGo.AddComponent<Animator>();
                _animator.runtimeAnimatorController = srcAnimator.runtimeAnimatorController;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var labelGo = new GameObject("name");
                labelGo.transform.SetParent(_root.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 4.2f, 0f);
                _label = labelGo.AddComponent<TextMesh>();
                _label.characterSize = 0.5f;
                _label.fontSize = 32;
                _label.anchor = TextAnchor.LowerCenter;
                _label.color = new Color(1f, 1f, 1f, 0.9f);

                NetLog.Log($"AVATAR_BUILT id={_id} 已从主角复制视觉层级");
                Plugin.Log.LogInfo($"[Avatar] 已为 {_id} 构建化身");
                return true;
            }

            /// <summary>复制纯视觉层级。动画剪辑按路径名绑定子节点,所以名字必须一致。</summary>
            private static void CopyVisualTree(Transform src, Transform dst)
            {
                var srcSr = src.GetComponent<SpriteRenderer>();
                if (srcSr != null)
                {
                    var sr = dst.gameObject.AddComponent<SpriteRenderer>();
                    sr.sprite = srcSr.sprite;
                    sr.sortingLayerName = srcSr.sortingLayerName;
                    sr.sortingOrder = srcSr.sortingOrder;
                    sr.sharedMaterial = srcSr.sharedMaterial;
                    sr.enabled = srcSr.enabled;
                }
                foreach (Transform child in src)
                {
                    var dstChild = new GameObject(child.name).transform;
                    dstChild.SetParent(dst, false);
                    dstChild.localPosition = child.localPosition;
                    dstChild.localRotation = child.localRotation;
                    dstChild.localScale = child.localScale;
                    CopyVisualTree(child, dstChild);
                }
            }

            public void Destroy()
            {
                if (_root != null) Object.Destroy(_root);
                _root = null;
            }
        }
    }
}
