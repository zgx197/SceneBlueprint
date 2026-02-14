#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SceneBlueprint.Adapters.Unity2D;
using SceneBlueprint.Adapters.Unity3D;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.SpatialModes
{
    /// <summary>
    /// 空间模式描述器。
    /// 描述 SceneView 放置策略与导出绑定编码策略。
    /// </summary>
    public interface ISpatialModeDescriptor
    {
        string ModeId { get; }
        string DisplayName { get; }
        string AdapterType { get; }

        bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos);

        void EncodeBinding(
            GameObject? sceneObject,
            BindingType bindingType,
            out string stableObjectId,
            out string adapterType,
            out string spatialPayloadJson);
    }

    /// <summary>
    /// 项目级空间模式配置。
    /// 不通过窗口切换，项目固定使用一个 ModeId。
    /// </summary>
    public static class SpatialModeProjectSettings
    {
        /// <summary>
        /// 项目固定空间模式 ID。
        /// 可选：Unity3D / Unity2D / 其他自定义描述器 ModeId。
        /// </summary>
        public const string ProjectModeId = "Unity3D";
    }

    /// <summary>
    /// 空间模式注册表。
    /// 通过反射自动发现并注册所有 ISpatialModeDescriptor 实现。
    /// </summary>
    public static class SpatialModeRegistry
    {
        private static readonly Dictionary<string, ISpatialModeDescriptor> _descriptors =
            new Dictionary<string, ISpatialModeDescriptor>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;

        public static IReadOnlyList<ISpatialModeDescriptor> GetAll()
        {
            EnsureInitialized();
            return _descriptors.Values.ToList();
        }

        public static bool TryGet(string modeId, out ISpatialModeDescriptor descriptor)
        {
            EnsureInitialized();
            return _descriptors.TryGetValue(modeId, out descriptor!);
        }

        public static ISpatialModeDescriptor GetProjectModeDescriptor()
        {
            EnsureInitialized();

            if (TryGet(SpatialModeProjectSettings.ProjectModeId, out var descriptor))
                return descriptor;

            if (_descriptors.Count > 0)
            {
                var fallback = _descriptors.Values.First();
                SBLog.Warn(SBLogTags.Registry,
                    $"未找到空间模式 '{SpatialModeProjectSettings.ProjectModeId}'，回退到 '{fallback.ModeId}'。");
                return fallback;
            }

            throw new InvalidOperationException("未发现任何 ISpatialModeDescriptor 实现。请检查模式描述器注册。");
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            var descriptorType = typeof(ISpatialModeDescriptor);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name ?? "";
                if (assemblyName.StartsWith("System", StringComparison.Ordinal)
                    || assemblyName.StartsWith("Microsoft", StringComparison.Ordinal)
                    || assemblyName.StartsWith("mscorlib", StringComparison.Ordinal)
                    || assemblyName.StartsWith("netstandard", StringComparison.Ordinal))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface)
                        continue;
                    if (!descriptorType.IsAssignableFrom(type))
                        continue;

                    try
                    {
                        var descriptor = (ISpatialModeDescriptor)Activator.CreateInstance(type)!;
                        if (string.IsNullOrWhiteSpace(descriptor.ModeId))
                            continue;

                        if (_descriptors.ContainsKey(descriptor.ModeId))
                        {
                            SBLog.Warn(SBLogTags.Registry,
                                $"空间模式 '{descriptor.ModeId}' 重复定义，已跳过: {type.FullName}");
                            continue;
                        }

                        _descriptors[descriptor.ModeId] = descriptor;
                    }
                    catch (Exception ex)
                    {
                        SBLog.Warn(SBLogTags.Registry,
                            $"空间模式描述器加载失败: {type.FullName}，{ex.Message}");
                    }
                }
            }

            SBLog.Info(SBLogTags.Registry, $"SpatialModeRegistry: 加载 {_descriptors.Count} 个空间模式");
        }
    }

    /// <summary>内置 Unity3D 空间模式描述器。</summary>
    public sealed class Unity3DSpatialModeDescriptor : ISpatialModeDescriptor
    {
        public string ModeId => "Unity3D";
        public string DisplayName => "Unity 3D";
        public string AdapterType => Unity3DAdapterServices.AdapterType;

        public bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            return Unity3DAdapterServices.TryGetSceneViewPlacement(mousePos, sceneView, out worldPos);
        }

        public void EncodeBinding(
            GameObject? sceneObject,
            BindingType bindingType,
            out string stableObjectId,
            out string adapterType,
            out string spatialPayloadJson)
        {
            Unity3DAdapterServices.EncodeBinding(
                sceneObject,
                bindingType,
                out stableObjectId,
                out adapterType,
                out spatialPayloadJson);
        }
    }

    /// <summary>内置 Unity2D 空间模式描述器。</summary>
    public sealed class Unity2DSpatialModeDescriptor : ISpatialModeDescriptor
    {
        public string ModeId => "Unity2D";
        public string DisplayName => "Unity 2D";
        public string AdapterType => Unity2DAdapterServices.AdapterType;

        public bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            return Unity2DAdapterServices.TryGetSceneViewPlacement(mousePos, sceneView, out worldPos);
        }

        public void EncodeBinding(
            GameObject? sceneObject,
            BindingType bindingType,
            out string stableObjectId,
            out string adapterType,
            out string spatialPayloadJson)
        {
            Unity2DAdapterServices.EncodeBinding(
                sceneObject,
                bindingType,
                out stableObjectId,
                out adapterType,
                out spatialPayloadJson);
        }
    }
}
