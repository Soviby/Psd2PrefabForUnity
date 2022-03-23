using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace QTool.Psd2Ui
{

    //[CreateAssetMenu(menuName = "工具/基础UI生成设置",fileName ="基础UI生成设置",order =0)]
    public class BaseUiImportSetting : ScriptableObject
    {
        public List<Prefab> prefabList = new List<Prefab>();
        public List<FontRef> fontList = new List<FontRef>();
        [Range(0.1f, 1f)]
        public float uiSizeScale = 1;
        [Range(0.1f, 5f)]
        public float textScale = 1;
        public float autoanchoredRate = 0.1f;
    }
}