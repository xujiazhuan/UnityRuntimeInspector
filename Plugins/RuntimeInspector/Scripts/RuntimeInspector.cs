using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RuntimeInspectorNamespace
{
    public class RuntimeInspector : SkinnedWindow, ITooltipManager
    {
        public enum VariableVisibility
        {
            None = 0,
            SerializableOnly = 1,
            All = 2
        };

        public enum HeaderVisibility
        {
            Collapsible = 0,
            AlwaysVisible = 1,
            Hidden = 2
        };


        public delegate object InspectedObjectChangingDelegate(object previousInspectedObject,
            object newInspectedObject);

        public delegate void ComponentFilterDelegate(GameObject gameObject, List<Component> components);

#pragma warning disable 0649
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("refreshInterval")]
        private float m_refreshInterval;

        private float nextRefreshTime = -1f;

        public float RefreshInterval
        {
            get => m_refreshInterval;
            set => m_refreshInterval = value;
        }

        [Space] [SerializeField] private VariableVisibility m_exposeFields = VariableVisibility.SerializableOnly;

        public VariableVisibility ExposeFields
        {
            get => m_exposeFields;
            set
            {
                if (m_exposeFields != value)
                {
                    m_exposeFields = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField] private VariableVisibility m_exposeProperties = VariableVisibility.SerializableOnly;

        public VariableVisibility ExposeProperties
        {
            get => m_exposeProperties;
            set
            {
                if (m_exposeProperties != value)
                {
                    m_exposeProperties = value;
                    isDirty = true;
                }
            }
        }

        [Space] [SerializeField] private bool m_arrayIndicesStartAtOne;

        public bool ArrayIndicesStartAtOne
        {
            get => m_arrayIndicesStartAtOne;
            set
            {
                if (m_arrayIndicesStartAtOne != value)
                {
                    m_arrayIndicesStartAtOne = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField] private bool m_useTitleCaseNaming;

        public bool UseTitleCaseNaming
        {
            get => m_useTitleCaseNaming;
            set
            {
                if (m_useTitleCaseNaming != value)
                {
                    m_useTitleCaseNaming = value;
                    isDirty = true;
                }
            }
        }

        [Space] [SerializeField] private bool m_showAddComponentButton = true;

        public bool ShowAddComponentButton
        {
            get => m_showAddComponentButton;
            set
            {
                if (m_showAddComponentButton != value)
                {
                    m_showAddComponentButton = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField] private bool m_showRemoveComponentButton = true;

        public bool ShowRemoveComponentButton
        {
            get => m_showRemoveComponentButton;
            set
            {
                if (m_showRemoveComponentButton != value)
                {
                    m_showRemoveComponentButton = value;
                    isDirty = true;
                }
            }
        }

        [Space] [SerializeField] private bool m_showInspectReferenceButton = true;

        public bool ShowInspectReferenceButton
        {
            get => m_showInspectReferenceButton;
            set
            {
                if (m_showInspectReferenceButton != value)
                {
                    m_showInspectReferenceButton = value;
                    isDirty = true;
                }
            }
        }

        [Space] [SerializeField] private bool m_showTooltips;

        public bool ShowTooltips => m_showTooltips;

        [SerializeField] private float m_tooltipDelay = 0.5f;

        public float TooltipDelay
        {
            get => m_tooltipDelay;
            set => m_tooltipDelay = value;
        }

        internal TooltipListener TooltipListener { get; private set; }

        [Space] [SerializeField] private int m_nestLimit = 5;

        public int NestLimit
        {
            get => m_nestLimit;
            set
            {
                if (m_nestLimit != value)
                {
                    m_nestLimit = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField] private HeaderVisibility m_inspectedObjectHeaderVisibility = HeaderVisibility.Collapsible;

        public HeaderVisibility InspectedObjectHeaderVisibility
        {
            get => m_inspectedObjectHeaderVisibility;
            set
            {
                if (m_inspectedObjectHeaderVisibility != value)
                {
                    m_inspectedObjectHeaderVisibility = value;

                    if (currentDrawer != null && currentDrawer is ExpandableInspectorField field)
                        field.HeaderVisibility = m_inspectedObjectHeaderVisibility;
                }
            }
        }

        [SerializeField] private RuntimeHierarchy m_connectedHierarchy;

        public RuntimeHierarchy ConnectedHierarchy
        {
            get => m_connectedHierarchy;
            set => m_connectedHierarchy = value;
        }


        private bool m_isLocked;

        public bool IsLocked
        {
            get => m_isLocked;
            set => m_isLocked = value;
        }

        [SerializeField]
        private RectTransform drawArea;

#pragma warning restore 0649

        private static int aliveInspectors;

        private bool initialized;


        private InspectorField currentDrawer;
        private bool inspectLock;
        private bool isDirty;

        private object m_inspectedObject;

        public object InspectedObject => m_inspectedObject;

        public bool IsBound => !m_inspectedObject.IsNull();

        private Canvas m_canvas;

        public Canvas Canvas => m_canvas;

        // Used to make sure that the scrolled content always remains within the scroll view's boundaries
        private PointerEventData nullPointerEventData;

        public InspectedObjectChangingDelegate OnInspectedObjectChanging;

        private ComponentFilterDelegate m_componentFilter;

        public ComponentFilterDelegate ComponentFilter
        {
            get => m_componentFilter;
            set
            {
                m_componentFilter = value;
                Refresh();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            m_canvas = GetComponentInParent<Canvas>();
            nullPointerEventData = new PointerEventData(null);

            if (m_showTooltips)
            {
                TooltipListener = gameObject.AddComponent<TooltipListener>();
                TooltipListener.Initialize(this);
            }


            aliveInspectors++;


            RuntimeInspectorUtils.IgnoredTransformsInHierarchy.Add(drawArea);

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			// On new Input System, scroll sensitivity is much higher than legacy Input system
#endif
        }

        private void OnDestroy()
        {
            RuntimeInspectorUtils.IgnoredTransformsInHierarchy.Remove(drawArea);
        }

        private void OnTransformParentChanged()
        {
            m_canvas = GetComponentInParent<Canvas>();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (UnityEditor.EditorApplication.isPlaying)
                isDirty = true;
        }
#endif

        protected override void Update()
        {
            base.Update();

            if (IsBound)
            {
                float time = Time.realtimeSinceStartup;
                if (isDirty)
                {
                    // Rebind to refresh the exposed variables in Inspector
                    object inspectedObject = m_inspectedObject;
                    StopInspectInternal();
                    InspectInternal(inspectedObject);

                    isDirty = false;
                    nextRefreshTime = time + m_refreshInterval;
                }
                else
                {
                    if (time > nextRefreshTime)
                    {
                        nextRefreshTime = time + m_refreshInterval;
                        Refresh();
                    }
                }
            }
            else if (currentDrawer != null)
                StopInspectInternal();
        }

        public void Refresh()
        {
            if (IsBound)
            {
                if (currentDrawer == null)
                    m_inspectedObject = null;
                else
                    currentDrawer.Refresh();
            }
        }

        // Refreshes the Inspector in the next Update. Called by most of the InspectorDrawers
        // when their values have changed. If a drawer is bound to a property whose setter
        // may modify the input data (e.g. when input data is 20 but the setter clamps it to 10),
        // the drawer's BoundInputFields will still show the unmodified input data (20) until the
        // next Refresh. That is because BoundInputFields don't have access to the fields/properties 
        // they are modifying, there is no way for the BoundInputFields to know whether or not
        // the property has modified the input data (changed it from 20 to 10).
        // 
        // Why not refresh only the changed InspectorDrawers? Because changing a property may affect
        // multiple fields/properties that are bound to other drawers, we don't know which
        // drawers may be affected. The safest way is to refresh all the drawers.
        // 
        // Why not Refresh? That's the hacky part: most drawers call this function in their
        // BoundInputFields' OnValueSubmitted event. If Refresh was used, BoundInputField's
        // "recentText = str;" line that is called after the OnValueSubmitted event would mess up
        // with refreshing the value displayed on the BoundInputField.
        public void RefreshDelayed()
        {
            nextRefreshTime = 0f;
        }

        protected override void RefreshSkin()
        {
            if (IsBound && !isDirty)
                currentDrawer.Skin = Skin;
        }

        public void Inspect(object obj)
        {
            if (!m_isLocked)
                InspectInternal(obj);
        }

        internal void InspectInternal(object obj)
        {
            if (inspectLock)
                return;

            isDirty = false;
            Initialize();

            if (OnInspectedObjectChanging != null)
                obj = OnInspectedObjectChanging(m_inspectedObject, obj);

            if (m_inspectedObject == obj)
                return;

            StopInspectInternal();

            inspectLock = true;
            try
            {
                m_inspectedObject = obj;

                if (obj.IsNull())
                    return;

#if UNITY_EDITOR || !NETFX_CORE
                if (obj.GetType().IsValueType)
#else
				if( obj.GetType().GetTypeInfo().IsValueType )
#endif
                {
                    m_inspectedObject = null;
                    Debug.LogError("Can't inspect a value type!");
                    return;
                }

                //if( !gameObject.activeSelf )
                //{
                //	m_inspectedObject = null;
                //	Debug.LogError( "Can't inspect while Inspector is inactive!" );
                //	return;
                //}

                InspectorField inspectedObjectDrawer = CreateDrawerForType(obj.GetType(), drawArea, 0, false);
                if (inspectedObjectDrawer != null)
                {
                    inspectedObjectDrawer.BindTo(obj.GetType(), string.Empty, () => m_inspectedObject,
                        (value) => m_inspectedObject = value);
                    inspectedObjectDrawer.NameRaw = obj.GetNameWithType();
                    inspectedObjectDrawer.Refresh();

                    if (inspectedObjectDrawer is ExpandableInspectorField field)
                        field.IsExpanded = true;

                    currentDrawer = inspectedObjectDrawer;
                    if (currentDrawer is ExpandableInspectorField inspectorField)
                        inspectorField.HeaderVisibility = m_inspectedObjectHeaderVisibility;

                    GameObject go = m_inspectedObject as GameObject;
                    if (!go && m_inspectedObject as Component)
                        go = ((Component)m_inspectedObject).gameObject;

                    if (ConnectedHierarchy && go &&
                        !ConnectedHierarchy.Select(go.transform, RuntimeHierarchy.SelectOptions.FocusOnSelection))
                        ConnectedHierarchy.Deselect();
                }
                else
                    m_inspectedObject = null;
            }
            finally
            {
                inspectLock = false;
            }
        }

        public void StopInspect()
        {
            if (!m_isLocked)
                StopInspectInternal();
        }

        internal void StopInspectInternal()
        {
            if (inspectLock)
                return;

            if (currentDrawer != null)
            {
                currentDrawer.Unbind();
                currentDrawer = null;
            }

            m_inspectedObject = null;

            ColorPicker.Instance.Close();
            ObjectReferencePicker.Instance.Close();
        }

        public InspectorField CreateDrawerForType(Type type, Transform drawerParent, int depth,
            bool drawObjectsAsFields = true, MemberInfo variable = null)
        {
            InspectorField[] variableDrawers =
                RuntimeInspectorManager.Instance.GetDrawersForType(type, drawObjectsAsFields);
            if (variableDrawers != null)
            {
                for (int i = 0; i < variableDrawers.Length; i++)
                {
                    if (variableDrawers[i].CanBindTo(type, variable))
                    {
                        InspectorField drawer =
                            RuntimeInspectorManager.Instance.InstantiateDrawer(variableDrawers[i], drawerParent);
                        drawer.Inspector = this;
                        drawer.Skin = Skin;
                        drawer.Depth = depth;

                        return drawer;
                    }
                }
            }

            return null;
        }
    }
}