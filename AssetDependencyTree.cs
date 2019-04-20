using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Syy.Tools
{
    public class AssetDependencyTree : EditorWindow
    {
        [MenuItem("Assets/Select Dependencies (Tree View)", false, 30)]
        public static void Open()
        {
            var window = GetWindow<AssetDependencyTree>("Select Dependencies");
            window.Init(Selection.objects);
        }

        DependencyNode[] _nodes;
        Vector2 _scrollPosition;
        string _filterText;

        void Init(UnityEngine.Object[] targets)
        {
            _nodes = targets.Select(target => new DependencyNode(target)).Where(node => node.Owner != null).ToArray();
            foreach (var node in _nodes)
            {
                node.Filter(_filterText);
            }
        }

        void OnGUI()
        {
            OnGUIToolbar();
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                foreach (var node in _nodes)
                {
                    OnGUINode(node, forceDisplay: true);
                }
            }
        }

        void OnGUIToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    Init(_nodes.Select(node => node.Owner).ToArray());
                }

                GUILayout.Space(5);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    _filterText = GUILayout.TextField(_filterText, "ToolbarSeachTextField", GUILayout.ExpandWidth(true));
                    if (!string.IsNullOrEmpty(_filterText) && GUILayout.Button("Clear", "ToolbarSeachCancelButton"))
                    {
                        _filterText = string.Empty;
                    }

                    if (check.changed)
                    {
                        foreach (var node in _nodes)
                        {
                            node.Filter(_filterText);
                        }
                    }
                }
            }
        }

        void OnGUINode(DependencyNode node, bool forceDisplay = false)
        {
            if (!node.IsChecked)
            {
                node.Check();
            }

            if (!forceDisplay && !node.RequireDisplay)
            {
                return;
            }

            bool beginDisabledGroup = false;
            if (forceDisplay && !node.RequireDisplay)
            {
                EditorGUI.BeginDisabledGroup(true);
                beginDisabledGroup = true;
            }

            int arrowWidth = 20;
            int iconWidth = 16;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(node.Space);
                if (node.HasDependencies)
                {
                    if (GUILayout.Button((node.IsOpened ? " ▼" : " ▶︎"), GUI.skin.label, GUILayout.Width(arrowWidth)))
                    {
                        node.IsOpened = !node.IsOpened;
                    }
                }
                else
                {
                    if (GUILayout.Button("  ", GUI.skin.label, GUILayout.Width(arrowWidth)))
                    {

                    }
                }

                var rect = GUILayoutUtility.GetLastRect();
                rect.x += iconWidth;
                GUI.DrawTexture(rect, node.Icon, ScaleMode.ScaleToFit);
                GUILayout.Space(iconWidth - 3);
                if (GUILayout.Button(node.Owner.name, GUI.skin.label))
                {
                    EditorGUIUtility.PingObject(node.Owner);
                }
            }

            if (node.IsOpened)
            {
                if (node.HasDependencies)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var dependencyNode in node.Dependencies)
                        {
                            OnGUINode(dependencyNode);
                        }
                    }
                }
            }

            if (beginDisabledGroup)
            {
                EditorGUI.EndDisabledGroup();
            }
        }

        [Serializable]
        class DependencyNode
        {
            public DependencyNode Parent { get; private set; }
            public UnityEngine.Object Owner { get; private set; }
            public string OwnerPath { get; private set; }
            public List<DependencyNode> Dependencies = new List<DependencyNode>(4);
            public bool HasDependencies { get { return Dependencies.Count > 0; } }
            public bool IsChecked { get; private set; }

            // GUI
            public bool IsOpened { get; set; }
            public Texture Icon { get; private set; }
            public float Space { get; private set; }
            public bool RequireDisplay { get { return IsFilterPassed || Dependencies.Any(dipendency => dipendency.IsFilterPassed);}}
            public bool IsFilterPassed { get; private set; } = true;
            string _fitlerText;

            public DependencyNode(UnityEngine.Object owner)
            {
                if (owner != null)
                {
                    Owner = owner;
                    OwnerPath = AssetDatabase.GetAssetPath(Owner);
                    Icon = EditorGUIUtility.ObjectContent(Owner, Owner.GetType()).image;
                }
            }

            public DependencyNode(string ownerPath, DependencyNode parent) : this(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ownerPath))
            {
                Parent = parent;
                Space = Parent.Space + 20;
            }

            public void Check()
            {
                if (!string.IsNullOrEmpty(OwnerPath))
                {
                    IEnumerable<string> paths = null;
                    if (Directory.Exists(OwnerPath))
                    {
                        paths = AssetDatabase.FindAssets("t: Object", new string[] { OwnerPath }).Distinct().Select(guid => AssetDatabase.GUIDToAssetPath(guid));
                    }
                    else
                    {
                        paths = AssetDatabase.GetDependencies(OwnerPath);
                    }
                    Dependencies.AddRange(paths.Select(path => new DependencyNode(path, this)).Where(node => node.Owner != null && node.OwnerPath != OwnerPath));
                    Filter(_fitlerText);
                }
                IsChecked = true;
            }

            public void Filter(string filterText)
            {
                _fitlerText = string.IsNullOrEmpty(filterText) ? "" : filterText.ToLower();
                IsFilterPassed = string.IsNullOrEmpty(_fitlerText) || Owner.name.ToLower().Contains(_fitlerText);
                FilterDependencies(_fitlerText);

                if (!string.IsNullOrEmpty(_fitlerText) && RequireDisplay)
                {
                    IsOpened = true;
                }
            }

            void FilterDependencies(string filterText)
            {
                foreach (var dependenciy in Dependencies)
                {
                    dependenciy.Filter(filterText);
                }
            }
        }
    }
}
