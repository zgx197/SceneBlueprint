#nullable enable
using NodeGraph.Core;
using NodeGraph.Serialization;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// ActionNodeData 的 JSON 序列化器。
    /// 实现 NodeGraph 的 IUserDataSerializer 接口，用于图的持久化（保存/加载）。
    /// 内部使用 PropertyBagSerializer 进行 JSON 转换。
    /// </summary>
    public class ActionNodeDataSerializer : IUserDataSerializer
    {
        public string SerializeNodeData(INodeData data)
        {
            if (data is ActionNodeData actionData)
            {
                // 格式: { "typeId": "xxx", "properties": { ... } }
                var sb = new System.Text.StringBuilder();
                sb.Append("{\"typeId\":\"");
                sb.Append(EscapeJson(actionData.ActionTypeId));
                sb.Append("\",\"properties\":");
                sb.Append(PropertyBagSerializer.ToJson(actionData.Properties));
                sb.Append("}");
                return sb.ToString();
            }
            return "{}";
        }

        public INodeData? DeserializeNodeData(string typeId, string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}")
                return null;

            try
            {
                // 简易解析：提取 typeId 和 properties
                string? actionTypeId = ExtractStringField(json, "typeId");
                string? propertiesJson = ExtractObjectField(json, "properties");

                if (actionTypeId == null)
                    return null;

                var data = new ActionNodeData(actionTypeId);
                if (propertiesJson != null)
                {
                    data.Properties = PropertyBagSerializer.FromJson(propertiesJson);
                }
                return data;
            }
            catch
            {
                return null;
            }
        }

        public string SerializeEdgeData(IEdgeData data)
        {
            // SceneBlueprint 当前不使用 EdgeData
            return "{}";
        }

        public IEdgeData? DeserializeEdgeData(string json)
        {
            // SceneBlueprint 当前不使用 EdgeData
            return null;
        }

        // ── JSON 辅助方法 ──

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>从 JSON 中提取字符串字段值</summary>
        private static string? ExtractStringField(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\":\"";
            int start = json.IndexOf(pattern);
            if (start < 0) return null;

            start += pattern.Length;
            int end = start;
            while (end < json.Length)
            {
                if (json[end] == '"' && json[end - 1] != '\\')
                    break;
                end++;
            }
            return json.Substring(start, end - start);
        }

        /// <summary>从 JSON 中提取对象字段值（匹配花括号）</summary>
        private static string? ExtractObjectField(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return null;

            start += pattern.Length;
            // 跳过空白
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            if (start >= json.Length || json[start] != '{')
                return null;

            int depth = 0;
            int end = start;
            while (end < json.Length)
            {
                if (json[end] == '{') depth++;
                else if (json[end] == '}') depth--;
                if (depth == 0)
                {
                    end++;
                    break;
                }
                end++;
            }
            return json.Substring(start, end - start);
        }
    }
}
