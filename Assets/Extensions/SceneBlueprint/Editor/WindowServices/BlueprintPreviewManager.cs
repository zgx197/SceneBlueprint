#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Editor.Preview;
using SceneBlueprint.Runtime.Markers;
using UnityEditor;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 节点预览调度器（从 SceneBlueprintWindow 提取）。
    /// 负责维护脏集合、MarkerId↔NodeId 索引、图形状快照，并通过
    /// EditorApplication.delayCall 调度合批刷新，实际渲染委托给
    /// <see cref="Preview.BlueprintPreviewManager"/>（预览计算引擎单例）。
    /// </summary>
    public class NodePreviewScheduler
    {
        // ── 依赖 ──
        private readonly IBlueprintEditorContext _ctx;
        private readonly Func<string>            _getPreviewContextId;

        // ── 脏标记状态 ──
        private readonly HashSet<string>                      _dirtyNodeIds    = new();
        private bool                                          _dirtyAll;
        private bool                                          _flushScheduled;

        // ── MarkerId ↔ NodeId 双向索引（Location.RandomArea） ──
        private readonly Dictionary<string, HashSet<string>> _markerToNodeIds = new();
        private readonly Dictionary<string, string>          _nodeToMarkerId  = new();

        // ── 图形状快照 ──
        private readonly HashSet<string>         _observedNodeIds            = new();
        private readonly Dictionary<string, int> _observedMarkerSignatures   = new();
        private int                              _observedSubGraphFrameCount = -1;

        // ── 构造 ──

        public NodePreviewScheduler(IBlueprintEditorContext ctx, Func<string> getPreviewContextId)
        {
            _ctx                 = ctx;
            _getPreviewContextId = getPreviewContextId;
        }

        // ── 脏标记 API ──

        public void MarkDirtyAll(string reason)
        {
            _dirtyAll = true;
            _dirtyNodeIds.Clear();
            SBLog.Debug(SBLogTags.Pipeline, "NodePreviewScheduler.MarkDirtyAll: {0}", reason);
            ScheduleFlush();
        }

        public void MarkDirtyForNode(string nodeId, string reason)
        {
            if (!string.IsNullOrEmpty(nodeId))
                MarkDirtyForNodes(new[] { nodeId }, reason);
        }

        public void MarkDirtyForNodes(IEnumerable<string> nodeIds, string reason)
        {
            if (_dirtyAll) return;
            int added = 0;
            foreach (var id in nodeIds)
                if (!string.IsNullOrEmpty(id) && _dirtyNodeIds.Add(id)) added++;
            if (added > 0)
            {
                SBLog.Debug(SBLogTags.Pipeline,
                    "NodePreviewScheduler.MarkDirtyForNodes: added={0}, reason={1}", added, reason);
                ScheduleFlush();
            }
        }

        public int MarkDirtyForNodesByAreaMarkerIds(IEnumerable<string> markerIds, string reason)
        {
            if (_ctx.ViewModel == null) return 0;
            var mids = new HashSet<string>(markerIds.Where(id => !string.IsNullOrEmpty(id)));
            if (mids.Count == 0) return 0;

            var nids = CollectNodeIdsByMarkerIds(mids);
            if (nids.Count == 0)
            {
                RebuildMarkerNodeIndex();
                nids = CollectNodeIdsByMarkerIds(mids);
            }
            MarkDirtyForNodes(nids, reason);
            return nids.Count;
        }

        public int MarkDirtyForAllRandomAreaNodes(string reason)
        {
            var vm = _ctx.ViewModel;
            if (vm == null) return 0;
            var ids = vm.Graph.Nodes
                .Where(n => (n.UserData as ActionNodeData)?.ActionTypeId == "Location.RandomArea")
                .Select(n => n.Id).ToList();
            MarkDirtyForNodes(ids, reason);
            return ids.Count;
        }

        public int MarkDirtyForUncachedRandomAreaNodes(string reason)
        {
            var vm = _ctx.ViewModel;
            if (vm == null) return 0;
            var cached = new HashSet<string>(
                Preview.BlueprintPreviewManager.Instance
                    .GetCurrentBlueprintPreviews().Select(p => p.NodeId));
            var ids = vm.Graph.Nodes
                .Where(n => (n.UserData as ActionNodeData)?.ActionTypeId == "Location.RandomArea"
                         && !cached.Contains(n.Id))
                .Select(n => n.Id).ToList();
            MarkDirtyForNodes(ids, reason);
            return ids.Count;
        }

        private HashSet<string> CollectNodeIdsByMarkerIds(HashSet<string> mids)
        {
            var result = new HashSet<string>();
            foreach (var mid in mids)
                if (_markerToNodeIds.TryGetValue(mid, out var set))
                    foreach (var nid in set) result.Add(nid);
            return result;
        }

        // ── 调度 & 刷新 ──

        public void ScheduleFlush()
        {
            if (_flushScheduled) return;
            _flushScheduled = true;
            EditorApplication.delayCall -= FlushDirtyPreviews;
            EditorApplication.delayCall += FlushDirtyPreviews;
        }

        private void FlushDirtyPreviews()
        {
            EditorApplication.delayCall -= FlushDirtyPreviews;
            _flushScheduled = false;

            var vm = _ctx.ViewModel;
            if (vm == null) { _dirtyAll = false; _dirtyNodeIds.Clear(); return; }

            bool doAll        = _dirtyAll;
            var  snapshot     = new List<string>(_dirtyNodeIds);
            _dirtyAll = false;
            _dirtyNodeIds.Clear();
            if (!doAll && snapshot.Count == 0) return;

            var graph     = vm.Graph;
            var contextId = _getPreviewContextId();
            MarkerCache.SetDirty();

            if (doAll)
            {
                Preview.BlueprintPreviewManager.Instance.RefreshAllPreviews(contextId, graph);
                SyncMarkerSignatureSnapshot();
                SBLog.Debug(SBLogTags.Pipeline,
                    "NodePreviewScheduler.Flush: 全量, nodeCount={0}", graph.Nodes.Count);
                return;
            }

            int refreshed = 0;
            var removed   = new List<string>();
            foreach (var nid in snapshot)
            {
                var node = graph.FindNode(nid);
                if (node?.UserData is ActionNodeData nd)
                {
                    Preview.BlueprintPreviewManager.Instance.RefreshPreviewForNode(contextId, nid, nd);
                    refreshed++;
                }
                else { removed.Add(nid); }
            }

            int removedCnt = removed.Count > 0
                ? Preview.BlueprintPreviewManager.Instance.RemovePreviews(removed, repaint: false)
                : 0;

            if (refreshed > 0 || removedCnt > 0)
            {
                UnityEditor.SceneView.RepaintAll();
                SyncMarkerSignatureSnapshot();
                SBLog.Debug(SBLogTags.Pipeline,
                    "NodePreviewScheduler.Flush: 局部, refreshed={0}, removed={1}", refreshed, removedCnt);
            }
        }

        // ── 标记签名快照 ──

        public void SyncMarkerSignatureSnapshot()
        {
            _observedMarkerSignatures.Clear();
            var mids = Preview.BlueprintPreviewManager.Instance.GetCurrentPreviewMarkerIds();
            if (mids.Count == 0) return;
            var lookup = BuildMarkerLookup();
            foreach (var mid in mids)
            {
                int sig = lookup.TryGetValue(mid, out var m) ? ComputeMarkerSignature(m) : int.MinValue;
                _observedMarkerSignatures[mid] = sig;
            }
        }

        public List<string> CollectChangedPreviewMarkerIds(IReadOnlyCollection<string> previewMarkerIds)
        {
            var lookup  = BuildMarkerLookup();
            var changed = new List<string>();
            foreach (var mid in previewMarkerIds)
            {
                int cur = lookup.TryGetValue(mid, out var m) ? ComputeMarkerSignature(m) : int.MinValue;
                if (!_observedMarkerSignatures.TryGetValue(mid, out int prev) || prev != cur)
                    changed.Add(mid);
            }
            return changed;
        }

        // ── 图形状快照 ──

        public void SyncGraphShapeSnapshot(Graph graph)
        {
            _observedNodeIds.Clear();
            foreach (var n in graph.Nodes) _observedNodeIds.Add(n.Id);
            _observedSubGraphFrameCount = graph.SubGraphFrames.Count;
        }

        /// <summary>
        /// 检测图结构变化（节点增删、子图数量变化）。
        /// 对新增节点更新索引并标脏；对删除节点清理索引并标脏；最后同步快照。
        /// 对应原 SceneBlueprintWindow.DetectPreviewGraphShapeChange。
        /// </summary>
        public void DetectGraphShapeChange()
        {
            var vm = _ctx.ViewModel;
            if (vm == null) return;

            var graph = vm.Graph;

            if (_observedSubGraphFrameCount < 0)
            {
                SyncGraphShapeSnapshot(graph);
                return;
            }

            bool subGraphChanged = graph.SubGraphFrames.Count != _observedSubGraphFrameCount;
            if (!subGraphChanged && graph.Nodes.Count == _observedNodeIds.Count) return;

            var currentIds = new HashSet<string>(graph.Nodes.Select(n => n.Id));
            var removed    = _observedNodeIds.Where(id => !currentIds.Contains(id)).ToList();
            var added      = currentIds.Where(id => !_observedNodeIds.Contains(id)).ToList();

            bool changed = subGraphChanged || removed.Count > 0 || added.Count > 0;
            if (!changed) return;

            SBLog.Debug(SBLogTags.Pipeline,
                "NodePreviewScheduler.DetectGraphShapeChange: added={0}, removed={1}, subGraph {2}->{3}",
                added.Count, removed.Count, _observedSubGraphFrameCount, graph.SubGraphFrames.Count);

            if (added.Count > 0)
            {
                foreach (var nid in added)
                {
                    var node = graph.FindNode(nid);
                    UpdateMarkerNodeIndex(nid, node?.UserData as ActionNodeData);
                }
                MarkDirtyForNodes(added, "GraphShapeChanged.NodeAdded");
            }

            if (removed.Count > 0)
            {
                foreach (var nid in removed)
                    RemoveMarkerNodeIndex(nid);
                MarkDirtyForNodes(removed, "GraphShapeChanged.NodeRemoved");
            }

            SyncGraphShapeSnapshot(graph);
        }

        // ── MarkerId ↔ NodeId 索引管理 ──

        public void UpdateMarkerNodeIndex(string nodeId, ActionNodeData? data)
        {
            RemoveMarkerNodeIndex(nodeId);
            if (string.IsNullOrEmpty(nodeId) || data == null) return;
            if (!TryGetRandomAreaMarkerId(data, out var mid)) return;

            if (!_markerToNodeIds.TryGetValue(mid, out var set))
            {
                set = new HashSet<string>();
                _markerToNodeIds[mid] = set;
            }
            set.Add(nodeId);
            _nodeToMarkerId[nodeId] = mid;
        }

        public void RemoveMarkerNodeIndex(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            if (!_nodeToMarkerId.TryGetValue(nodeId, out var mid)) return;
            _nodeToMarkerId.Remove(nodeId);
            if (_markerToNodeIds.TryGetValue(mid, out var set))
            {
                set.Remove(nodeId);
                if (set.Count == 0) _markerToNodeIds.Remove(mid);
            }
        }

        public void RebuildMarkerNodeIndex()
        {
            _markerToNodeIds.Clear();
            _nodeToMarkerId.Clear();
            var vm = _ctx.ViewModel;
            if (vm == null) return;
            foreach (var node in vm.Graph.Nodes)
                UpdateMarkerNodeIndex(node.Id, node.UserData as ActionNodeData);
        }

        // ── 重置 ──

        public void ResetState()
        {
            _dirtyAll    = false;
            _flushScheduled = false;
            _dirtyNodeIds.Clear();
            _markerToNodeIds.Clear();
            _nodeToMarkerId.Clear();
            _observedNodeIds.Clear();
            _observedMarkerSignatures.Clear();
            _observedSubGraphFrameCount = -1;
        }

        // ── 静态辅助 ──

        private static bool TryGetRandomAreaMarkerId(ActionNodeData data, out string markerId)
        {
            markerId = "";
            if (data.ActionTypeId != "Location.RandomArea") return false;
            markerId = data.Properties.Get<string>("area") ?? "";
            return !string.IsNullOrEmpty(markerId);
        }

        private static Dictionary<string, SceneMarker> BuildMarkerLookup()
        {
            var d = new Dictionary<string, SceneMarker>();
            foreach (var m in MarkerCache.GetAll())
                if (m != null && !string.IsNullOrEmpty(m.MarkerId)) d[m.MarkerId] = m;
            return d;
        }

        private static int ComputeMarkerSignature(SceneMarker marker)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (marker.MarkerTypeId?.GetHashCode() ?? 0);
                h = h * 31 + QuantizeVec3(marker.transform.position);
                h = h * 31 + QuantizeQuat(marker.transform.rotation);
                if (marker is AreaMarker am)
                {
                    h = h * 31 + (int)am.Shape;
                    h = h * 31 + QuantizeVec3(am.BoxSize);
                    h = h * 31 + UnityEngine.Mathf.RoundToInt(am.Height * 1000f);
                    if (am.Vertices != null) { h = h * 31 + am.Vertices.Count; foreach (var v in am.Vertices) h = h * 31 + QuantizeVec3(v); }
                }
                return h;
            }
        }

        private static int QuantizeVec3(UnityEngine.Vector3 v)
        {
            unchecked { return (UnityEngine.Mathf.RoundToInt(v.x * 1000) * 31 + UnityEngine.Mathf.RoundToInt(v.y * 1000)) * 31 + UnityEngine.Mathf.RoundToInt(v.z * 1000); }
        }

        private static int QuantizeQuat(UnityEngine.Quaternion q)
        {
            unchecked { return ((UnityEngine.Mathf.RoundToInt(q.x * 1000) * 31 + UnityEngine.Mathf.RoundToInt(q.y * 1000)) * 31 + UnityEngine.Mathf.RoundToInt(q.z * 1000)) * 31 + UnityEngine.Mathf.RoundToInt(q.w * 1000); }
        }
    }
}
