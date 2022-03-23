using PhotoshopFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace QTool.Psd2Ui
{
    public interface IKey
    {
        string Key { get; set; }
    }
    [System.Serializable]
    public class FontRef :IKey
    {
        public string Key { get => key; set => key = value; }
        public string key;
        [UnityEngine.Serialization.FormerlySerializedAs("obj")]
        public Font font;
        [Range(-0.5f,0.5f)]
        public float YOffset=0;
    }
    [System.Serializable]
    public class Prefab : IKey
    {
        public string Key { get => key; set => key = value; }
        public string key;
        [UnityEngine.Serialization.FormerlySerializedAs("obj")]
        public GameObject prefab;
    }
    public class UiImportSetting : BaseUiImportSetting
    {
        public BaseUiImportSetting parentSetting;
        [HideInInspector]
        [SerializeField]
        private UnityEngine.Object psdFile;
      
        public float UiSizeScale
        {
            get
            {
                return uiSizeScale * (parentSetting == null ? 1 : parentSetting.uiSizeScale);
            }
        }
        public float TextScale
        {
            get
            {
                return textScale * (parentSetting == null ? 1 : parentSetting.textScale);
            }
        }
        public float AutoanchoredRate
        {
            get
            {
                return autoanchoredRate * (parentSetting == null ? 1 : parentSetting.autoanchoredRate);
            }
        }

        public Action SavePrefabAction;
        public Action LoadSpriteAction;
        [HideInInspector]
        public List<RectTransform> toPrefabUi=new List<RectTransform>();

        public string AssetPath
        {
            get
            {
                return AssetDatabase.GetAssetPath(psdFile);
            }
        }
        public string RootPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(AssetPath), name);
            }
        }
        public string ResourcesPath
        {
            get
            {
                return Path.Combine(RootPath, "BaseResources");
            }
        }
        
       
        public void Init(UnityEngine.Object psdFile)
        {
            this.psdFile = psdFile;
            prefabList.Clear();
            var psd = new PsdFile(AssetPath, new LoadContext { Encoding = System.Text.Encoding.Default });
            List<string> baseList = new List<string>();
            foreach (var layer in psd.Layers)
            {
                if (layer.Name.Contains("=prefab"))
                {
                    baseList.Add(layer.TrueName());
                }
            }
            foreach (var layer in psd.Layers)
            {
                //var name = layer.TrueName();
                if (layer.Name.Contains("=&prefab") && !baseList.Contains(layer.TrueName())) 
                {
                    prefabList.CheckGet(layer.TrueName(),parentSetting?.prefabList);
                }
                else if (layer.Name.Contains("=text["))
                {
                    var startIndex = layer.Name.IndexOf("=text[") + "=text[".Length;
                    var endIndex = layer.Name.IndexOf("]");
                    var infos = layer.Name.Substring(startIndex, endIndex - startIndex).Split('|');
                    var font = infos[1];
                    fontList.CheckGet( font,parentSetting?.fontList );
                }
            }
        }
        [ContextMenu("生成UI预制体")]
        public void CreateUIPrefab()
        {
            if (!Directory.Exists(RootPath))
            {
                Directory.CreateDirectory(RootPath);
            }
            if (!Directory.Exists(ResourcesPath))
            {
                Directory.CreateDirectory(ResourcesPath);
            }
            var root = this.CreateUIPrefabRoot();
            for (int i = 0; i < root.childCount; i++)
            {
                var ui = root.GetChild(i) as RectTransform;
                if (ui.childCount > 0)
                {
                    this.SaveAsPrefab(ui);
                }

            }
            GameObject.DestroyImmediate(root.gameObject);

        }

    }
    #region 拓展 

    public static class UiPsdImporterExtends
    {
        public static T CheckGet<T>(this List<T> objList, string key, List<T> parentList = null) where T : IKey,new() 
        {
            foreach (var kv in objList)
            {
                if (kv.Key.Equals(key))
                {
                    return kv;
                }
            }
            if (parentList != null)
            {
                foreach (var kv in parentList)
                {
                    if (kv.Key.Equals(key))
                    {
                        return kv;
                    }
                }
            }
            var obj = new T { Key = key };
            objList.Add(obj);
            return obj;
        }
        public static string SaveName(this Layer layer)
        {
            var name = layer.TrueName();
            if (name.Contains('+'))
            {
                name.Replace('+', '_');
            }
            if (name.Contains('<') || name.Contains('>'))
            {
                name = name.Replace('<', '_').Replace('>', '_');
            }
            return name;
        }
        public static LayerSectionInfo GetGroupInfo(this Layer layer)
        {
            return layer.GetInfo("lsct", "lsdk") as LayerSectionInfo;
        }
        public static RawLayerInfo GetTextInfo(this Layer layer)
        {
            return layer.GetInfo("TySh") as RawLayerInfo;
        }
        public static bool IsRectZero(this Layer layer)
        {
            return layer.Rect.Width == 0 || layer.Rect.Height == 0;
        }
        public static LayerInfo GetInfo(this Layer layer,params string[] keys)
        {
            foreach (var info in layer.AdditionalInfo)
            {
                foreach (var key in keys)
                {
                    if (info.Key.Equals(key))
                    {
                        return info;
                    }
                }
            }
            return null;
        }


        public static RectTransform CreateUIPrefabRoot(this UiImportSetting psdUi)
        {
            psdUi.LoadSpriteAction = null;
            psdUi.SavePrefabAction = null;
            psdUi.toPrefabUi.Clear();
            Stack<RectTransform> groupStack = new Stack<RectTransform>(); ;
            var psd = new PsdFile(psdUi.AssetPath, new LoadContext { Encoding = System.Text.Encoding.Default });
            var name = Path.GetFileNameWithoutExtension(psdUi.AssetPath);
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(psd.ColumnCount, psd.RowCount)*psdUi.UiSizeScale;
            foreach (var layer in psd.Layers)
            {
                var parentUi = groupStack.Count > 0 ? groupStack.Peek() : root;
                var textInfo = layer.GetTextInfo();
                if (textInfo != null)
                {

                    psdUi.CreateText(layer, parentUi);
                }
                else
                {
                    var groupInfo = layer.GetGroupInfo();
                    if (groupInfo != null)
                    {
                        switch (groupInfo.SectionType)
                        {
                            case LayerSectionType.OpenFolder:
                            case LayerSectionType.ClosedFolder:
                                {
                                    //层级开启标志
                                    var groupUI = groupStack.Pop();
                                    Bounds bounds = new Bounds();
                                    var childList = new List<RectTransform>();

                                    for (int i = groupUI.childCount - 1; i >= 0; i--)
                                    {
                                        var child = groupUI.GetChild(0) as RectTransform;
                                        if (bounds.center == Vector3.zero && bounds.size == Vector3.zero)
                                        {
                                            bounds = new Bounds(child.position, Vector3.zero);
                                        }
                                        bounds.Encapsulate(child.transform.position + new Vector3(child.rect.xMin, child.rect.yMin));
                                        bounds.Encapsulate(child.transform.position + new Vector3(child.rect.xMax, child.rect.yMax));
                                        childList.Add(child);
                                        child.SetParent(root);
                                    }

                                    groupUI.sizeDelta = bounds.size;
                                    groupUI.position = bounds.center;
                                    foreach (var item in childList)
                                    {
                                        item.SetParent(groupUI);
                                    }
                                    groupUI.name = layer.TrueName();
                                    groupUI.gameObject.SetActive(layer.Visible);
                                    if (layer.Name.Contains("=prefab"))
                                    {
                                        psdUi.SavePrefabAction += () =>
                                        {
                                            if (groupUI == null) return;
                                            var index = groupUI.GetSiblingIndex();
                                            psdUi.SaveAsPrefab(groupUI);

                                        };
                                    }
                                    else if (layer.Name.Contains("=&prefab"))
                                    {
                                        psdUi.ChangeToPrefab(groupUI);
                                    }
                                }
                                break;
                            case LayerSectionType.SectionDivider:
                                {
                                    //层级结束标志
                                    groupStack.Push(psdUi.CreateGroup(layer, parentUi));
                                }
                                break;
                            default:
                                Debug.LogError("未解析的层级逻辑" + groupInfo.SectionType);
                                break;
                        }
                    }
                    else
                    {
                        psdUi.CreateImage(layer, parentUi);
                    }
                }
            }

            AssetDatabase.Refresh();
            psdUi.LoadSpriteAction?.Invoke();
            psdUi.SavePrefabAction?.Invoke();
            return root;
        }
        public static Vector2 Center(this Layer layer)
        {
            return new Vector2
            {
                x = layer.Rect.Left + layer.Rect.Width / 2,
                y = layer.PsdFile.RowCount - (layer.Rect.Top + layer.Rect.Height / 2),
            };
        }
        public static string TrueName(this Layer layer)
        {
            return layer.HaveFlag() ? (layer.Name.Substring(0, layer.Name.IndexOf("="))) : layer.Name;
        }
        public static bool HaveFlag(this Layer layer)
        {
            return layer.Name.Contains("=");
        }

        // static List<GameObject> destoryList = new List<GameObject>();
        public static bool ChangeToPrefab(this UiImportSetting psdUi, RectTransform tempUi)
        {
            if (tempUi == null) return true;


            var prefab = psdUi.prefabList.CheckGet(tempUi.name, psdUi.parentSetting?.prefabList).prefab;

            if (prefab == null)
            {
                if (!psdUi.toPrefabUi.Contains(tempUi))
                {
                    psdUi.toPrefabUi.Add(tempUi);
                }

                // Debug.LogError("缺少预制体【" + tempUi.name + "】");
                return false;
            }

            if (UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(tempUi.gameObject))
            {
                var prefabAsset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(tempUi.gameObject);
                if (prefab == prefabAsset)
                {
                    return true;
                }
            }
            var instancePrefab = PrefabUtility.InstantiatePrefab(prefab, tempUi.parent) as GameObject;
            var ui = instancePrefab.GetComponent<RectTransform>();
            ui.SetSiblingIndex(tempUi.GetSiblingIndex());
            ui.ChangeTo(tempUi);
            GameObject.DestroyImmediate(tempUi.gameObject);
            return true;
        }
        public static void SaveAsPrefab(this UiImportSetting psdUi, RectTransform ui)
        {
            var uiPrefab = psdUi.prefabList.CheckGet(ui.name, psdUi.parentSetting?.prefabList).prefab;
            if (uiPrefab == null)
            {
                uiPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(ui.gameObject, Path.Combine(psdUi.RootPath, ui.name + ".prefab"), InteractionMode.AutomatedAction);
                psdUi.prefabList.CheckGet(ui.name).prefab = uiPrefab;
                psdUi.toPrefabUi.RemoveAll((item) => psdUi.ChangeToPrefab(item));
                return;
            }
        }
        public static void ChangeTo(this Transform oldUi, Transform newUI)
        {

            Component[] coms = new Component[3];
            coms[0] = oldUi.GetComponent<RectTransform>();
            coms[1] = oldUi.GetComponent<Image>();
            coms[2] = oldUi.GetComponent<Text>();
            foreach (var com in coms)
            {

                if (com == null) continue;
                var newCom = newUI.GetComponent(com.GetType());
                if (newCom != null)
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(newCom);
                    UnityEditorInternal.ComponentUtility.PasteComponentValues(com);

                }
            }
            for (int i = 0; i < oldUi.childCount && i < newUI.childCount; i++)
            {
                if (!UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(oldUi.GetChild(i).gameObject))
                {
                    if (oldUi.GetChild(i).name.Equals(newUI.GetChild(i).name))
                    {
                        oldUi.GetChild(i).ChangeTo(newUI.GetChild(i));
                    }
                }
            }

        }
        public static void Autoanchored(this UiImportSetting psdUi, RectTransform ui)
        {
            for (int i = 0; i < ui.childCount; i++)
            {

                var child = ui.GetChild(i) as RectTransform;

                var rightUpOffset = ui.UpRight() - child.UpRight();
                var leftDonwOffset = child.DownLeft() - ui.DownLeft();

                var widthCheck = psdUi.AutoanchoredRate * ui.Width();
                var heightCheck = psdUi.AutoanchoredRate * ui.Height();


                if (rightUpOffset.x < widthCheck && leftDonwOffset.x < widthCheck)
                {
                    child.offsetMax = new Vector2(-rightUpOffset.x, child.offsetMax.y);
                    child.anchorMax = new Vector2(1, child.anchorMax.y);
                    child.offsetMin = new Vector2(leftDonwOffset.x, child.offsetMin.y);
                    child.anchorMin = new Vector2(0, child.anchorMin.y);
                }
                else if(rightUpOffset.x<widthCheck*2&&leftDonwOffset.x>1-widthCheck*2+child.Width()/2)
                {
                    child.offsetMax = new Vector2(-rightUpOffset.x, child.offsetMax.y);
                    child.anchorMax = new Vector2(1, child.anchorMax.y);
                    child.offsetMin = new Vector2(-(rightUpOffset.x+child.Width()), child.offsetMin.y);
                    child.anchorMin = new Vector2(1, child.anchorMin.y);
                }
                else if (rightUpOffset.x > 1 - widthCheck * 2 + child.Width() / 2 && leftDonwOffset.x < widthCheck * 2)
                {
                    child.offsetMax = new Vector2(leftDonwOffset.x+child.Width(), child.offsetMax.y);
                    child.anchorMax = new Vector2(0, child.anchorMax.y);
                    child.offsetMin = new Vector2(leftDonwOffset.x, child.offsetMin.y);
                    child.anchorMin = new Vector2(0, child.anchorMin.y);
                }
                if (rightUpOffset.y < heightCheck && leftDonwOffset.y < heightCheck)
                {
                    child.offsetMax = new Vector2(child.offsetMax.x, -rightUpOffset.y);
                    child.anchorMax = new Vector2(child.anchorMax.x, 1);
                    child.offsetMin = new Vector2(child.offsetMin.x, leftDonwOffset.y);
                    child.anchorMin = new Vector2(child.anchorMin.x, 0);
                }
                else if(rightUpOffset.y < heightCheck*2 && leftDonwOffset.y >1- heightCheck*2+child.Height()/2)
                {
                    child.offsetMax = new Vector2(child.offsetMax.x, -rightUpOffset.y);
                    child.anchorMax = new Vector2(child.anchorMax.x, 1);
                    child.offsetMin = new Vector2(child.offsetMin.x, -(rightUpOffset.y+child.Height()));
                    child.anchorMin = new Vector2(child.anchorMin.x, 1);
                }
                else if (rightUpOffset.y > 1 - heightCheck * 2 + child.Height() / 2 && leftDonwOffset.y<heightCheck*2 )
                {
                    child.offsetMax = new Vector2(child.offsetMax.x, leftDonwOffset.y + child.Height());
                    child.anchorMax = new Vector2(child.anchorMax.x, 0);
                    child.offsetMin = new Vector2(child.offsetMin.x, leftDonwOffset.y);
                    child.anchorMin = new Vector2(child.anchorMin.x, 0);
                }
                if (!UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    psdUi.Autoanchored(child);
                }
            }
        }
        public static RectTransform CreateUIBase(this UiImportSetting psdUi, Layer layer, RectTransform parent = null)
        {
            var ui = new GameObject(layer.TrueName(), typeof(RectTransform)).GetComponent<RectTransform>();
            ui.sizeDelta = new Vector2(layer.Rect.Width, layer.Rect.Height)*psdUi.UiSizeScale;
            ui.position = layer.Center()*psdUi.UiSizeScale - parent.rect.size / 2;
            ui.SetParent(parent);
            ui.gameObject.SetActive(layer.Visible);
            return ui;
        }

        public static RectTransform CreateGroup(this UiImportSetting psdUi, Layer layer, RectTransform parent = null)
        {
            var ui = psdUi.CreateUIBase(layer, parent);
            ui.sizeDelta = parent.rect.size;
            return ui;
        }
        public static void CreateImage(this UiImportSetting psdUi, Layer layer, RectTransform parent = null)
        {
            if (!layer.HaveFlag()) return ;
            if (layer.IsRectZero())
            {
                Debug.LogError($"layout of {layer.Name} is empty.");
                return;
            }

            var ui = psdUi.CreateUIBase(layer, parent);
            var tex = CreateTexture(layer);
            if (tex != null)
            {
                var image = ui.gameObject.AddComponent<Image>();
                image.color = new Color(1, 1, 1, layer.Opacity / 255f);
                psdUi.GetSprite(layer, (sprite) =>
                {
                    if (image == null) return;
                    image.sprite = sprite;
                });
            }
        }
        public static Text CreateText(this UiImportSetting psdUi, Layer layer, RectTransform root = null)
        {
            if (!layer.HaveFlag()) return null;

            var ui = psdUi.CreateUIBase(layer, root);
            var text = "";
            var font = "";
            var size = 40f;
            var color = Color.black;
            if (layer.Name.Contains("=text["))
            {
                var startIndex = layer.Name.IndexOf("=text[") + "=text[".Length;
                var endIndex = layer.Name.IndexOf("]");
                var infos = layer.Name.Substring(startIndex, endIndex - startIndex).Split('|');
                text = infos[0].Replace("#$%", "\n");
                font = infos[1];
                size = float.Parse(infos[2]);
                ColorUtility.TryParseHtmlString("#" + infos[3], out color);
            }
            else
            {
                Debug.LogWarning("文字层 缺少字体大小等相关信息 请先在Ps中运行脚本(生成UGUI格式文件.jsx)");
            }

            var textUi = ui.gameObject.AddComponent<Text>();
            textUi.text = text;
            var parentFontList = psdUi.parentSetting?.fontList;
            var fongSetting= psdUi.fontList.CheckGet(font, parentFontList);
            textUi.fontSize = (int)(size * psdUi.TextScale);
            var tempFont = fongSetting.font;
            if (parentFontList != null)
            {
                tempFont = parentFontList.Find((f)=> {
                    return f.Key.Equals(font);
                })?.font;
            } 

            if (tempFont == null)
            {
                Debug.LogError("未指定字体【" + font + "】");
            }
            else
            {
                textUi.font = tempFont;
                textUi.transform.position += Vector3.up * fongSetting.YOffset * textUi.fontSize;
            }

            if (textUi.rectTransform.Height() < textUi.fontSize * 1.8f)
            {
                textUi.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            textUi.verticalOverflow = VerticalWrapMode.Overflow;
            textUi.color = color;
            return textUi;
        }


        public static void GetSprite(this UiImportSetting psdUi, Layer layer, Action<Sprite> callBack)
        {
            var tex = CreateTexture(layer);

            if (tex != null)
            {
                string path = Path.Combine(psdUi.ResourcesPath, psdUi.name + "_" + tex.name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                psdUi.LoadSpriteAction += () =>
                {
                    var sprite = psdUi.LoadSprite(path);
                    if (sprite != null)
                    {
                        callBack?.Invoke(sprite);
                    }
                };
            }

        }
        static Sprite LoadSprite(this UiImportSetting psdUi, string path)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
            textureImporter.spritePixelsPerUnit = 100;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
        }

        public static Texture2D CreateTexture(Layer layer)
        {
            if (layer.IsRectZero())
                return null;

            Texture2D tex = new Texture2D((int)layer.Rect.Width, (int)layer.Rect.Height, TextureFormat.RGBA32, true);
            Color32[] pixels = new Color32[tex.width * tex.height];

            Channel red = (from l in layer.Channels where l.ID == 0 select l).First();
            Channel green = (from l in layer.Channels where l.ID == 1 select l).First();
            Channel blue = (from l in layer.Channels where l.ID == 2 select l).First();
            Channel alpha = layer.AlphaChannel;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte r = red.ImageData[i];
                byte g = green.ImageData[i];
                byte b = blue.ImageData[i];
                byte a = 255;

                if (layer.Name.Contains("white"))
                {
                    if (r > 0)
                    {
                        r = 255;
                    }
                    if (g > 0)
                    {
                        g = 255;
                    }
                    if (b > 0)
                    {
                        b = 255;
                    }
                }
                if (alpha != null)
                    a = alpha.ImageData[i];

                int mod = i % tex.width;
                int n = ((tex.width - mod - 1) + i) - mod;
                pixels[pixels.Length - n - 1] = new Color32(r, g, b, a);
            }

            tex.SetPixels32(pixels);
            tex.name = layer.SaveName();
            if (!layer.Name.Contains("=png"))
            {
                tex.name += layer.LayerID;
            }
            tex.Apply();
            return tex;
        }
    }
    #endregion


    public static class RectTransformExtend
    {
        public static Vector2 UpRightRectOffset(this RectTransform rectTransform)
        {
            return new Vector2(rectTransform.Width() * (1 - rectTransform.pivot.x), rectTransform.Height() * (1 - rectTransform.pivot.y));
        }
        public static Vector2 DownLeftRectOffset(this RectTransform rectTransform)
        {
            return new Vector2(rectTransform.Width() * (rectTransform.pivot.x), rectTransform.Height() * (rectTransform.pivot.y));
        }

        public static float Height(this RectTransform rectTransform)
        {
            return rectTransform.rect.size.y;
        }
        public static float Width(this RectTransform rectTransform)
        {
            return rectTransform.rect.size.x;
        }
        public static Vector2 Size(this RectTransform rectTransform)
        {
            return rectTransform.rect.size;
        }

        public static RectTransform RectTransform(this Transform transform)
        {
            return transform as RectTransform;
        }
        public static Vector2 UpRight(this RectTransform rectTransform)
        {
            return new Vector2(rectTransform.position.x, rectTransform.position.y) + rectTransform.UpRightRectOffset();
        }
        public static Vector2 DownLeft(this RectTransform rectTransform)
        {
            return new Vector2(rectTransform.position.x, rectTransform.position.y) - rectTransform.DownLeftRectOffset();
        }

    }
}