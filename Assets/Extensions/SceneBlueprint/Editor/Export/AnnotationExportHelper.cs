#nullable enable
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using SceneBlueprint.Core.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// Annotation 导出辅助工具——从 Marker 上收集 MarkerAnnotation 数据并转换为导出格式。
    /// <para>
    /// 被 BlueprintExporter 的后处理阶段调用，用于：
    /// 1. 从 AreaMarker 收集子 PointMarker
    /// 2. 从 PointMarker 收集 MarkerAnnotation 数据
    /// 3. 将 Annotation 数据写入 SceneBindingEntry.Annotations
    /// </para>
    /// </summary>
    public static class AnnotationExportHelper
    {
        /// <summary>
        /// 从 AreaMarker 下收集所有子 PointMarker。
        /// </summary>
        public static List<PointMarker> CollectChildPointMarkers(AreaMarker area)
        {
            var result = new List<PointMarker>();
            var parent = area.transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                var pm = parent.GetChild(i).GetComponent<PointMarker>();
                if (pm != null)
                    result.Add(pm);
            }
            return result;
        }

        /// <summary>
        /// 从 PointMarker 上收集所有 MarkerAnnotation 的导出数据。
        /// <para>
        /// 无 Annotation 时返回空数组并打印日志。
        /// </para>
        /// </summary>
        /// <param name="pointMarker">目标 PointMarker</param>
        /// <param name="actionTypeId">调用方的 Action TypeId（用于日志上下文）</param>
        public static AnnotationDataEntry[] CollectAnnotations(
            PointMarker pointMarker, string actionTypeId)
        {
            var annotations = MarkerCache.GetAnnotations(pointMarker);
            if (annotations.Length == 0)
            {
                SBLog.Info(SBLogTags.Export,
                    $"PointMarker '{pointMarker.GetDisplayLabel()}' (ID: {pointMarker.MarkerId}) " +
                    $"无 MarkerAnnotation，将使用节点 '{actionTypeId}' 的全局默认配置");
                return System.Array.Empty<AnnotationDataEntry>();
            }

            var entries = new List<AnnotationDataEntry>();
            foreach (var annotation in annotations)
            {
                var data = new Dictionary<string, object>();
                annotation.CollectExportData(data);

                var properties = new List<PropertyValue>();
                foreach (var kvp in data)
                {
                    properties.Add(new PropertyValue
                    {
                        Key = kvp.Key,
                        ValueType = InferValueType(kvp.Value),
                        Value = SerializeValue(kvp.Value)
                    });
                }

                entries.Add(new AnnotationDataEntry
                {
                    TypeId = annotation.AnnotationTypeId,
                    Properties = properties.ToArray()
                });
            }

            return entries.ToArray();
        }

        /// <summary>
        /// 通过 MarkerId 在场景中查找 SceneMarker。
        /// </summary>
        public static SceneMarker? FindMarkerById(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return null;

            var allMarkers = MarkerCache.GetAll();
            foreach (var marker in allMarkers)
            {
                if (marker != null && marker.MarkerId == markerId)
                    return marker;
            }
            return null;
        }

        private static string InferValueType(object value)
        {
            return value switch
            {
                int => "Int",
                float => "Float",
                bool => "Bool",
                string => "String",
                _ => "String"
            };
        }

        private static string SerializeValue(object value)
        {
            return value switch
            {
                float f => f.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => value?.ToString() ?? ""
            };
        }
    }
}
