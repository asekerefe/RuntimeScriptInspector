using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuntimeScriptInspector
{
    /*
     *  RuntimeScriptInspector
     * 
     *  Responsible for discovering fields and methods
     *  inside the attached scripts on the selected object.
     *  Both private and public object values can be
     *  changed during runtime. In addition, it can
     *  invoke methods with no parameters.
     *  
     *  It also draws the inspector view.
     * 
     *  written by Alican Şekerefe 
    */
    public class RuntimeScriptInspector : EditorWindow
    {
        //binding flags for discovery of fields and methods
        private static readonly BindingFlags fieldDiscoveryFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags methodDiscoveryFlags = fieldDiscoveryFlags | BindingFlags.DeclaredOnly;

        //colors to be used
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
            //don't forget the title (simply, doesn't work without it)
            window.title = "RSInspector";
        }

        //initializes the instance
        private void OnEnable()
        {
            window = EditorWindow.GetWindow<RuntimeScriptInspector>();
            components = new List<MonoBehaviour>();
            componentToInfoMap = new Dictionary<MonoBehaviour, ComponentInfo>();
        }

        private void Update()
        {
            //update view so that value changes can be seen realtime
            Repaint();
        }

        //selection has been changed. discover the selected object
        //and load necessary values
        private void OnSelectionChange()
        {
            selectedObject = null;

            //make sure something is selected
            if (Selection.gameObjects.Length > 0)
            {
                //get the latest selection
                GameObject lastObject = Selection.gameObjects[Selection.gameObjects.Length - 1];
                //find all custom scripts (ignores built-in components)
                MonoBehaviour[] monoComponents = lastObject.GetComponents<MonoBehaviour>();

                components.Clear();
                componentToInfoMap.Clear();

                for (int i = 0; i < monoComponents.Length; i++)
                {
                    MonoBehaviour component = monoComponents[i];
                    ComponentInfo componentInfo = new ComponentInfo(component);
                    components.Add(component);

                    //load fields and methods
                    discoverMembers(component.GetType(), componentInfo.fields, componentInfo.methods);

                    //calculate the length of the components in the gui view
                    componentInfo.guiLength = componentInfo.fields.Count * 20 + componentInfo.methods.Count * 20 + (30 + 20 + 20 + 20 + 20 + 20);
                    componentToInfoMap[component] = componentInfo;
                }

                selectedObject = lastObject;
            }

            updateGUILength();
        }

        //finds all fields and methods of the given Type object 
        //recursively (child to parent) and fills the given lists
        private void discoverMembers(Type type, List<MemberInfo> fields, List<MemberInfo> methods)
        {
            //check if the given object is discoverable (custom script)
            if (type != typeof(MonoBehaviour))
            {
                MethodInfo[] methodInfoArr = type.GetMethods(methodDiscoveryFlags);

                foreach (MethodInfo info in methodInfoArr)
                    //add the method if it doesn't require any parameter
                    if (info.GetParameters().Length == 0)
                        methods.Add(info);

                FieldInfo[] fieldInfoArr = type.GetFields(fieldDiscoveryFlags);

                foreach (FieldInfo info in fieldInfoArr)
                    fields.Add(info);

                //continue with the base type
                discoverMembers(type.BaseType, fields, methods);
            }
        }

        //updates the max length of the gui considering hidden and visible component info
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

        //draw the inspector
        private void OnGUI()
        {
            if (selectedObject != null)
            {
                //draw scroll
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

        //returns true if the given type is supported (more will come)
        private bool isBasicType(Type type)
        {
            return  type == typeof(int) ||
                    type == typeof(float) ||
                    type == typeof(char) ||
                    type == typeof(bool) ||
                    type == typeof(double) ||
                    type == typeof(string);
        }

        //draws method invokation field for the given component with its method
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

        //draws title with the given properties
        private void drawTitle(string title, int fontSize, bool center, bool bold, bool useSeperator, float width = -1)
        {
            //backup old properties
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

            //revert global skin properties
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

        //draws the value modifier field for the given component with its field
        private void drawModifier(MonoBehaviour component, FieldInfo info)
        {
            object prevVal = info.GetValue(component);

            GUILayout.BeginHorizontal();
            {
                drawLabelWithColor(info.Name, 220f, info.IsPublic ? green : red);
                Type fieldType = info.FieldType;
                object newVal = prevVal;

                //draw object field
                if (fieldType == typeof(GameObject))
                    newVal = EditorGUILayout.ObjectField(prevVal as GameObject, typeof(GameObject));
                //draw component field
                else if (fieldType.IsSubclassOf(typeof(Behaviour)))
                    newVal = EditorGUILayout.ObjectField(prevVal as UnityEngine.Object, fieldType);
                //draw checkbox
                else if (fieldType == typeof(bool))
                    newVal = GUILayout.Toggle(Convert.ToBoolean(prevVal), "", GUILayout.Width(110f)).ToString();
                //draw a textfield
                else
                    newVal = GUILayout.TextField(prevVal + "", GUILayout.Width(110f));

                //if value has changed, assign the new one
                if (prevVal != newVal)
                    info.SetValue(component, Convert.ChangeType(newVal, info.FieldType));
                
                GUILayout.EndHorizontal();
            }
        }
    }
}