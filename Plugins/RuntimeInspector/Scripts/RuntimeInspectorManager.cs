using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    protected static T _instance;

    public static T Instance
    {
        get
        {
            //获取单例实例时如果实例为空
            if (_instance == null)
            {
                //首先在场景中寻找是否已有object挂载当前脚本
                _instance = FindObjectOfType<T>();
                //如果场景中没有挂载当前脚本那么则生成一个空的gameobject并挂载此脚本
                if (_instance == null)
                {
                    //如果创建对象，则会在创建时调用其身上脚本的Awake即调用T的Awake（T的Awake实际上是继承的父类的）
                    //所以此时无需为_instance赋值，其会在Awake中赋值。
                    new GameObject("singleton of " + typeof(T)).AddComponent<T>();
                }
            }

            return _instance;
        }
    }

    //在游戏最开始时调用Awake 如果当前脚本已经挂载到了gameobject上则会将_instance赋值为脚本自身
    private void Awake()
    {
        _instance = this as T;
    }
}

namespace RuntimeInspectorNamespace
{
    public class RuntimeInspectorManager : Singleton<RuntimeInspectorManager>
    {
        private const string POOL_OBJECT_NAME = "RuntimeInspectorPool";


        [SerializeField] private int poolCapacity = 10;
        [SerializeField] private RuntimeInspectorSettings[] settings;

        private readonly Dictionary<Type, InspectorField[]> typeToDrawers = new Dictionary<Type, InspectorField[]>(89);

        private readonly Dictionary<Type, InspectorField[]> typeToReferenceDrawers =
            new Dictionary<Type, InspectorField[]>(89);

        private readonly List<InspectorField> eligibleDrawers = new List<InspectorField>(4);

        private static readonly Dictionary<Type, List<InspectorField>> drawersPool =
            new Dictionary<Type, List<InspectorField>>();

        private Transform poolParent;
        private readonly List<VariableSet> hiddenVariables = new List<VariableSet>(32);
        private readonly List<VariableSet> exposedVariables = new List<VariableSet>(32);

        [Space] [SerializeField] private RuntimeInspector.VariableVisibility m_exposeFields =
            RuntimeInspector.VariableVisibility.SerializableOnly;

        [SerializeField] private RuntimeInspector.VariableVisibility m_exposeProperties =
            RuntimeInspector.VariableVisibility.SerializableOnly;

        private void Awake()
        {
            GameObject poolParentGO = GameObject.Find(POOL_OBJECT_NAME);
            if (poolParentGO == null)
            {
                poolParentGO = new GameObject(POOL_OBJECT_NAME);
                DontDestroyOnLoad(poolParentGO);
            }

            poolParent = poolParentGO.transform;
            RuntimeInspectorUtils.IgnoredTransformsInHierarchy.Add(poolParent);

            for (int i = 0; i < settings.Length; i++)
            {
                if (!settings[i])
                    continue;

                VariableSet[] hiddenVariablesForTypes = settings[i].HiddenVariables;
                for (int j = 0; j < hiddenVariablesForTypes.Length; j++)
                {
                    VariableSet hiddenVariablesSet = hiddenVariablesForTypes[j];
                    if (hiddenVariablesSet.Init())
                        hiddenVariables.Add(hiddenVariablesSet);
                }

                VariableSet[] exposedVariablesForTypes = settings[i].ExposedVariables;
                for (int j = 0; j < exposedVariablesForTypes.Length; j++)
                {
                    VariableSet exposedVariablesSet = exposedVariablesForTypes[j];
                    if (exposedVariablesSet.Init())
                        exposedVariables.Add(exposedVariablesSet);
                }
            }
        }

        private void OnDestroy()
        {
            if (poolParent)
            {
                RuntimeInspectorUtils.IgnoredTransformsInHierarchy.Remove(poolParent);
                DestroyImmediate(poolParent.gameObject);
            }
            
            ColorPicker.DestroyInstance();
            ObjectReferencePicker.DestroyInstance();
            drawersPool.Clear();
        }

        public void PoolDrawer(InspectorField drawer)
        {
            List<InspectorField> drawerPool;
            if (!drawersPool.TryGetValue(drawer.GetType(), out drawerPool))
            {
                drawerPool = new List<InspectorField>(poolCapacity);
                drawersPool[drawer.GetType()] = drawerPool;
            }

            if (drawerPool.Count < poolCapacity)
            {
                drawer.gameObject.SetActive(false);
                drawer.transform.SetParent(poolParent, false);
                drawerPool.Add(drawer);
            }
            else
                Destroy(drawer.gameObject);
        }

        public InspectorField InstantiateDrawer(InspectorField drawer, Transform drawerParent)
        {
            List<InspectorField> drawerPool;
            if (drawersPool.TryGetValue(drawer.GetType(), out drawerPool))
            {
                for (int i = drawerPool.Count - 1; i >= 0; i--)
                {
                    InspectorField instance = drawerPool[i];
                    drawerPool.RemoveAt(i);

                    if (instance)
                    {
                        instance.transform.SetParent(drawerParent, false);
                        instance.gameObject.SetActive(true);

                        return instance;
                    }
                }
            }

            InspectorField newDrawer = Instantiate(drawer, drawerParent, false);
            newDrawer.Initialize();
            return newDrawer;
        }


        public InspectorField[] GetDrawersForType(Type type, bool drawObjectsAsFields)
        {
            bool searchReferenceFields = drawObjectsAsFields && typeof(Object).IsAssignableFrom(type);

            InspectorField[] cachedResult;
            if ((searchReferenceFields && typeToReferenceDrawers.TryGetValue(type, out cachedResult)) ||
                (!searchReferenceFields && typeToDrawers.TryGetValue(type, out cachedResult)))
                return cachedResult;

            Dictionary<Type, InspectorField[]> drawersDict =
                searchReferenceFields ? typeToReferenceDrawers : typeToDrawers;

            eligibleDrawers.Clear();
            for (int i = settings.Length - 1; i >= 0; i--)
            {
                InspectorField[] drawers =
                    searchReferenceFields ? settings[i].ReferenceDrawers : settings[i].StandardDrawers;
                for (int j = drawers.Length - 1; j >= 0; j--)
                {
                    if (drawers[j].SupportsType(type))
                        eligibleDrawers.Add(drawers[j]);
                }
            }

            cachedResult = eligibleDrawers.Count > 0 ? eligibleDrawers.ToArray() : null;
            drawersDict[type] = cachedResult;

            return cachedResult;
        }


        internal ExposedVariablesEnumerator GetExposedVariablesForType(Type type)
        {
            MemberInfo[] allVariables = type.GetAllVariables();
            if (allVariables == null)
                return new ExposedVariablesEnumerator(null, null, null, RuntimeInspector.VariableVisibility.None,
                    RuntimeInspector.VariableVisibility.None);

            List<VariableSet> hiddenVariablesForType = null;
            List<VariableSet> exposedVariablesForType = null;
            for (int i = 0; i < hiddenVariables.Count; i++)
            {
                if (hiddenVariables[i].type.IsAssignableFrom(type))
                {
                    if (hiddenVariablesForType == null)
                        hiddenVariablesForType = new List<VariableSet>() { hiddenVariables[i] };
                    else
                        hiddenVariablesForType.Add(hiddenVariables[i]);
                }
            }

            for (int i = 0; i < exposedVariables.Count; i++)
            {
                if (exposedVariables[i].type.IsAssignableFrom(type))
                {
                    if (exposedVariablesForType == null)
                        exposedVariablesForType = new List<VariableSet>() { exposedVariables[i] };
                    else
                        exposedVariablesForType.Add(exposedVariables[i]);
                }
            }

            return new ExposedVariablesEnumerator(allVariables, hiddenVariablesForType, exposedVariablesForType,
                m_exposeFields, m_exposeProperties);
        }
    }
}