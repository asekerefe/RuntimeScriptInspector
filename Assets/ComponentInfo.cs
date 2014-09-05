using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace RuntimeScriptInspector
{
    public struct ComponentInfo
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
