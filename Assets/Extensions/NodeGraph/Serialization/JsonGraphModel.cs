#nullable enable
using System;
using System.Collections.Generic;

namespace NodeGraph.Serialization
{
    /// <summary>
    /// JSON 序列化中间模型。与 Graph 内存模型一一对应，但全部使用基本类型。
    /// 用于跨引擎导入导出和剪贴板。
    /// </summary>

    [Serializable]
    public class JsonGraphModel
    {
        public string id = "";
        public JsonSettingsModel settings = new JsonSettingsModel();
        public List<JsonNodeModel> nodes = new List<JsonNodeModel>();
        public List<JsonEdgeModel> edges = new List<JsonEdgeModel>();
        public List<JsonGroupModel> groups = new List<JsonGroupModel>();
        public List<JsonCommentModel> comments = new List<JsonCommentModel>();
        public List<JsonSubGraphFrameModel> subGraphFrames = new List<JsonSubGraphFrameModel>();
    }

    [Serializable]
    public class JsonSettingsModel
    {
        public string topology = "DAG";
    }

    [Serializable]
    public class JsonNodeModel
    {
        public string id = "";
        public string typeId = "";
        public JsonVec2 position = new JsonVec2();
        public JsonVec2 size = new JsonVec2 { x = 200, y = 100 };
        public string displayMode = "Expanded";
        public bool allowDynamicPorts;
        public List<JsonPortModel> ports = new List<JsonPortModel>();
        public string? userData;   // 业务数据 JSON（由 IUserDataSerializer 处理）
    }

    [Serializable]
    public class JsonPortModel
    {
        public string id = "";
        public string name = "";
        public string direction = "Input";   // "Input" / "Output"
        public string kind = "Data";         // "Data" / "Control"
        public string dataType = "";
        public string capacity = "Multiple"; // "Single" / "Multiple"
        public int sortOrder;
    }

    [Serializable]
    public class JsonEdgeModel
    {
        public string id = "";
        public string sourcePortId = "";
        public string targetPortId = "";
        public string? userData;
    }

    [Serializable]
    public class JsonGroupModel
    {
        public string id = "";
        public string title = "";
        public JsonVec2 position = new JsonVec2();
        public JsonVec2 size = new JsonVec2();
        public JsonColor color = new JsonColor { r = 0.3f, g = 0.5f, b = 0.8f, a = 0.3f };
        public List<string> nodeIds = new List<string>();
    }

    [Serializable]
    public class JsonCommentModel
    {
        public string id = "";
        public string text = "";
        public JsonVec2 position = new JsonVec2();
        public JsonVec2 size = new JsonVec2 { x = 200, y = 60 };
        public float fontSize = 14f;
        public JsonColor textColor = new JsonColor { r = 1, g = 1, b = 1, a = 1 };
        public JsonColor backgroundColor = new JsonColor { r = 0.2f, g = 0.2f, b = 0.2f, a = 0.7f };
    }

    [Serializable]
    public class JsonSubGraphFrameModel
    {
        public string id = "";
        public string title = "";
        public JsonVec2 position = new JsonVec2();
        public JsonVec2 size = new JsonVec2();
        public List<string> nodeIds = new List<string>();
        public string representativeNodeId = "";
        public bool isCollapsed;
        public string? sourceAssetId;
    }

    [Serializable]
    public class JsonVec2
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class JsonColor
    {
        public float r;
        public float g;
        public float b;
        public float a = 1f;
    }
}
