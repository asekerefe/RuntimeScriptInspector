using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuntimeScriptInspector
{
    public class RuntimeScriptInspector : EditorWindow
    {
        private static readonly BindingFlags fieldDiscoveryFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags methodDiscoveryFlags = fieldDiscoveryFlags | BindingFlags.DeclaredOnly;
        private static readonly Color red = new Color(186 / 256f, 0, 0);
        private static readonly Color green = new Color(47 / 256f, 143 / 256f, 53 / 256f);
        private static readonly Rect windowRect = new Rect(100f, 100f, 360f, 600f);

        private GameObject selectedObject = null;
        private List<MonoBehaviour> components = null;
        private Dictionary<MonoBehaviour, ComponentInfo> componentToInfoMap = null;

        private Vector2 scrollPosition = Vector2.zero;
        private RuntimeScriptInspector window = null;
        private int length = 0;

        [MenuItem("Utility/RSI")]
        private static void Init()
        {
            RuntimeScriptInspector window = EditorWindow.GetWindow<RuntimeScriptInspector>();
            window.position = windowRect;
            window.title = "RSInspector";
        }

        private void OnEnable()
        {
            window = EditorWindow.GetWindow<RuntimeScriptInspector>();
            components = new List<MonoBehaviour>();
            componentToInfoMap = new Dictionary<MonoBehaviour, ComponentInfo>();
        }

        private void Update()
        {
            Repaint();
        }

        private void OnSelectionChange()
        {
            selectedObject = null;

            if (Selection.gameObjects.Length > 0)
            {
                GameObject lastObject = Selection.gameObjects[Selection.gameObjects.Length - 1];
                MonoBehaviour[] monoComponents = lastObject.GetComponents<MonoBehaviour>();

                components.Clear();
                componentToInfoMap.Clear();

                for (int i = 0; i < monoComponents.Length; i++)
                {
                    MonoBehaviour component = monoComponents[i];
                    ComponentInfo componentInfo = new ComponentInfo(component);
                    components.Add(component);

                    discoverMembers(component.GetType(), componentInfo.fields, componentInfo.methods);

                    componentInfo.guiLength = componentInfo.fields.Count * 20 + componentInfo.methods.Count * 20 + (30 + 20 + 20 + 20 + 20 + 20);
                    componentToInfoMap[component] = componentInfo;
                }

                selectedObject = lastObject;
            }

            updateGUILength();
        }

        private void discoverMembers(Type type, List<MemberInfo> fields, List<MemberInfo> methods)
        {
            if (type != typeof(MonoBehaviour))
            {
                MethodInfo[] methodInfoArr = type.GetMethods(methodDiscoveryFlags);

                foreach (MethodInfo info in methodInfoArr)
                    if (info.GetParameters().Length == 0)
                        methods.Add(info);

                FieldInfo[] fieldInfoArr = type.GetFields(fieldDiscoveryFlags);

                foreach (FieldInfo info in fieldInfoArr)
                        fields.Add(info);

                discoverMembers(type.BaseType, fields, methods);
            }
        }

        private void updateGUILength()
        {
            length = 20;

            for (int i = 0; i < components.Count;i++)
            {
                ComponentInfo info = componentToInfoMap[components[i]];
                if (info.visible)
                    length += info.guiLength;
            }

        }

        private void OnGUI()
        {
            if (selectedObject != null)
            {
                scrollPosition = GUI.BeginScrollView(
                    new Rect(0, 0, window.position.width, window.position.height),
                    scrollPosition,
                    new Rect(0, 0, 0, length));

                drawTitle("Target: "+selectedObject.name, 16, true, true, true);

                for (int i = 0; i < components.Count; i++)
                {
                    MonoBehaviour component = components[i];
                    ComponentInfo componentInfo = componentToInfoMap[component];
                    List<MemberInfo> fields = componentInfo.fields;
                    List<MemberInfo> methods = componentInfo.methods;
                    
                    GUILayout.BeginHorizontal();
                    {
                        drawTitle("Component: " + component.GetType().Name, 12, false, true, true, 290f);

                        if (GUILayout.Button(componentInfo.visible ? "H" : "S", GUILayout.Width(20f)))
                        {
                            componentInfo.visible = !componentInfo.visible;
                            componentToInfoMap[component] = componentInfo;
                            //updateGUILength();
                        }

                        GUILayout.EndHorizontal();
                    }

                    if (componentInfo.visible)
                    {
                        drawTitle("Variables ("+fields.Count+")", 10, false, true, true);
                        for (int j = 0; j < fields.Count; j++)
                            drawModifier(component, fields[j] as FieldInfo);

                        GUILayout.Space(20f);
                        
                        drawTitle("Methods (" + methods.Count+")", 10, false, true, true);
                        for (int j = 0; j < methods.Count; j++)
                            drawMethodCaller(component, methods[j] as MethodInfo);

                    }

                    GUILayout.Space(20f);
                }
                GUI.EndScrollView();
            }
        }

        private bool isValueType(Type type)
        {
            return  type == typeof(int) ||
                    type == typeof(float) ||
                    type == typeof(char) ||
                    type == typeof(bool) ||
                    type == typeof(double) ||
                    type == typeof(string);
        }

        private void drawMethodCaller(MonoBehaviour component, MethodInfo method)
        {
            GUILayout.BeginHorizontal();
            {
                drawLabelWithColor(method.Name, 230f, method.IsPublic ? green : red);
                if (GUILayout.Button("Invoke", GUILayout.Width(100f)))
                    method.Invoke(component, null);
                
                GUILayout.EndHorizontal();
            }
        }

        private void drawTitle(string title, int fontSize, bool center, bool bold, bool useSeperator, float width = -1)
        {
            int prevSize = GUI.skin.label.fontSize;
            TextAnchor prevAlignment = GUI.skin.label.alignment;
            FontStyle prevStyle = GUI.skin.label.fontStyle;

            if (fontSize > 0)
                GUI.skin.label.fontSize = fontSize;
            if (center)
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            if (bold)
                GUI.skin.label.fontStyle = FontStyle.Bold;

            if (width<0)
                GUILayout.Label(title);
            else
                GUILayout.Label(title, GUILayout.Width(width));

            if (useSeperator)
                GUILayout.Space(20f);

            GUI.skin.label.fontSize = prevSize;
            GUI.skin.label.alignment = prevAlignment;
            GUI.skin.label.fontStyle = prevStyle;
        }

        private void drawLabelWithColor(string label, float width, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = color;
            GUILayout.Label(label, style, GUILayout.Width(width));
        }

        private void drawModifier(MonoBehaviour component, FieldInfo info)
        {
            object prevVal = info.GetValue(component);

            GUILayout.BeginHorizontal();
            {
                drawLabelWithColor(info.Name, 220f, info.IsPublic ? green : red);
                Type fieldType = info.FieldType;
                object newVal = prevVal;

                if (fieldType == typeof(GameObject))
                    newVal = EditorGUILayout.ObjectField(prevVal as GameObject, typeof(GameObject));
                else if (fieldType.IsSubclassOf(typeof(Behaviour)))
                    newVal = EditorGUILayout.ObjectField(prevVal as UnityEngine.Object, fieldType);
                else if (fieldType == typeof(bool))
                    newVal = GUILayout.Toggle(Convert.ToBoolean(prevVal), "", GUILayout.Width(110f)).ToString();
                else
                    newVal = GUILayout.TextField(prevVal + "", GUILayout.Width(110f));

                if (prevVal != newVal)
                    info.SetValue(component, Convert.ChangeType(newVal, info.FieldType));
                
                GUILayout.EndHorizontal();
            }
        }
    }

    internal struct ComponentInfo
    {
        public MonoBehaviour component;
        public bool visible;
        public List<MemberInfo> fields;
        public List<MemberInfo> methods;
        public int guiLength;

        public ComponentInfo(MonoBehaviour component)
        {
            this.component = component;
            this.fields = new List<MemberInfo>();
            this.methods = new List<MemberInfo>();
            this.visible = true;
            this.guiLength = 0;
        }
    }
}