using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ObjectPoolManager : MonoBehaviour
{
    // 초기화를 위해 ObjectPoolManager에 입력하는 데이터
    // 런타임에는 해당 데이터 클래스를 사용하지 않음

    [Serializable]
    public class ObjectPoolData
    {
        [FormerlySerializedAs("Key")]
        [SerializeField] private string key;
        [FormerlySerializedAs("Prefab")]
        [SerializeField] private GameObject prefab;
        [Tooltip("풀 생성 시 미리 확보할 개수. 0이면 기존 설정과 같이 Expand Size를 사용합니다.")]
        [SerializeField, Min(0)] private int initialSize;
        [FormerlySerializedAs("ExpandSize")]
        [Tooltip("풀이 소진되면 즉시 추가 생성할 개수. 0이면 1개를 생성합니다.")]
        [SerializeField] private byte expandSize;
        [FormerlySerializedAs("Parent")]
        [SerializeField] private Transform parent;

        public string Key => key;
        public GameObject Prefab => prefab;
        public int InitialSize => initialSize;
        public byte ExpandSize => expandSize;
        public Transform Parent => parent;
    }

    private class ObjectPool
    {
        private readonly Stack<IObjectPoolItem> pool = new Stack<IObjectPoolItem>();
        private readonly GameObject prefab;
        private readonly Transform parent;
        private readonly int expandSize;
        private readonly string key;

        public ObjectPool(GameObject prefab, Transform parent, int initialSize, int expandSize, string key)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.expandSize = Mathf.Max(1, expandSize);
            this.key = key;
            Expand(initialSize > 0 ? initialSize : this.expandSize);
        }

        private void Expand(int count)
        {
            for (int i = 0; i < count; i++)
            {
                IObjectPoolItem instance = Instantiate(prefab, parent).GetComponent<IObjectPoolItem>();
                instance.Key = key;
                Return(instance);
            }
        }

        public IObjectPoolItem Get()
        {
            if (pool.Count == 0) Expand(expandSize);
            return pool.Pop();
        }

        public void Return(IObjectPoolItem item)
        {
            item.GameObject.SetActive(false);
            pool.Push(item);
        }
    }

    public static ObjectPoolManager Instance { get; private set; }

    [Header("만들 풀 목록")]
    [FormerlySerializedAs("ObjectPoolDatas")]
    [SerializeField] private ObjectPoolData[] objectPoolDatas;

    private readonly Dictionary<string, ObjectPool> objectPoolDic = new Dictionary<string, ObjectPool>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{name}: ObjectPoolManager가 이미 있습니다. 이 컴포넌트는 쓰지 않습니다");
            enabled = false;
            return;
        }

        Instance = this;

        // 다른 오브젝트의 Start에서 바로 꺼내 쓸 수 있도록 Awake에서 미리 만듦
        foreach (var data in objectPoolDatas)
        {
            CreatePool(data);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void CreatePool(ObjectPoolData data)
    {
        if (objectPoolDic.ContainsKey(data.Key))
        {
            Debug.LogWarning($"{data.Key}값이 Pool에 이미 존재합니다.");
            return;
        }

        if (data.Prefab.GetComponent<IObjectPoolItem>() == null)
        {
            Debug.LogWarning($"{data.Prefab.name}의 프리펩에 IObjectPoolItem을" +
                             $"상속받은 컴포넌트가 없습니다.");
            return;
        }

        Transform poolParent = data.Parent == null ? transform : data.Parent;
        objectPoolDic.Add(data.Key, new ObjectPool(data.Prefab, poolParent, data.InitialSize, data.ExpandSize, data.Key));
    }

    public IObjectPoolItem GetObjectOrNull(string key)
    {
        if (objectPoolDic.TryGetValue(key, out var pool)) return pool.Get();

        Debug.LogWarning($"{key}값의 Pool이 존재하지 않습니다");
        return null;
    }

    public void ReturnToPool(IObjectPoolItem item)
    {
        if (objectPoolDic.TryGetValue(item.Key, out var pool) == false)
        {
            Debug.LogWarning($"{item.Key}값의 Pool이 존재하지 않습니다");
            return;
        }

        pool.Return(item);
    }
}
